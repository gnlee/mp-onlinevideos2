﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using System.Net;
using System.IO;
using System.Threading;
using OnlineVideos.Hoster.Base;
using System.ComponentModel;

namespace OnlineVideos.Sites
{
    public class WatchSeriesUtil : GenericSiteUtil
    {
        /// <summary>
        /// Defines the number of urls that are shown for each hoster. 
        /// 
        /// For example, if the limit is 5, only the top 5 entries will be shown
        /// for each hoster (e.g. putlocker, megavideo,...).
        /// </summary>
        [Category("OnlineVideosUserConfiguration"), Description("Limit Number of Urls that are shown per hosters (0: show all).")]
        int limitUrlsPerHoster = 5;

        /// <summary>
        /// If false, all video urls that have no hoster utility yet will be hidden. If true, all urls will
        /// be shown (unknown hosters will get a "ns" suffix)
        /// </summary>
        [Category("OnlineVideosUserConfiguration"), Description("Show hosters for which no provider exists.")]
        bool showUnknownHosters = false;

        private enum Depth { MainMenu = 0, Alfabet = 1, Series = 2, Seasons = 3, BareList = 4 };
        public CookieContainer cc = null;
        private string nextVideoListPageUrl = null;
        private Category currCategory = null;

        public override void Initialize(SiteSettings siteSettings)
        {
            base.Initialize(siteSettings);
            //ReverseProxy.Instance.AddHandler(this);

        }

        public void GetBaseCookie()
        {
            HttpWebRequest request = WebRequest.Create(baseUrl) as HttpWebRequest;
            if (request == null) return;
            request.UserAgent = OnlineVideoSettings.Instance.UserAgent;
            request.Accept = "*/*";
            request.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip,deflate");
            request.CookieContainer = new CookieContainer();
            HttpWebResponse response = null;
            try
            {
                response = (HttpWebResponse)request.GetResponse();
            }
            finally
            {
                if (response != null) ((IDisposable)response).Dispose();
            }

            cc = new CookieContainer();
            CookieCollection ccol = request.CookieContainer.GetCookies(new Uri(baseUrl));
            foreach (Cookie c in ccol)
                cc.Add(c);
        }

        public override int DiscoverDynamicCategories()
        {
            GetBaseCookie();

            base.DiscoverDynamicCategories();
            int i = 0;
            do
            {
                RssLink cat = (RssLink)Settings.Categories[i];
                if (cat.Url.Equals(baseUrl) || cat.Name.ToUpperInvariant() == "HOW TO WATCH" ||
                    cat.Name.ToUpperInvariant() == "CONTACT" || cat.Name.ToUpperInvariant() == "ABOUT US"
                   )
                    Settings.Categories.Remove(cat);
                else
                {
                    if (cat.Url.EndsWith("/A"))
                        cat.Other = Depth.MainMenu;
                    else
                    {
                        cat.Other = Depth.BareList;
                        cat.HasSubCategories = false;
                    }
                    i++;
                }
            }
            while (i < Settings.Categories.Count);
            return Settings.Categories.Count;
        }

        public override int DiscoverSubCategories(Category parentCategory)
        {
            return GetSubCategories(parentCategory, ((RssLink)parentCategory).Url);
        }

