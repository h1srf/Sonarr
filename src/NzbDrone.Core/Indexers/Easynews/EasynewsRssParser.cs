using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml.Linq;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.Easynews
{
    public class EasynewsRssParser : RssParser
    {
        protected override string GetTitle(XElement item)
        {
            // Filename is inside last parenthesis - remove autounrar
            var rawTitle = base.GetTitle(item);
            var fileMatches = Regex.Matches(rawTitle, @"\(([^\)]*)\)");
            var fileName = fileMatches.Count > 0 ? fileMatches[fileMatches.Count - 1].Groups[1].Value.Replace("AutoUnRAR", "").Trim() : rawTitle;
            return fileName;
        }

        protected override long GetEnclosureLength(XElement item)
        {
            var enclosure = item.Element("enclosure");
            if (enclosure != null)
            {
                return ParseSize(enclosure.Attribute("length")?.Value, false);
            }

            return 0;
        }

        protected override string GetDownloadUrl(XElement item)
        {
            // Prefer enclosure url, fallback to link
            var enclosure = item.Element("enclosure");
            if (enclosure != null && enclosure.Attribute("url") != null)
            {
                return enclosure.Attribute("url").Value;
            }

            var link = item.Element("link");
            return link != null ? link.Value : null;
        }

        protected override DateTime GetPublishDate(XElement item)
        {
            var pubDateStr = item.Element("pubDate")?.Value;
            if (DateTime.TryParse(pubDateStr, out var pubDate))
            {
                return pubDate.ToUniversalTime();
            }

            return DateTime.Now;
        }

        public override IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            var releases = new List<ReleaseInfo>();
            var doc = XDocument.Parse(indexerResponse.Content);

            var items = doc.Descendants("item");
            foreach (var item in items)
            {
                try
                {
                    var title = GetTitle(item);
                    var url = GetDownloadUrl(item);
                    var size = GetEnclosureLength(item);
                    var pubDate = GetPublishDate(item);

                    // URL decode filename for Title if possible
                    var decodedTitle = HttpUtility.UrlDecode(title);

                    releases.Add(new ReleaseInfo
                    {
                        DownloadProtocol = DownloadProtocol.File,
                        DownloadUrl = url,
                        PublishDate = pubDate,
                        Title = decodedTitle,
                        Size = size,
                        Guid = url
                    });
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error parsing item {i}", item.ToString());
                }
            }

            return releases;
        }
    }
}
