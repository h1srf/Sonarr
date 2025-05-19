using System;
using System.Collections.Generic;
using System.Net;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Indexers.Easynews
{
    public class EasynewsRequestGenerator : IIndexerRequestGenerator
    {
        public EasynewsSettings Settings { get; set; }

        public IndexerPageableRequestChain GetRecentRequests()
        {
            var pageableRequests = new IndexerPageableRequestChain();

            var url = BuildRssUrl(null, "alt.binaries.boneless");

            var httpRequest = new HttpRequest(url);
            httpRequest.Credentials = new NetworkCredential(Settings.Username, Settings.Password);

            pageableRequests.Add(new[] { new IndexerRequest(httpRequest) });
            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(SingleEpisodeSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();
            var indexerRequests = new List<IndexerRequest>();

            foreach (var queryTitle in searchCriteria.CleanSceneTitles)
            {
                var searchTerm = $"{queryTitle}+S{searchCriteria.SeasonNumber:00}E{searchCriteria.EpisodeNumber:00}";
                var url = BuildRssUrl(searchTerm);

                var httpRequest = new HttpRequest(url);
                httpRequest.Credentials = new NetworkCredential(Settings.Username, Settings.Password);

                indexerRequests.Add(new IndexerRequest(httpRequest));
            }

            pageableRequests.AddTier(indexerRequests);
            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(SeasonSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();
            var indexerRequests = new List<IndexerRequest>();

            foreach (var queryTitle in searchCriteria.CleanSceneTitles)
            {
                var searchTerm = $"{queryTitle}+S{searchCriteria.SeasonNumber:00}";
                var url = BuildRssUrl(searchTerm);

                var httpRequest = new HttpRequest(url);
                httpRequest.Credentials = new NetworkCredential(Settings.Username, Settings.Password);

                indexerRequests.Add(new IndexerRequest(httpRequest));
            }

            pageableRequests.AddTier(indexerRequests);
            return pageableRequests;
        }

        private string BuildRssUrl(string searchTerm = null, string group = null)
        {
            var baseUrl = Settings.BaseUrl;

            var url = $"{baseUrl}?&gps=&sbj=&from=&ns=&fil=";

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                url += Uri.EscapeDataString(searchTerm);
            }

            url += "&fex=&vc=&ac=&fty[]=VIDEO&s1=dtime&s1d=-&s2=nrfile&s2d=+&s3=dsize&s3d=+";
            url += "&pby=200&grpF[]=";

            if (!string.IsNullOrWhiteSpace(group))
            {
                url += group;
            }

            url += "&svL=&d1=&d1t=&d2=&d2t=&b1=&b1t=&b2=&b2t=&px1=&px1t=&px2=&px2t=";
            url += "&fps1=&fps1t=&fps2=&fps2t=&bps1=&bps1t=&bps2=&bps2t=&hz1=&hz1t=&hz2=&hz2t=";
            url += "&rn1=&rn1t=&rn2=&rn2t=&submit=Search&fly=2&sS=5";

            return url;
        }

        public IndexerPageableRequestChain GetSearchRequests(DailyEpisodeSearchCriteria searchCriteria)
        {
            throw new NotImplementedException();
        }

        public IndexerPageableRequestChain GetSearchRequests(AnimeEpisodeSearchCriteria searchCriteria)
        {
            throw new NotImplementedException();
        }

        public IndexerPageableRequestChain GetSearchRequests(SpecialEpisodeSearchCriteria searchCriteria)
        {
            throw new NotImplementedException();
        }

        public IndexerPageableRequestChain GetSearchRequests(DailySeasonSearchCriteria searchCriteria)
        {
            throw new NotImplementedException();
        }

        public IndexerPageableRequestChain GetSearchRequests(AnimeSeasonSearchCriteria searchCriteria)
        {
            throw new NotImplementedException();
        }
    }
}