        private int GetSubCategories(Category parentCategory, string url)
        {
            string webData;
            int p = url.IndexOf('#');
            if (p >= 0)
            {
                string nm = url.Substring(p + 1);
                webData = GetWebData(url.Substring(0, p), cc);
                webData = @"class=""listbig"">" + GetSubString(webData, @"class=""listbig""><a name=""" + nm + @"""", @"class=""listbig""");
            }
            else
                webData = GetWebData(url, cc);

            parentCategory.SubCategories = new List<Category>();
            Match m = null;
            switch ((Depth)parentCategory.Other)
            {
                case Depth.MainMenu:
                    webData = GetSubString(webData, @"class=""pagination""", @"class=""listbig""");
                    m = regEx_dynamicCategories.Match(webData);
                    break;
                case Depth.Alfabet:
                    webData = GetSubString(webData, @"class=""listbig""", @"class=""clear""");
                    m = regEx_dynamicSubCategories.Match(webData);
                    break;
                case Depth.Series:
                    webData = GetSubString(webData, @"class=""lists"">", @"class=""clear""");
                    string[] tmp = { @"class=""lists"">" };
                    string[] seasons = webData.Split(tmp, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string s in seasons)
                    {
                        RssLink cat = new RssLink();
                        cat.Name = HttpUtility.HtmlDecode(GetSubString(s, ">", "<")).Trim();
                        cat.Url = s;
                        cat.SubCategoriesDiscovered = true;
                        cat.HasSubCategories = false;
                        cat.Other = ((Depth)parentCategory.Other) + 1;

                        parentCategory.SubCategories.Add(cat);
                        cat.ParentCategory = parentCategory;
                    }
                    break;
                default:
                    m = null;
                    break;
            }

            while (m != null && m.Success)
            {
                RssLink cat = new RssLink();
                cat.Name = HttpUtility.HtmlDecode(m.Groups["title"].Value);
                cat.Url = m.Groups["url"].Value;
                cat.Description = HttpUtility.HtmlDecode(m.Groups["description"].Value);
                cat.HasSubCategories = !parentCategory.Other.Equals(Depth.Series);
                cat.Other = ((Depth)parentCategory.Other) + 1;

                if (cat.Name == "NEW")
                {
                    cat.HasSubCategories = false;
                    cat.Other = Depth.BareList;
                }

                parentCategory.SubCategories.Add(cat);
                cat.ParentCategory = parentCategory;
                m = m.NextMatch();
            }

            parentCategory.SubCategoriesDiscovered = true;
            return parentCategory.SubCategories.Count;
        }

        public override List<VideoInfo> getVideoList(Category category)
        {
            return getOnePageVideoList(category, ((RssLink)category).Url);
        }

        private List<VideoInfo> getOnePageVideoList(Category category, string url)
        {
            currCategory = category;
            nextVideoListPageUrl = null;
            string webData;
            if (category.Other.Equals(Depth.BareList))
            {
                webData = GetWebData(url, cc);
                webData = GetSubString(webData, @"class=""listbig""", @"class=""clear""");
                string[] parts = url.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    if (parts[parts.Length - 1] == "latest")
                        nextVideoListPageUrl = url + "/1";
                    else
                    {
                        int pageNr;
                        if (parts[parts.Length - 2] == "latest" && int.TryParse(parts[parts.Length - 1], out pageNr))
                            if (pageNr + 1 <= 9)
                                nextVideoListPageUrl = url.Substring(0, url.Length - 1) + (pageNr + 1).ToString();
                    }
                }
            }
            else
                webData = url;

