namespace NzbDrone.Core.Download.Clients.Pyload
{
    public class PyloadDownloadStatus
    {
        public long PackageID { get; set; }
        public long Eta { get; set; }
        public double Percent { get; set; }
        public long Bleft { get; set; }
        public long Size { get; set; }
        public PyloadStatus Status { get; set; }
        public string Statusmsg { get; set; }
        public string Name { get; set; }
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
        public int pid { get; set; }
        public long? sizetotal { get; set; }
        public long? sizedone { get; set; }
        public PyloadStatus status { get; set; }
        public string folder { get; set; }
        public string name { get; set; }
        public string downloadId => $"pyload_{name.GetHashCode()}_{pid}";

        public DownloadItemStatus GetDownloadItemStatus()
        {
            switch (status)
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
