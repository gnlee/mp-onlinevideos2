﻿using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using OnlineVideos.Hoster;
using System.Xml;
using System.Web;
using OnlineVideos.Sites;

namespace OnlineVideos.Hoster
{
    /// <summary>
    /// Hoster class for www.sockshare.com
    /// 
    /// It's basically the same as putlocker.com, only on a different domain
    /// </summary>
    public class SockShare : HosterBase
    {
        public override string GetHosterUrl()
        {
            return "sockshare.com";
        }


        private string requestFileInformation(string url, CookieContainer cc)
        {
            string webData = WebCache.Instance.GetWebData(url, cookies: cc);
            if (!string.IsNullOrEmpty(webData))
            {
                if (!string.IsNullOrEmpty(Helpers.StringUtils.GetRegExData(@"(?<exists>This\sfile\sdoesn\'t\sexist,\sor\shas\sbeen\sremoved\s?\.)", webData, "exists")))
                    webData = string.Empty;
            }
            return webData;
        }

        private string getDlLink(string data, CookieContainer cc, string referer)
        {
            if (string.IsNullOrEmpty(data)) return string.Empty;

            string dlLink = string.Empty;

            dlLink = Helpers.StringUtils.GetRegExData(@"<a href=""/gopro\.php"">Tired of ads and waiting\? Go Pro\!</a>[\t\n\rn ]+</div>[\t\n\rn ]+<a href=""(?<link>/.*?)""", data, "link");

            if (string.IsNullOrEmpty(dlLink))
            {
                dlLink = Helpers.StringUtils.GetRegExData(@"""(?<link>/get_file\.php\?download=[A-Z0-9]+\&key=[a-z0-9]+)""", data, "link");
            }

            if (string.IsNullOrEmpty(dlLink))
            {
                dlLink = Helpers.StringUtils.GetRegExData(@"playlist: \'(?<link>/get_file\.php\?stream=[A-Za-z0-9=]+)\'", data, "link");
                if (!string.IsNullOrEmpty(dlLink))
                {
                    string tempLink = new Uri(new Uri(string.Format("{0}{1}", "http://www.", GetHosterUrl())), dlLink).AbsoluteUri;
                    string webData = WebCache.Instance.GetWebData(tempLink, cookies: cc, referer: referer);
                    if (!string.IsNullOrEmpty(webData))
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(webData);
                        XmlNamespaceManager nsmgr = new XmlNamespaceManager(doc.NameTable);
                        nsmgr.AddNamespace("media", "http://search.yahoo.com/mrss/");
                        XmlNode node = doc.SelectSingleNode(@"rss/channel/item/media:content[starts-with(@type,""video"")]", nsmgr);
                        if (node == null)
                            node = doc.SelectSingleNode(@"rss/channel/item/media:content", nsmgr);
                        dlLink = node.Attributes["url"].Value;

                        if (string.IsNullOrEmpty(dlLink))
                        {
                            dlLink = Helpers.StringUtils.GetRegExData(@"""(?<link>http://media-b\d+\.putlocker\.com/download/\d+/.*?)""", webData, "link");
                        }
                    }
                    else
                    {
                        dlLink = string.Empty;
                    }
                }
            }

            if (!String.IsNullOrEmpty(dlLink))
                dlLink = HttpUtility.HtmlDecode(dlLink).Trim('\'');
            if (new System.Uri(dlLink).IsAbsoluteUri) return dlLink;
            else return new Uri(new Uri(string.Format("{0}{1}", "http://www.", GetHosterUrl())), dlLink).AbsoluteUri;
        }

        public override string GetVideoUrl(string url)
        {
            CookieContainer cc = new CookieContainer();
            string webData = requestFileInformation(url, cc);
            if (string.IsNullOrEmpty(webData)) return string.Empty;

            Match m = Regex.Match(webData, @"<input\stype=""hidden""\svalue=""(?<hashValue>[a-z0-9]+)""\sname=""(?<hashName>[^""]+)"">\s*<input\sname=""(?<confirmName>[^""]+)""\stype=""submit""\svalue=""(?<confirmValue>[^""]+)""[^>]*>\s*</form>", MyHosterBase.DefaultRegexOptions);

            if (!m.Success) return String.Empty;

            string postData = String.Format(@"{0}={1}&{2}={3}",
                HttpUtility.UrlEncode(m.Groups["hashName"].Value), HttpUtility.UrlEncode(m.Groups["hashValue"].Value),
                HttpUtility.UrlEncode(m.Groups["confirmName"].Value), HttpUtility.UrlEncode(m.Groups["confirmValue"].Value));

            string sWaitTime = Helpers.StringUtils.GetRegExData(@"var countdownNum = (?<waittime>\d+);", webData, "waittime");
            int iWaitTime = 1;
            if (!string.IsNullOrEmpty(sWaitTime))
            {
                if (!int.TryParse(sWaitTime, out iWaitTime))
                    iWaitTime = 10;
            }

            Thread.Sleep(iWaitTime * 1001);

            string webDataLink = WebCache.Instance.GetWebData(url, postData, cc, url);

            string dlLink = getDlLink(webDataLink, cc, url);
            if (string.IsNullOrEmpty(dlLink)) return string.Empty;
            return dlLink;
        }
    }
}