            List<VideoInfo> videos = new List<VideoInfo>();
            if (!string.IsNullOrEmpty(webData))
            {
                Match m = regEx_VideoList.Match(webData);
                while (m.Success)
                {
                    SeriesVideoInfo video = new SeriesVideoInfo();
                    video.parent = this;

                    video.Title = HttpUtility.HtmlDecode(m.Groups["Title"].Value);
                    video.VideoUrl = m.Groups["VideoUrl"].Value.Replace("..", baseUrl);
                    video.Airdate = m.Groups["Airdate"].Value;
                    if (video.Airdate == "-")
                        video.Airdate = String.Empty;
                    Match m2 = Regex.Match(video.VideoUrl, @"-(?<id>\d+).html");

                    if (m2.Success) video.VideoUrl = baseUrl + "/getlinks.php?q=" + m2.Groups["id"].Value + "&domain=all";

                    try
                    {
                        string name = string.Empty;
                        int season = -1;
                        int episode = -1;
                        int year = -1;

                        // 1st way - Seas. X Ep. Y
                        //Modern Family Seas. 1 Ep. 12
                        Match trackingInfoMatch = Regex.Match(video.Title, @"(?<name>.+)\s+Seas\.\s*?(?<season>\d+)\s+Ep\.\s*?(?<episode>\d+)", RegexOptions.IgnoreCase);
                        FillTrackingInfoData(trackingInfoMatch, ref name, ref season, ref episode, ref year);

                        if (!GotTrackingInfoData(name, season, episode, year) &&
                            category != null && category.ParentCategory != null &&
                            !string.IsNullOrEmpty(category.Name) && !string.IsNullOrEmpty(category.ParentCategory.Name))
                        {
                            // 2nd way - using parent category name, category name and video title 
                            //Aaron Stone Season 1 (19 episodes) 1. Episode 21 1 Hero Rising (1)
                            string parseString = string.Format("{0} {1} {2}", category.ParentCategory.Name, category.Name, video.Title);
                            trackingInfoMatch = Regex.Match(parseString, @"(?<name>.+)\s+Season\s*?(?<season>\d+).*?Episode\s*?(?<episode>\d+)", RegexOptions.IgnoreCase);
                            FillTrackingInfoData(trackingInfoMatch, ref name, ref season, ref episode, ref year);
                        }

                        if (GotTrackingInfoData(name, season, episode, year))
                        {
                            TrackingInfo tInfo = new TrackingInfo();
                            tInfo.Title = name;
                            tInfo.Season = (uint)season;
                            tInfo.Episode = (uint)episode;
                            tInfo.VideoKind = VideoKind.TvSeries;
                            video.Other = tInfo;
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warn("Error parsing TrackingInfo data: {0}", e.ToString());
                    }

                    videos.Add(video);
                    m = m.NextMatch();
                }

            }
            return videos;
        }

        public static bool GotTrackingInfoData(string name, int season, int episode, int year)
        {
            return (!string.IsNullOrEmpty(name) && ((season > -1 && episode > -1) || (year > 1900)));
        }

        public static void FillTrackingInfoData(Match trackingInfoMatch, ref string name, ref int season, ref int episode, ref int year)
        {
            if (trackingInfoMatch != null && trackingInfoMatch.Success)
            {
                name = trackingInfoMatch.Groups["name"].Value.Trim();
                if (!int.TryParse(trackingInfoMatch.Groups["season"].Value, out season))
                {
                    season = -1;
                }
                if (!int.TryParse(trackingInfoMatch.Groups["episode"].Value, out episode))
                {
                    episode = -1;
                }
                if (!int.TryParse(trackingInfoMatch.Groups["year"].Value, out year))
                {
                    year = -1;
                }
            }
        }

        public override string getUrl(VideoInfo video)
        {
            string tmp = base.getUrl(video);
            return SortPlaybackOptions(video, baseUrl, tmp, limitUrlsPerHoster, showUnknownHosters);
        }

        /// <summary>
        /// Sorts and filters all video links (hosters) for a given video
        /// </summary>
        /// <param name="video">The video that is handled</param>
        /// <param name="baseUrl">The base url of the video</param>
        /// <param name="tmp"></param>
        /// <param name="limit">How many playback options are at most shown per hoster (0=all)</param>
        /// <param name="showUnknown">Also show playback options where no hoster is available yet</param>
        /// <returns></returns>
        public static string SortPlaybackOptions(VideoInfo video, string baseUrl, string tmp, int limit, bool showUnknown)
        {
            List<PlaybackElement> lst = new List<PlaybackElement>();
            if (video.PlaybackOptions == null) // just one
                lst.Add(new PlaybackElement("100%justone", tmp));
            else
                foreach (string name in video.PlaybackOptions.Keys)
                {
                    PlaybackElement element = new PlaybackElement(name, video.PlaybackOptions[name]);
                    element.status = "ns";
                    if (element.server.Equals("videoclipuri") ||
                        HosterFactory.ContainsName(element.server.ToLower().Replace("google", "googlevideo")))
                        element.status = String.Empty;
                    lst.Add(element);
                }

            Dictionary<string, int> counts = new Dictionary<string, int>();
            foreach (PlaybackElement el in lst)
            {
                if (counts.ContainsKey(el.server))
                    counts[el.server]++;
                else
                    counts.Add(el.server, 1);
            }
            Dictionary<string, int> counts2 = new Dictionary<string, int>();
            foreach (string name in counts.Keys)
                if (counts[name] != 1)
                    counts2.Add(name, counts[name]);

            lst.Sort(PlaybackComparer);

            for (int i = lst.Count - 1; i >= 0; i--)
            {
                if (counts2.ContainsKey(lst[i].server))
                {
                    lst[i].dupcnt = counts2[lst[i].server];
                    counts2[lst[i].server]--;
                }
            }

            video.PlaybackOptions = new Dictionary<string, string>();

            foreach (PlaybackElement el in lst)
            {
                if (!Uri.IsWellFormedUriString(el.url, System.UriKind.Absolute))
                    el.url = new Uri(new Uri(baseUrl), el.url).AbsoluteUri;

                if ((limit == 0 || el.dupcnt <= limit) && (showUnknown || el.status == null || !el.status.Equals("ns")))
                {
                    video.PlaybackOptions.Add(el.GetName(), el.url);
                }
            }

            if (lst.Count == 1)
            {
                video.VideoUrl = video.GetPlaybackOptionUrl(lst[0].GetName());
                video.PlaybackOptions = null;
                return video.VideoUrl;
            }

            if (lst.Count > 0)
                tmp = lst[0].url;

            return tmp;
        }

