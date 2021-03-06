﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.ComponentModel;
using HtmlAgilityPack;
using System.Web;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using OnlineVideos.Sites.Utils;
using System.Collections.Specialized;
using OnlineVideos.Helpers;

namespace OnlineVideos.Sites.BrowserUtilConnectors
{

    public class NetfilxWebUtil : SiteUtilBase, IBrowserSiteUtil
    {

        #region Helper classes

        private class NetflixCategory : RssLink
        {
            internal bool InQueue { get; set; }
        }

        #endregion

        #region Settings

        [Category("OnlineVideosUserConfiguration"), LocalizableDisplayName("Username"), Description("Netflix email")]
        protected string username = null;
        [Category("OnlineVideosUserConfiguration"), LocalizableDisplayName("Password"), Description("Netflix password"), PasswordPropertyText(true)]
        protected string password = null;
        //[Category("OnlineVideosUserConfiguration"), LocalizableDisplayName("Remember log-in in browser"), Description("Remember the log-in in the Browser Player")]
        //No need to configure this at the moment...
        protected bool rememberLogin = true;
        [Category("OnlineVideosUserConfiguration"), LocalizableDisplayName("Show loading spinner"), Description("Show the loading spinner in the Browser Player")]
        protected bool showLoadingSpinner = true;

        protected const uint noOfItems = 48;

        #endregion

        #region Urls

        private string loginUrl = @"https://www.netflix.com/Login";
        private string homeUrl = @"https://www.netflix.com/";
        private string kidsUrl = @"https://www.netflix.com/Kids";
        private string playerUrl = @"http://www.netflix.com/watch/{0}";
        private string loginPostData = @"authURL={0}&email={1}&password={2}&RememberMe=on";
        private string switchProfileUrl = @"{0}/{1}/profiles/switch?switchProfileGuid={2}";

        #endregion

