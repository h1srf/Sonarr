using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FluentValidation.Results;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;

namespace NzbDrone.Core.Download.Clients.Pyload
{
    public class Pyload : DownloadClientBase<PyloadSettings>
    {
        private readonly IHttpClient _httpClient;

        public Pyload(IHttpClient httpClient,
                      IConfigService configService,
                      IDiskProvider diskProvider,
                      IRemotePathMappingService remotePathMappingService,
                      Logger logger,
                      ILocalizationService localizationService)
            : base(configService, diskProvider, remotePathMappingService, logger, localizationService)
        {
            _httpClient = httpClient;
        }

        public override string Name => "pyload";

        public override DownloadProtocol Protocol => DownloadProtocol.File;

        public override async Task<string> Download(RemoteEpisode remoteEpisode, IIndexer indexer)
        {
            EnsureLogin();

            var urlArray = new[] { remoteEpisode.Release.DownloadUrl };
            var packageName = $"{remoteEpisode.Release.Title}_{DateTime.Now.ToFileTime()}";

            var formData = new Dictionary<string, object>
            {
                ["links"] = JsonConvert.SerializeObject(urlArray),
                ["name"] = JsonConvert.SerializeObject(packageName)
            };

            var request = CreateFormRequest("/api/addPackage", formData);
            var response = await _httpClient.ExecuteAsync(request);

            if (response.HasHttpError)
            {
                throw new DownloadClientException($"Error adding package: {response.StatusCode}");
            }

            var downloadId = $"pyload_{packageName.GetHashCode()}_{response.Content}";
            _logger.Info($"DownloadId: {downloadId}");

            return downloadId;
        }

        public override IEnumerable<DownloadClientItem> GetItems()
        {
            EnsureLogin();

            var status = GetStatus();
            var baseDownloadFolder = status.OutputRootFolders.FirstOrDefault().FullPath;

            var downloadsRequest = CreateRequest("/api/statusDownloads");
            var downloadsResponse = _httpClient.Execute(downloadsRequest);

            if (downloadsResponse.HasHttpError)
            {
                throw new DownloadClientException("Error retrieving active downloads");
            }

            var downloadsJson = downloadsResponse.Content;
            var downloads = JsonConvert.DeserializeObject<List<PyloadDownloadStatus>>(downloadsJson);

            var currentDownloads = downloads.Select(p => new DownloadClientItem
            {
                DownloadId = p.DownloadId,
                Status = p.GetDownloadItemStatus(),
                RemainingSize = (long)p.Bleft,
                TotalSize = p.Size,
                RemainingTime = TimeSpan.FromSeconds(p.Eta),
                Message = p.Statusmsg,
                DownloadClientInfo = DownloadClientItemClientInfo.FromDownloadClient(this, false),
                Title = Name
            }).ToList();

            var queueRequest = CreateRequest("/api/getQueue");
            var queueResponse = _httpClient.Execute(queueRequest);

            if (queueResponse.HasHttpError)
            {
                throw new DownloadClientException("Error retrieving queue data");
            }

            var queueData = JsonConvert.DeserializeObject<List<PyloadQueue>>(queueResponse.Content);

            var currentDownloadIds = currentDownloads.Select(i => i.DownloadId).ToHashSet();
            var finished = queueData
                .Where(x => !currentDownloadIds.Contains(x.downloadId))
                .Select(p => new DownloadClientItem
                {
                    DownloadId = p.downloadId,
                    Status = p.GetDownloadItemStatus(),
                    RemainingSize = (long)(p.sizetotal - p.sizedone),
                    TotalSize = p.sizetotal ?? 0,
                    RemainingTime = TimeSpan.FromSeconds(10),
                    Message = string.Empty,
                    DownloadClientInfo = DownloadClientItemClientInfo.FromDownloadClient(this, false),
                    Title = Name,
                    OutputPath = new OsPath(System.IO.Path.Combine(baseDownloadFolder, p.folder)),
                    CanBeRemoved = true
                }).ToList();

            return currentDownloads.Union(finished);
        }

