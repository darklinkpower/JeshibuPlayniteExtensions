﻿using HtmlAgilityPack;
using Playnite.SDK.Models;
using PlayniteExtensions.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace Barnite.Scrapers
{
    public class PlayAsiaScraper : MetadataScraper
    {
        public PlayAsiaScraper(IPlatformUtility platformUtility, IWebDownloader webclient)
            : base(platformUtility, webclient)
        {
        }

        public override string Name { get; } = "Play-Asia";
        public override string WebsiteUrl { get; } = "https://www.play-asia.com";

        protected override string GetSearchUrlFromBarcode(string barcode)
        {
            return "https://www.play-asia.com/search/" + HttpUtility.UrlEncode(barcode);
        }

        private Regex EndBracesTextRegex = new Regex(@"(\s+(\([^)]+\)|\[[^]]+\]))+\s*$", RegexOptions.ExplicitCapture | RegexOptions.Compiled);
        private Regex SetCookieRegex = new Regex(@"\bsetCookie\('(?<c_name>\w+)', '(?<value>[.0-9]+)', (?<expiredays>[0-9]+)\);", RegexOptions.Compiled | RegexOptions.Multiline);
        private Regex JsRedirectRegex = new Regex(@"^\s*window\.location\s*=\s*'(?<url>.+?)'\s*;\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
        private Regex JsReloadRegex = new Regex(@"^\s*location\.reload\((true)?\)\s*;\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

        protected override CookieCollection ScrapeJsCookies(string html)
        {
            var cookies = new CookieCollection();

            var cookieMatch = SetCookieRegex.Match(html);
            if (!cookieMatch.Success)
                return cookies;

            string c_name = cookieMatch.Groups["c_name"].Value;
            string value = cookieMatch.Groups["value"].Value;
            int expireDays = int.Parse(cookieMatch.Groups["expiredays"].Value);

            var cookie = new Cookie(c_name, value, "/", ".play-asia.com");

            cookie.Expires = cookie.TimeStamp.AddDays(expireDays);

            cookies.Add(cookie);
            return cookies;
        }

        protected override GameMetadata ScrapeGameDetailsHtml(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            string name = doc.DocumentNode.SelectSingleNode("//div[@class='p_table']//h1[@class='p_name']/text()")?.InnerText;
            if (name == null)
                return null;

            name = EndBracesTextRegex.Replace(name, string.Empty);

            var game = new GameMetadata { Name = name.HtmlDecode() };

            string comptext = doc.DocumentNode.SelectSingleNode("//div[@class='p_table']//div[@id='comptext']")?.InnerText;
            if (comptext != null)
            {
                var match = Regex.Match(comptext, @"^\s*Compatible with (?<platform>[^(]+)", RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);
                if (match.Success)
                {
                    string platformMatch = match.Groups["platform"].Value;
                    var platformNames = platformMatch.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).Select(StringExtensions.HtmlDecode);
                    game.Platforms = new HashSet<MetadataProperty>(platformNames.Select(PlatformUtility.GetPlatform));
                }
            }

            Uri baseUri = new Uri("https://www.play-asia.com");

            var imgSrc = doc.DocumentNode.SelectSingleNode("//img[@class='unvl mainimg_r' and @data-src]")?.Attributes["data-src"].Value;
            if (imgSrc != null)
            {
                imgSrc = GetAbsoluteUrl(imgSrc);

                //remove querystring parameters that shrink the image
                int questionMarkIndex = imgSrc.IndexOf('?');
                if (questionMarkIndex != -1)
                    imgSrc = imgSrc.Remove(questionMarkIndex);

                game.CoverImage = new MetadataFile(imgSrc);
            }

            return game;
        }

        protected override IEnumerable<GameLink> ScrapeSearchResultHtml(string html)
        {
            return new GameLink[0];
        }

        protected override string ScrapeRedirectUrl(string requestUrl, string html)
        {
            var match = JsRedirectRegex.Match(html);
            if (match.Success)
                return GetAbsoluteUrl(match.Groups["url"].Value);

            if (JsReloadRegex.IsMatch(html))
                return requestUrl;

            return null;
        }
    }
}
