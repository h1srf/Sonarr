using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation.Results;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;
using RestSharp;

namespace NzbDrone.Core.Download.Clients.Pyload
{
    public class Pyload : DownloadClientBase<PyloadSettings>
    {
        public Pyload(IConfigService configService,
            IDiskProvider diskProvider,
            IRemotePathMappingService remotePathMappingService,
            Logger logger,
            ILocalizationService localizationService)
            : base(configService, diskProvider, remotePathMappingService, logger, localizationService)
        {
        }

        public override string Name
        {
            get
            {
                return "pyload";
            }
        }

        public override DownloadProtocol Protocol
        {
            get
            {
                return DownloadProtocol.File;
            }
        }

        public override async Task<string> Download(RemoteEpisode remoteEpisode, IIndexer indexer)
        {
            var client = CreateClient();
            var request = new RestRequest("/api/addPackage", Method.POST);
            var urlArray = new[] { remoteEpisode.Release.DownloadUrl };
            var packageName = remoteEpisode.Release.Title + "_" + DateTime.Now.ToFileTime();

            request.AddParameter("links", JsonConvert.SerializeObject(urlArray), ParameterType.GetOrPost);
            request.AddParameter("name", JsonConvert.SerializeObject(packageName), ParameterType.GetOrPost);
            var response = await client.ExecuteAsync(request);
            if (response.ResponseStatus != ResponseStatus.Completed)
            {
                throw new DownloadClientException("Error downloading file", response.ErrorException);
            }

            var downloadId = $"pyload_{packageName.GetHashCode()}_{response.Content}";
            _logger.Info($"DownloadId: {downloadId}");
            return downloadId;
        }

        public override IEnumerable<DownloadClientItem> GetItems()
        {
            var status = GetStatus();
            var baseDownloadFolder = status.OutputRootFolders.FirstOrDefault().FullPath;
            var client = CreateClient();

            var downloadsRequest = new RestRequest("/api/statusDownloads", Method.GET);

            var avcd = client.Execute(downloadsRequest).Content;
            var downloads = JsonConvert.DeserializeObject<List<PyloadDownloadStatus>>(client.Execute(downloadsRequest).Content);

            var currentDownloads = downloads.Select(p => new DownloadClientItem()
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

            var request = new RestRequest("/api/getQueue", Method.GET);
            var response = client.Execute(request);

            if (response.ResponseStatus != ResponseStatus.Completed)
            {
                throw new DownloadClientException("Error getting queue data", response.ErrorException);
            }

            var queueData = JsonConvert.DeserializeObject<List<PyloadQueue>>(response.Content);

            var currentDownloadIds = currentDownloads.Select(i => i.DownloadId);
            var finished = queueData.Where(x => !currentDownloadIds.Contains(x.downloadId))
                .Select(p => new DownloadClientItem()
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

            var client = CreateClient();
            var request = new RestRequest("/api/getConfig");

            var response = client.Execute(request);
            var pyloadConfig = JObject.Parse(response.Content);
            var generalConfigArray = (JArray)pyloadConfig["general"]["items"];
            var generalConfigItems = generalConfigArray.Select(g => JsonConvert.DeserializeObject<PyloadConfigItem>(g.ToString()));
            var folderItem = generalConfigItems.FirstOrDefault(f => f.Type == "folder");
            if (folderItem != null)
            {
                status.OutputRootFolders = new List<OsPath>();
                status.OutputRootFolders.Add(_remotePathMappingService.RemapRemoteToLocal(Settings.Host, new OsPath(folderItem.Value)));
            }

            return status;
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            CreateClient();
        }

        private IRestClient CreateClient()
        {
            var baseUrl = string.Format("{0}://{1}:{2}", Settings.UseSsl ? "https" : "http", Settings.Host, Settings.Port);

            var client = new RestClient(baseUrl);
            var request = new RestRequest("/api/login", Method.POST);
            request.AddParameter("username", Settings.Username);
            request.AddParameter("password", Settings.Password);

            var response = client.Execute(request);
            if (response.ResponseStatus != ResponseStatus.Completed)
            {
                throw new DownloadClientAuthenticationException("Error logging in to pyload", response.ErrorException);
            }

            return client;
        }

        public override void RemoveItem(DownloadClientItem item, bool deleteData)
        {
            var status = GetStatus();
            var baseDownloadFolder = status.OutputRootFolders.FirstOrDefault().FullPath;
            var client = CreateClient();

            var pid = item.DownloadId.Split('_').Last();

            var getPackageData = new RestRequest("/api/getPackageInfo", Method.GET);
            getPackageData.AddParameter("pid", pid, ParameterType.GetOrPost);

            var packageDataResponse = client.Execute(getPackageData);
            var packageInfo = JsonConvert.DeserializeObject<PyloadQueue>(packageDataResponse.Content);

            _logger.Debug($"Calling /api/deletePackages: pid {pid}");
            var request = new RestRequest($"/api/deletePackages/[{pid}]", Method.GET);

            var response = client.Execute(request);
            if (response.ResponseStatus != ResponseStatus.Completed)
            {
                throw new DownloadClientException("Error removing file", response.ErrorException);
            }

            if (!deleteData)
            {
                _logger.Info("Skipping data deletion: deleteData is false.");
                return;
            }

            if (!status.IsLocalhost && _remotePathMappingService.All().FirstOrDefault(a => a.Host == Settings.Host) != null)
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
    }
}
