using Newtonsoft.Json;

namespace NzbDrone.Core.Download.Clients.Pyload
{
    public class PyloadDownloadStatus
    {
        [JsonProperty("package_id")]
        public long PackageID { get; set; }

        [JsonProperty("eta")]
        public long Eta { get; set; }

        [JsonProperty("percent")]
        public double Percent { get; set; }

        [JsonProperty("bleft")]
        public long Bleft { get; set; }

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("status")]
        public PyloadStatus Status { get; set; }

        [JsonProperty("statusmsg")]
        public string Statusmsg { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("package_name")]
        public string PackageName { get; set; }

        public string DownloadId => $"pyload_{PackageName.GetHashCode()}_{PackageID}";

        public DownloadItemStatus GetDownloadItemStatus()
        {
            switch (Status)
            {
                case PyloadStatus.Processing:
                case PyloadStatus.Downloading:
                case PyloadStatus.Starting:
                    return DownloadItemStatus.Downloading;

                case PyloadStatus.Aborted:
                case PyloadStatus.Failed:
                    return DownloadItemStatus.Failed;

                case PyloadStatus.Queued:
                case PyloadStatus.Waiting:
                    return DownloadItemStatus.Queued;

                case PyloadStatus.Finished:
                    return DownloadItemStatus.Completed;
            }

            return DownloadItemStatus.Warning;
        }
    }

    public class PyloadQueue
    {
        [JsonProperty("pid")]
        public int pid { get; set; }

        [JsonProperty("sizetotal")]
        public long? sizetotal { get; set; }

        [JsonProperty("sizedone")]
        public long? sizedone { get; set; }

        [JsonProperty("folder")]
        public string folder { get; set; }

        [JsonProperty("name")]
        public string name { get; set; }

        // The new API may not include a status field; you’ll need to infer it
        public string downloadId => $"pyload_{name.GetHashCode()}_{pid}";

        public DownloadItemStatus GetDownloadItemStatus()
        {
            if (sizetotal.HasValue && sizedone.HasValue)
            {
                if (sizedone >= sizetotal)
                {
                    return DownloadItemStatus.Completed;
                }

                if (sizedone == 0)
                {
                    return DownloadItemStatus.Queued;
                }

                return DownloadItemStatus.Downloading;
            }

            return DownloadItemStatus.Warning;
        }
    }

    public enum PyloadStatus
    {
        Aborted = 9,
        Custom = 11,
        Decrypting = 10,
        Downloading = 12,
        Failed = 8,
        Finished = 0,
        Offline = 1,
        Online = 2,
        Processing = 13,
        Queued = 3,
        Skipped = 4,
        Starting = 7,
        TempOffline = 6,
        Unknown = 14,
        Waiting = 5,
    }
}