        #region GetWebData
        private string MyGetWebData(string url, string postData = null, string referer = null, string contentType = null)
        {
            //Never cache, problems with profiles sometimes
            string data = HboNordic.HboWebCache.Instance.GetWebData(url, postData: postData, cookies: Cookies, referer: referer, contentType: contentType, cache: false);
            //Side effects
            //AuthUrl
            Regex rgx = new Regex(@"""authURL"":""(?<authURL>[^""]*)");
            Match m = rgx.Match(data);
            if (m.Success)
            {
                latestAuthUrl = m.Groups["authURL"].Value;
            }
            return data;
        }
        #endregion

        #region profiles
        private JToken currentProfile = null;
        private List<JToken> profiles = null;
        private string ProfileName
        {
            get
            {
                return currentProfile == null ? "" : (HttpUtility.HtmlDecode(currentProfile["summary"]["profileName"].Value<string>()) + (IsKidsProfile ? " (Kids)" : string.Empty));
            }
        }

        private string ProfileToken
        {
            get
            {
                return currentProfile == null ? "" : currentProfile["summary"]["guid"].Value<string>();
            }
        }

        private bool IsKidsProfile
        {
            get
            {
                return currentProfile != null && currentProfile["summary"]["isKids"].Value<bool>();
            }
        }

        private string ProfileIcon
        {
            get
            {
                if (currentProfile == null)
                    return string.Empty;
                string icon = currentProfile["summary"]["avatarName"].Value<string>().Replace("icon", string.Empty);
                if (icon.Count() == 1)
                {
                    icon = "00" + icon;
                }
                else if (icon.Count() == 2)
                {
                    icon = "0" + icon;
                }
                return string.Format(@"http://cdn-0.nflximg.com/ffe/profiles/avatars_v2/320x320/PICON_{0}.png", icon);
            }
        }

        private void LoadProfiles()
        {
            if (profiles == null || profiles.Count == 0)
            {
                string data = MyGetWebData(homeUrl);
                Regex rgx = new Regex(@"netflix\.falkorCache = (.*)?;</script><script>window\.netflix");
                Match m = rgx.Match(data);
                if (m.Success)
                {
                    string jsonData = m.Groups[1].Value;
                    JObject json = (JObject)JsonConvert.DeserializeObject(jsonData);
                    profiles = new List<JToken>();
                    foreach (JToken profile in json["profiles"])
                    {
                        JToken val = profile.FirstOrDefault<JToken>();

                        if (val != null && val.HasValues && val.Children<JToken>().Count() > 0)
                        {
                            profiles.Add(val.Value<JToken>());
                        }
                    }
                    profiles.Reverse();
                }
                else
                {
                    _cc = null;
                    Settings.DynamicCategoriesDiscovered = false;
                    Settings.Categories.Clear();
                    profiles = null;
                    throw new OnlineVideosException("Error loading profiles. Please try again");
                }
            }
        }
        #endregion

        #region Shakti Api

        private string _shaktiApi = "";
        private string ShaktiApi
        {
            get
            {
                if (string.IsNullOrEmpty(_shaktiApi))
                {
                    SetShaktiApiAndBuildId();
                }
                return _shaktiApi;
            }
        }

        private string _buildId = "";
        private string BuildId
        {
            get
            {
                if (string.IsNullOrEmpty(_shaktiApi))
                {
                    SetShaktiApiAndBuildId();
                }
                return _buildId;
            }
        }

        private void SetShaktiApiAndBuildId(string data = "")
        {
            if (string.IsNullOrEmpty(data))
            {
                data = MyGetWebData(homeUrl);
            }
            Regex rgx = new Regex(@"\""SHAKTI_API_ROOT\"":""([^\""]*)");
            Match m = rgx.Match(data);
            if (m.Success)
            {
                _shaktiApi = m.Groups[1].Value.Replace("http:", "https:");
            }
            rgx = new Regex(@"\""BUILD_IDENTIFIER\"":""([^\""]*)");
            m = rgx.Match(data);
            if (m.Success)
            {
                _buildId = m.Groups[1].Value;
            }
        }

        #endregion

        #region Cookies, credentials and auth
 
        private string latestAuthUrl = "";

        private bool HaveCredentials
        {
            get { return !string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(password); }
        }

        private CookieContainer _cc = null;
        private CookieContainer Cookies
        {
            get
            {
                if (!HaveCredentials)
                {
                    _cc = null;
                    throw new OnlineVideosException("Please enter your email and password");
                }
                if (_cc == null)
                {
                    _cc = new CookieContainer();
                    string url = WebCache.Instance.GetRedirectedUrl(loginUrl).Replace("entrytrap", "Login");
                    // No caching in this case.
                    var htmlDoc = GetWebData<HtmlDocument>(url, cookies: _cc, cache: false);
                    HtmlNode form = htmlDoc.DocumentNode.SelectSingleNode("//form[@id = 'login-form']");
                    HtmlNode authInput = form.SelectSingleNode("//input[@name = 'authURL']");
                    string authUrl = authInput != null ? authInput.GetAttributeValue("value", "") : "";
                    var data = GetWebData<HtmlDocument>(url, string.Format(loginPostData, HttpUtility.UrlEncode(authUrl), HttpUtility.UrlEncode(username), HttpUtility.UrlEncode(password)), _cc, cache: false);
                    if (!(data.DocumentNode.SelectSingleNode("//form[@id = 'login-form']") == null))
                    {
                        _cc = null;
                        Settings.DynamicCategoriesDiscovered = false;
                        Settings.Categories.Clear();
                        throw new OnlineVideosException("Email and password does not match, or error in login process. Please try again.");
                    }

                }
                return _cc;
            }
        }

        #endregion

        #region SiteUtilBase

        #region Categories

        public override int DiscoverDynamicCategories()
        {
            LoadProfiles();
            foreach (JToken p in profiles)
            {
                currentProfile = p;
                RssLink profile = new RssLink()
                {
                    HasSubCategories = true,
                    Name = ProfileName,
                    Thumb = ProfileIcon
                };
                currentProfile = null;
                profile.Other = (Func<List<Category>>)(() => GetProfileSubCategories(profile, p));
                Settings.Categories.Add(profile);
            }

            Settings.DynamicCategoriesDiscovered = Settings.Categories.Count > 0;
            return Settings.Categories.Count;
        }

        public override int DiscoverSubCategories(Category parentCategory)
        {
            var method = parentCategory.Other as Func<List<Category>>;
            if (method != null)
            {
                parentCategory.SubCategories = method.Invoke();
                return parentCategory.SubCategories.Count;
            }
            return 0;
        }

        public override int DiscoverNextPageCategories(NextPageCategory category)
        {
            category.ParentCategory.SubCategories.Remove(category);
            var method = category.Other as Func<List<Category>>;
            if (method != null)
            {
                List<Category> cats = method.Invoke();
                category.ParentCategory.SubCategories.AddRange(cats);
                return cats.Count;
            }
            return 0;
        }

        private List<Category> GetProfileSubCategories(Category parentCategory, JToken profile)
        {
            currentProfile = profile;
            MyGetWebData(string.Format(switchProfileUrl, ShaktiApi, BuildId, ProfileToken), referer: homeUrl);
            List<Category> cats = new List<Category>();
            //My List
            RssLink myList = new RssLink() { Name = "My List", HasSubCategories = true, ParentCategory = parentCategory };
            myList.Other = (Func<List<Category>>)(() => GetListCategories(myList, "mylist", 0));
            cats.Add(myList);
            //continueWatching
            RssLink continueWatching = new RssLink() { Name = "Continue Watching", HasSubCategories = true, ParentCategory = parentCategory };
            continueWatching.Other = (Func<List<Category>>)(() => GetListCategories(continueWatching, "continueWatching", 0));
            cats.Add(continueWatching);

            if (!IsKidsProfile)
            {
                RssLink browse = new RssLink() { Name = "Browse Genres", HasSubCategories = true, ParentCategory = parentCategory };
                browse.Other = (Func<List<Category>>)(() => GetGenreListCategories(browse));
                cats.Add(browse);
            }
            else
            {
                RssLink browse = new RssLink() { Name = "Browse Kids Genres", HasSubCategories = true, ParentCategory = parentCategory };
                browse.Other = (Func<List<Category>>)(() => GetKidsGenreListCategories(browse));
                cats.Add(browse);
                RssLink kids = new RssLink() { Name = "Kids", HasSubCategories = true, ParentCategory = parentCategory };
                kids.Other = (Func<List<Category>>)(() => GetKidsHomeCategories(kids));
                cats.Add(kids);
            }

            //Do not remember profile cats, need to be loaded every time
            parentCategory.SubCategoriesDiscovered = false;
            return cats;
        }

        private List<Category> GetKidsGenreListCategories(Category parentCategory)
        {
            List<Category> cats = new List<Category>();
            string data = MyGetWebData(kidsUrl, referer: kidsUrl);
            data = MyGetWebData(kidsUrl, referer: kidsUrl);
            Regex rgx = new Regex(@"<li><a href=""http://www.netflix.com/KidsAltGenre\?agid=(?<url>[^""]*)"">(?<title>[^<]*)");
            foreach (Match m in rgx.Matches(data))
            {
                RssLink cat = new RssLink() { Name = m.Groups["title"].Value, Url = m.Groups["url"].Value, ParentCategory = parentCategory, HasSubCategories = true  };
                cat.Other = (Func<List<Category>>)(() => GetSubCategories(cat, "genres", 0));
                cats.Add(cat);
            }
            parentCategory.SubCategoriesDiscovered = true;
            return cats;
        }

        private List<Category> GetKidsHomeCategories(Category parentCategory)
        {
            List<Category> cats = new List<Category>();
            string data = MyGetWebData(kidsUrl, referer: kidsUrl);
            data = MyGetWebData(kidsUrl, referer: kidsUrl);
            HtmlDocument doc = new HtmlDocument();
            doc.LoadHtml(data);

            foreach (HtmlNode row in doc.DocumentNode.SelectNodes("//div[contains(@class,'mrow')]"))
            {
                try
                {
                    RssLink cat = null;
                    HtmlNode link = row.SelectSingleNode("div/h3/a");
                    if (link != null)
                    {
                        string url = link.GetAttributeValue("href", "");
                        url = HttpUtility.UrlDecode(url);
                        string queryString = new System.Uri(url).Query;
                        NameValueCollection queryDictionary = HttpUtility.ParseQueryString(queryString);
                        if (url.Contains("similars"))
                        {
                            url = queryDictionary["d"];
                            url = Regex.Replace(url, "[^0-9]", string.Empty);
                            cat = new RssLink() { Name = link.InnerText.Trim(), Url = url, ParentCategory = parentCategory, HasSubCategories = true };
                            cat.Other = (Func<List<Category>>)(() => GetSubCategories(cat, "similars", 0));
                        }
                        else if (url.Contains("agid"))
                        {
                            url = queryDictionary["agid"];
                            cat = new RssLink() { Name = link.InnerText.Trim(), Url = url, ParentCategory = parentCategory, HasSubCategories = true };
                            cat.Other = (Func<List<Category>>)(() => GetSubCategories(cat, "genres", 0));
                        }
                        if (cat != null)
                        {
                            // Some nice covers for the kids...
                            try
                            {
                                HtmlNode img = row.SelectSingleNode(".//img[contains(@class,'boxShotImg')]");
                                if (img != null)
                                {
                                    cat.Thumb = img.GetAttributeValue("src", "");
                                    if (string.IsNullOrWhiteSpace(cat.Thumb))
                                        cat.Thumb = img.GetAttributeValue("hsrc", "");
                                }
                            }
                            catch { }
                            cats.Add(cat);
                        }
                    }
                }
                catch { }
            }
            parentCategory.SubCategoriesDiscovered = true;
            return cats;
        }

        private List<Category> GetGenreListCategories(Category parentCategory)
        {
            List<Category> cats = new List<Category>();
            string data = MyGetWebData(ShaktiApi + "/" + BuildId + "/pathEvaluator?withSize=true&materialize=true&model=bale&esn=www",
                postData: @"{""paths"":[[""genreList"",{""from"":0,""to"":24},[""id"",""menuName""]],[""genreList"",""summary""]],""authURL"":""" + latestAuthUrl + @"""}",
                contentType: "application/json");
            JObject json = (JObject)JsonConvert.DeserializeObject(data);
            foreach (JToken token in json["value"]["genres"].Where(t => t.Values().Count() > 1 && t.First()["menuName"] != null))
            {
                JToken item = token.First();
                RssLink cat = new RssLink() { ParentCategory = parentCategory, Name = item["menuName"].Value<string>(), Url = item["id"].Value<UInt32>().ToString(), HasSubCategories = true };
                cat.Other = (Func<List<Category>>)(() => GetSubCategories(cat, "genres", 0, true));
                cats.Add(cat);
            }
            parentCategory.SubCategoriesDiscovered = true;
            return cats;
        }

        private List<Category> GetListCategories(Category parentCategory, string listType, uint startIndex)
        {
            List<Category> cats = new List<Category>();

            string data = MyGetWebData(ShaktiApi + "/" + BuildId + "/pathEvaluator?withSize=true&materialize=true&model=harris&fallbackEsn=SLW32",
                postData: @"{""paths"":[[""lolomo"",""summary""],[""lolomo"",""" + listType + @""",{""from"":" + startIndex + @",""to"":" + (startIndex + noOfItems - 1) + @"},[""summary"",""title"",""synopsis"",""queue""]],[""lolomo"",""" + listType + @""",{""from"":" + startIndex + @",""to"":" + (startIndex + noOfItems) + @"},""boxarts"",""_342x192"",""jpg""],[""lolomo"",""" + listType + @""",[""context"",""id"",""length"",""name"",""trackIds"",""requestId""]]],""authURL"":""" + latestAuthUrl + @"""}",
                contentType: "application/json");
            JObject json = (JObject)JsonConvert.DeserializeObject(data);
            if (json["value"] != null && json["value"]["videos"] != null)
            {

                foreach (JToken token in json["value"]["videos"].Where(t => t.Values().Count() > 1 && t.First()["title"] != null))
                {
                    JToken item = token.First();
                    NetflixCategory cat = new NetflixCategory() { ParentCategory = parentCategory, Name = item["title"].Value<string>(), Description = item["synopsis"].Value<string>(), HasSubCategories = true, InQueue = item["queue"]["inQueue"].Value<bool>() };
                    cat.Thumb = item["boxarts"]["_342x192"]["jpg"]["url"].Value<string>();
                    JToken summary = item["summary"];
                    cat.Url = summary["id"].Value<UInt32>().ToString();
                    cat.Other = (Func<List<Category>>)(() => GetTitleCategories(cat, summary["type"].Value<string>() == "show"));
                    cats.Add(cat);
                }

                //Paging
                int length = json["value"]["lists"].First(t => t.Values().Count() > 1).First()["length"].Value<int>();
                if (length > noOfItems + startIndex)
                {
                    NextPageCategory next = new NextPageCategory() { ParentCategory = parentCategory };
                    next.Other = (Func<List<Category>>)(() => GetListCategories(parentCategory, listType, noOfItems + startIndex));
                    cats.Add(next);
                }
            }
            //Do not remember My List, need to be able to load new items
            parentCategory.SubCategoriesDiscovered = false;
            return cats;
        }

        private List<Category> GetSubCategories(Category parentCategory, string categoryType, uint startIndex, bool getSubGenres = false)
        {
            List<Category> cats = new List<Category>();
            string id = (parentCategory as RssLink).Url;
            string data;
            JObject json;
            if (getSubGenres)
            {
                Category subgenreCat = new Category() {Name = "Subgenres", SubCategories = new List<Category>(), ParentCategory = parentCategory, HasSubCategories = true, SubCategoriesDiscovered = true};
                data = MyGetWebData(ShaktiApi + "/" + BuildId + "/pathEvaluator?withSize=true&materialize=true&model=harris&fallbackEsn=SLW32",
                    postData: @"{""paths"":[[""genres""," + id + @",""subgenres"",{""from"":0,""to"":20},""summary""]],""authURL"":""" + latestAuthUrl + @"""}",
                    contentType: "application/json");
                json = (JObject)JsonConvert.DeserializeObject(data);
                foreach (JToken token in json["value"]["genres"].Where(t => t.Values().Count() > 1 && t.First()["summary"] != null))
                {
                    JToken summary = token.First()["summary"];
                    RssLink subCat = new RssLink() { Name = summary["menuName"].Value<string>(), Url = summary["id"].Value<UInt32>().ToString(), HasSubCategories = true, ParentCategory = subgenreCat};
                    subCat.Other = (Func<List<Category>>)(() => GetSubCategories(subCat, categoryType, 0));
                    subgenreCat.SubCategories.Add(subCat);
                }
                if (subgenreCat.SubCategories.Count > 0)
                {
                    cats.Add(subgenreCat);
                }
            }

            data = MyGetWebData(ShaktiApi + "/" + BuildId + "/pathEvaluator?withSize=true&materialize=true&model=harris&fallbackEsn=SLW32",
                postData: @"{""paths"":[[""" + categoryType + @"""," + id + @",{""from"":" + startIndex + @",""to"":" + (startIndex + noOfItems - 1) + @"},[""summary"",""title"",""synopsis"",""queue""]],[""" + categoryType + @"""," + id + @",{""from"":" + startIndex + @",""to"":" + (startIndex + noOfItems - 1) + @"},""boxarts"",""_342x192"",""jpg""]],""authURL"":""" + latestAuthUrl + @"""}",
                contentType: "application/json");
            json = (JObject)JsonConvert.DeserializeObject(data);
            if (json["value"] != null && json["value"]["videos"] != null)
            {
                foreach (JToken token in json["value"]["videos"].Where(t => t.Values().Count() > 1 && t.First()["title"] != null))
                {
                    JToken item = token.First();
                    NetflixCategory cat = new NetflixCategory() { ParentCategory = parentCategory, Name = item["title"].Value<string>(), Description = item["synopsis"].Value<string>(), HasSubCategories = true, InQueue = item["queue"]["inQueue"].Value<bool>() };
                    cat.Thumb = item["boxarts"]["_342x192"]["jpg"]["url"].Value<string>();
                    JToken summary = item["summary"];
                    cat.Url = summary["id"].Value<UInt32>().ToString();
                    cat.Other = (Func<List<Category>>)(() => GetTitleCategories(cat, summary["type"].Value<string>() == "show"));
                    cats.Add(cat);
                }
                if (cats.Count() >= noOfItems)
                {
                    NextPageCategory next = new NextPageCategory() { ParentCategory = parentCategory };
                    next.Other = (Func<List<Category>>)(() => GetSubCategories(parentCategory, categoryType, noOfItems + startIndex + 1)); //Why + 1???
                    cats.Add(next);
                }
            }
            parentCategory.SubCategoriesDiscovered = true;
            return cats;
        }

        private List<Category> GetTitleCategories(Category parentCategory, bool isShow)
        {
            List<Category> cats = new List<Category>();
            string id = (parentCategory as RssLink).Url;

            //Play Now/Continue
            RssLink playNowCat = new RssLink() { Name = "Play Now/Continue Watch", Thumb = parentCategory.Thumb, HasSubCategories = false, ParentCategory = parentCategory, Url = id };
            if (isShow)
                playNowCat.Other = (Func<List<VideoInfo>>)(() => GetPlayNowShowVideos(playNowCat));
            else
                playNowCat.Other = (Func<List<VideoInfo>>)(() => GetPlayNowMovieVideos(playNowCat));

            cats.Add(playNowCat);

            if (isShow)
            {
                //Seasons
                string data = MyGetWebData(ShaktiApi + "/" + BuildId + "/pathEvaluator?withSize=true&materialize=true&model=harris&fallbackEsn=SLW32",
                    postData: @"{""paths"":[[""videos""," + id + @",""seasonList"",{""from"":0,""to"":20},""summary""],[""videos""," + id + @",""seasonList"",""summary""]],""authURL"":""" + latestAuthUrl + @"""}",
                    contentType: "application/json");
                JObject json = (JObject)JsonConvert.DeserializeObject(data);

                foreach (JToken token in json["value"]["seasons"].Where(t => t.Values().Count() > 1))
                {
                    JToken item = token.First();
                    RssLink cat = new RssLink() { HasSubCategories = false, ParentCategory = parentCategory };
                    JToken summary = item["summary"];
                    cat.Url = summary["id"].Value<UInt32>().ToString();
                    cat.Name = summary["name"].Value<string>();
                    cat.Other = (Func<List<VideoInfo>>)(() => GetSeasonVideos(cat, summary["length"].Value<uint>()));
                    cats.Add(cat);
                }
            }
            //Similar
            RssLink similarCat = new RssLink() { Name = "More like " + parentCategory.Name, HasSubCategories = true, ParentCategory = parentCategory, Url = id };
            similarCat.Other = (Func<List<Category>>)(() => GetSubCategories(similarCat, "similars", 0));
            cats.Add(similarCat);

            parentCategory.SubCategoriesDiscovered = true;
            return cats;
        }

        #endregion

        #region Videos

        public override List<VideoInfo> GetVideos(Category category)
        {
            var method = category.Other as Func<List<VideoInfo>>;
            List<VideoInfo> videos = new List<VideoInfo>();
            if (method != null)
            {
                videos = method.Invoke();
            }
            return videos;
        }

        private List<VideoInfo> GetSeasonVideos(Category category, uint noOfEpisodes)
        {
            List<VideoInfo> videos = new List<VideoInfo>();
            string id = (category as RssLink).Url;
            string data = MyGetWebData(ShaktiApi + "/" + BuildId + "/pathEvaluator?withSize=true&materialize=true&model=harris&fallbackEsn=SLW32",
                postData: @"{""paths"":[[""seasons""," + id + @",""episodes"",{""from"":-1,""to"":" + noOfEpisodes + @"},[""summary"",""synopsis"",""title"",""runtime""]],[""seasons""," + id + @",""episodes"",{""from"":-1,""to"":" + noOfEpisodes + @"},""interestingMoment"",""_260x146"",""jpg""]],""authURL"":""" + latestAuthUrl + @"""}",
                contentType: "application/json");
            JObject json = (JObject)JsonConvert.DeserializeObject(data);
            foreach (JToken token in json["value"]["videos"].Where(t => t.Values().Count() > 1))
            {
                JToken item = token.First();
                VideoInfo video = new VideoInfo();
                JToken summary = item["summary"];
                uint e = summary["episode"].Value<uint>();
                uint s = summary["season"].Value<uint>();
                video.Title = s.ToString() + "x" + (e > 9 ? e.ToString() : ("0" + e.ToString())) + " " + item["title"].Value<string>();
                video.Description = item["synopsis"].Value<string>();
                video.Thumb = item["interestingMoment"]["_260x146"]["jpg"]["url"].Value<string>();
                video.VideoUrl = string.Format(playerUrl, summary["id"].Value<UInt32>());
                videos.Add(video);
            }

            return videos;
        }

        private List<VideoInfo> GetPlayNowShowVideos(Category category)
        {
            string id = (category as RssLink).Url;
            string data = MyGetWebData(ShaktiApi + "/" + BuildId + "/pathEvaluator?withSize=true&materialize=true&model=harris&fallbackEsn=SLW32",
                postData: @"{""paths"":[[""videos""," + id + @",""current"",[""summary"",""runtime"",""title"",""synopsis""]],[""videos""," + id + @",""current"",[""interestingMoment""],""_260x146"",""jpg""]],""authURL"":""" + latestAuthUrl + @"""}",
                contentType: "application/json");
            JObject json = (JObject)JsonConvert.DeserializeObject(data);
            JToken item = json["value"]["videos"].First(t => t.Values().Count() > 1 && t.First()["size"].Value<int>() > 2).First();
            VideoInfo video = new VideoInfo();
            JToken summary = item["summary"];
            uint e = summary["episode"].Value<uint>();
            uint s = summary["season"].Value<uint>();
            video.Title = s.ToString() + "x" + (e > 9 ? e.ToString() : ("0" + e.ToString())) + " " + item["title"].Value<string>();
            video.Description = item["synopsis"].Value<string>();
            video.Thumb = item["interestingMoment"]["_260x146"]["jpg"]["url"].Value<string>();
            video.VideoUrl = string.Format(playerUrl, summary["id"].Value<UInt32>());
            return new List<VideoInfo>() { video };
        }

        private List<VideoInfo> GetPlayNowMovieVideos(Category category)
        {
            string id = (category as RssLink).Url;
            VideoInfo video = new VideoInfo();
            video.Title = category.ParentCategory.Name;
            video.Description = category.ParentCategory.Description;
            video.Thumb = category.Thumb;
            video.VideoUrl = string.Format(playerUrl, id);
            return new List<VideoInfo>() { video };
        }

        #endregion

        #region Search

        public override bool CanSearch
        {
            get
            {
                return true;
            }
        }

        public override List<SearchResultItem> Search(string query, string category = null)
        {
            List<SearchResultItem> results = new List<SearchResultItem>();
            if (currentProfile != null)
            {
                RssLink cat = new RssLink() { Url = @"""" + HttpUtility.UrlEncode(query) + @""""};
                cat.SubCategories = GetSubCategories(cat, "search", 0);
                cat.SubCategories.ForEach(c => results.Add(c));
            }
            else
            {
                throw new OnlineVideosException("Please select a profile before searching.");
            }
            return results;
        }

        #endregion

        #region Context menu

        public override List<ContextMenuEntry> GetContextMenuEntries(Category selectedCategory, VideoInfo selectedItem)
        {
            List<ContextMenuEntry> result = new List<ContextMenuEntry>();
            if (selectedCategory != null && selectedCategory is NetflixCategory)
            {
                ContextMenuEntry entry = new ContextMenuEntry() { DisplayText = ((selectedCategory as NetflixCategory).InQueue ? "Remove from " : "Add to ") + "My List (" + ProfileName + ")"};
                result.Add(entry);
            }
            return result;
        }

        public override ContextMenuExecutionResult ExecuteContextMenuEntry(Category selectedCategory, VideoInfo selectedItem, ContextMenuEntry choice)
        {
            if ((choice.DisplayText.StartsWith("Add to My List") || choice.DisplayText.StartsWith("Remove from My List")) && selectedCategory != null && selectedCategory is NetflixCategory)
            {
                ContextMenuExecutionResult result = new ContextMenuExecutionResult();
                string addRemove = choice.DisplayText.StartsWith("Add to My List") ? "add" : "remove";
                string videoId = (selectedCategory as NetflixCategory).Url;
                //string trackId = (selectedCategory as NetflixCategory).TrackId;
                string title = selectedCategory.Name;
                string data = MyGetWebData(ShaktiApi + "/" + BuildId + "/playlistop?fallbackEsn=NFCDSF-01-",
                    postData: @"{""operation"":"""+ addRemove + @""",""videoId"":" + videoId + @",""trackId"":0,""authURL"":""" + latestAuthUrl + @"""}",
                    contentType: "application/json");
                JObject json = (JObject)JsonConvert.DeserializeObject(data);
                //Do something with the result json...
                (selectedCategory as NetflixCategory).InQueue = !(selectedCategory as NetflixCategory).InQueue;
                result.RefreshCurrentItems = true;
                result.ExecutionResultMessage = title + (choice.DisplayText.StartsWith("Add to My List") ? " added to" : " removed from") + " My List";
                return result;
            }
            return base.ExecuteContextMenuEntry(selectedCategory, selectedItem, choice);
        }

        #endregion

        #endregion

        #region IBrowserSiteUtil
        string IBrowserSiteUtil.ConnectorEntityTypeName
        {
            get
            {
                return "OnlineVideos.Sites.BrowserUtilConnectors.NetflixConnector";
            }
        }

        string IBrowserSiteUtil.UserName
        {
            get { return username + "¥" + ProfileToken + (showLoadingSpinner ? "SHOWLOADING" : "") + (rememberLogin ? "REMEMBERLOGIN" : ""); }
        }

        string IBrowserSiteUtil.Password
        {
            get { return password; }
        }

        #endregion

    }
}