        public override DownloadClientInfo GetStatus()
        {
            var status = new DownloadClientInfo
            {
                IsLocalhost = Settings.Host == "127.0.0.1" || Settings.Host == "localhost"
            };

            EnsureLogin();

            var request = CreateRequest("/api/getConfig");
            var response = _httpClient.Execute(request);

            if (response.HasHttpError)
            {
                throw new DownloadClientException("Error retrieving Pyload config");
            }

            var content = response.Content;
            var config = JObject.Parse(content);
            var generalConfigArray = (JArray)config["general"]["items"];
            var folderItem = generalConfigArray
                .Select(g => JsonConvert.DeserializeObject<PyloadConfigItem>(g.ToString()))
                .FirstOrDefault(f => f.Type == "folder");

            if (folderItem != null)
            {
                if (folderItem != null)
                {
                    status.OutputRootFolders = new List<OsPath>
                {
                    _remotePathMappingService.RemapRemoteToLocal(Settings.Host, new OsPath(folderItem.Value))
                };
                }
            }

            return status;
        }

        public override void RemoveItem(DownloadClientItem item, bool deleteData)
        {
            EnsureLogin();

            var status = GetStatus();
            var baseDownloadFolder = status.OutputRootFolders.FirstOrDefault().FullPath;
            var pid = item.DownloadId.Split('_').Last();

            var getPackageRequest = CreateRequest($"/api/getPackageInfo/{pid}");
            getPackageRequest.Method = HttpMethod.Get;

            var packageInfoResponse = _httpClient.Execute(getPackageRequest);
            if (packageInfoResponse.HasHttpError)
            {
                throw new DownloadClientException("Failed to fetch package info");
            }

            var packageInfo = JsonConvert.DeserializeObject<PyloadQueue>(packageInfoResponse.Content);
            _logger.Debug($"Calling /api/deletePackages: pid {pid}");
            var deleteRequest = CreateRequest($"/api/deletePackages/[{pid}]");

            var deleteResponse = _httpClient.Execute(deleteRequest);
            if (deleteResponse.HasHttpError)
            {
                throw new DownloadClientException("Failed to delete package from Pyload");
            }

            if (!deleteData)
            {
                _logger.Info("Skipping data deletion: deleteData is false.");
                return;
            }

            if (!status.IsLocalhost && _remotePathMappingService.All().FirstOrDefault(a => a.Host == Settings.Host) == null)
            {
                _logger.Warn("Skipping data deletion: Pyload is remote and no remote path mapping exists.");
                return;
            }

            try
            {
                var remotePath = new OsPath(System.IO.Path.Combine(baseDownloadFolder, packageInfo.folder));
                var localPath = status.IsLocalhost
                    ? remotePath
                    : _remotePathMappingService.RemapRemoteToLocal(Settings.Host, remotePath);

                _logger.Debug($"Deleting folder: {localPath}");
                _diskProvider.DeleteFolder(localPath.FullPath, true);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to delete download data.");
            }
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            EnsureLogin();
        }

        private string BuildUrl(string path) =>
            $"{(Settings.UseSsl ? "https" : "http")}://{Settings.Host}:{Settings.Port}{path}";

        private HttpRequest CreateRequest(string path)
        {
            var url = BuildUrl(path);
            return new HttpRequest(url);
        }

        private void EnsureLogin()
        {
            var formData = new Dictionary<string, object>
            {
                ["username"] = Settings.Username,
                ["password"] = Settings.Password
            };

            var request = CreateFormRequest("/api/login", formData);
            request.StoreResponseCookie = true;

            var response = _httpClient.Execute(request);

            if (response.HasHttpError)
            {
                throw new DownloadClientAuthenticationException($"Login failed: {response.StatusCode}");
            }
        }

        private HttpRequest CreateFormRequest(string path, Dictionary<string, object> formData)
        {
            var request = CreateRequest(path);
            request.Method = HttpMethod.Post;
            request.Headers.ContentType = "application/x-www-form-urlencoded";

            var encodedPairs = formData
                .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value?.ToString() ?? string.Empty)}");

            var encodedContent = string.Join("&", encodedPairs);
            request.SetContent(encodedContent);

            return request;
        }
    }
}