        public override bool HasNextPage
        {
            get
            {
                return nextVideoListPageUrl != null;
            }
        }

        public override List<VideoInfo> getNextPageVideos()
        {
            return getOnePageVideoList(currCategory, nextVideoListPageUrl);
        }

        public override bool CanSearch
        {
            get
            {
                return true;
            }
        }

        public override List<ISearchResultItem> DoSearch(string query)
        {
            List<ISearchResultItem> cats = new List<ISearchResultItem>();

            Regex r = new Regex(@"<tr><td\svalign=""top"">\s*<a\shref=""(?<url>[^""]*)""[^>]*>\s<img\ssrc=""(?<thumb>[^""]*)""[^>]*></a>\s*</td>\s*<td\svalign=""top"">\s*<a[^>]*><b>(?<title>[^<]*)</b></a>\s*<br\s/>\s*<b>Description:</b>\s*(?<description>[^<]*)<br\s/>",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

            string webData = GetWebData(baseUrl + "/search/" + query);
            Match m = r.Match(webData);
            while (m.Success)
            {
                RssLink cat = new RssLink();
                cat.Url = m.Groups["url"].Value;
                if (!string.IsNullOrEmpty(dynamicSubCategoryUrlFormatString)) cat.Url = string.Format(dynamicSubCategoryUrlFormatString, cat.Url);
                cat.Url = ApplyUrlDecoding(cat.Url, dynamicSubCategoryUrlDecoding);
                if (!Uri.IsWellFormedUriString(cat.Url, System.UriKind.Absolute)) cat.Url = new Uri(new Uri(baseUrl), cat.Url).AbsoluteUri;
                cat.Name = HttpUtility.HtmlDecode(m.Groups["title"].Value.Trim());
                cat.Thumb = m.Groups["thumb"].Value;
                if (!String.IsNullOrEmpty(cat.Thumb) && !Uri.IsWellFormedUriString(cat.Thumb, System.UriKind.Absolute)) cat.Thumb = new Uri(new Uri(baseUrl), cat.Thumb).AbsoluteUri;
                cat.Description = m.Groups["description"].Value;
                cat.Other = Depth.Series;
                cat.HasSubCategories = true;
                cats.Add(cat);
                m = m.NextMatch();
            }

            return cats;
        }

        public override ITrackingInfo GetTrackingInfo(VideoInfo video)
        {
            if (video.Other is ITrackingInfo)
                return video.Other as ITrackingInfo;

            return base.GetTrackingInfo(video);
        }

        public override string GetFileNameForDownload(VideoInfo video, Category category, string url)
        {
            if (string.IsNullOrEmpty(url)) // called for adding to favorites
                return video.Title;
            else // called for downloading
            {
                string name = base.GetFileNameForDownload(video, category, url);
                string extension = Path.GetExtension(name);
                if (String.IsNullOrEmpty(extension) || !OnlineVideoSettings.Instance.VideoExtensions.ContainsKey(extension))
                    name += ".flv";
                if (category.ParentCategory != null && !category.Other.Equals(Depth.BareList))
                {
                    string season = category.Name.Split('(')[0];
                    name = category.ParentCategory.Name + ' ' + season + ' ' + name;
                    int l;
                    do
                    {
                        l = name.Length;
                        name = name.Replace("  ", " ");
                    } while (l != name.Length);

                }
                return Utils.GetSaveFilename(name);
            }
        }


        public class SeriesVideoInfo : VideoInfo
        {
            public WatchSeriesUtil parent;

            public override string GetPlaybackOptionUrl(string url)
            {
                string newUrl = base.PlaybackOptions[url];

                parent.GetBaseCookie();
                string webData = GetWebData(newUrl, parent.cc);

                url = Regex.Match(webData, @"<a\shref=""(?<url>[^""]*)""[^>]*>Click\sHere\sto\sPlay").Groups["url"].Value;
                url = GetRedirectedUrl(url);
                if (url.StartsWith(parent.baseUrl))
                    return String.Empty;
                return GetVideoUrl(url);
            }
        }

        private static string GetSubString(string s, string start, string until)
        {
            int p = s.IndexOf(start);
            if (p == -1) return String.Empty;
            p += start.Length;
            if (until == null) return s.Substring(p);
            int q = s.IndexOf(until, p);
            if (q == -1) return s.Substring(p);
            return s.Substring(p, q - p);
        }

        private static int IntComparer(int i1, int i2)
        {
            if (i1 == i2) return 0;
            if (i1 > i2) return -1;
            return 1;
        }

        private static int PlaybackComparer(PlaybackElement e1, PlaybackElement e2)
        {
            HosterBase h1 = HosterFactory.GetHoster(e1.server);
            HosterBase h2 = HosterFactory.GetHoster(e2.server);

            //first stage is to compare priorities (the higher the user priority, the better)
            int res = (h1 != null && h2 != null) ? IntComparer(h1.UserPriority, h2.UserPriority) : 0;
            if (res != 0)
            {
                return res;
            }
            else
            {
                //no priorities found or equal priorites -> compare percentage
                res = IntComparer(e1.percentage, e2.percentage);

                if (res != 0)
                {
                    return res;
                }
                else
                {
                    //equal percentage -> see if status is different
                    res = String.Compare(e1.status, e2.status);
                    if (res != 0)
                    {
                        return res;
                    }
                    else
                    {
                        //everything else is same -> compare alphabetically
                        return String.Compare(e1.server, e2.server);
                    }
                }
            }
        }

    }

    internal class PlaybackElement
    {
        public int percentage;
        public string server;
        public string url;
        public string status;
        public string extra;
        public int dupcnt;

        public PlaybackElement()
        {
        }

        public string GetName()
        {
            string res = server;
            if (dupcnt != 0)
                res += " (" + dupcnt.ToString() + ')';
            if (!String.IsNullOrEmpty(extra))
                res += ' ' + extra;
            if (percentage > 0)
                res += ' ' + percentage.ToString() + '%';
            if (!String.IsNullOrEmpty(status))
                res += " - " + status;
            return res;
        }

        public PlaybackElement(string aPlaybackName, string aUrl)
        {
            string ser = aPlaybackName;
            if (aPlaybackName.Contains("%"))
            {
                string[] tmp = aPlaybackName.Split('%');
                percentage = int.Parse(tmp[0]);
                ser = tmp[1];
            }
            else
            {
                percentage = -1;
            }
            server = ser.TrimEnd(new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' }).Trim();
            int i = server.IndexOf(" ");
            if (i >= 0)
            {
                extra = server.Substring(i + 1);
                server = server.Substring(0, i);
            }
            i = server.LastIndexOf(".");
            if (i >= 0)
                server = server.Substring(0, i);
            url = aUrl;
        }

    }
}
