﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using OnlineVideos.Sites.WebAutomation.Factories;
using OnlineVideos.Sites.WebAutomation.ConnectorImplementations.SkyGo;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using OnlineVideos.Sites.WebAutomation.Interfaces;

namespace OnlineVideos.Sites.WebAutomation
{
    /// <summary>
    /// General Util class for web automation - that is where we load the information by scraping the website and play via a browser
    /// </summary>
    public class WebAutomationSiteUtil : SiteUtilBase, IBrowserSiteUtil
    {
        IInformationConnector _connector;

        /// <summary>
        /// The object type of the connector to use - required for IBrowserSiteUtil
        /// </summary>
        public string ConnectorEntityTypeName
        {
            get { return _connector.ConnectorEntityTypeName; }
        }

        /// <summary>
        /// The username - required for IBrowserSiteUtil
        /// </summary>
        public string UserName
        {
            get { return username; }
        }

        /// <summary>
        /// The password - required for IBrowserSiteUtil
        /// </summary>
        public string Password
        {
            get { return password; }
        }

        [Category("OnlineVideosConfiguration"), Description("Type of web automation to run")]
        ConnectorType webAutomationType = ConnectorType.SkyGo;

        /// <summary>
        /// Username required for some web automation
        /// </summary>
        [Category("OnlineVideosUserConfiguration"), LocalizableDisplayName("Login"), Description("Website user name")]
        string username;

        /// <summary>
        /// Password required for some web automation
        /// </summary>
        [Category("OnlineVideosUserConfiguration"), LocalizableDisplayName("Password"), Description("Website password"), PasswordPropertyText(true)]
        string password;

        /// <summary>
        /// Set the Web Automation Description from the enum
        /// </summary>
        /// <param name="siteSettings"></param>
        public override void Initialize(SiteSettings siteSettings)
        {
            base.Initialize(siteSettings);
            _connector = ConnectorFactory.GetInformationConnector(webAutomationType, this);
        }
        
        /// <summary>
        /// Load the list of videos - see if they've been pre-loaded when populating categories or not
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public override List<VideoInfo> getVideoList(Category category)
        {
            var result = new List<VideoInfo>();
            BuildVideos(result, category);
            return result;
        }

        /// <summary>
        /// Override the loading of main categories
        /// </summary>
        /// <returns></returns>
        public override int DiscoverDynamicCategories()
        {
            Settings.DynamicCategoriesDiscovered = true;
            //Settings.Categories.Clear();
            BuildCategories(null, Settings.Categories);
            //Settings.Categories = new BindingList<Category>(Settings.Categories.OrderBy(x => x.Name).ToList());
            return Settings.Categories.Count;
        }

        /// <summary>
        /// Override the loading of sub categories
        /// </summary>
        /// <param name="parentCategory"></param>
        /// <returns></returns>
        public override int DiscoverSubCategories(Category parentCategory)
        {
            if (parentCategory != null && parentCategory.SubCategories != null && parentCategory.SubCategories.Count > 0)
                return 1;
            parentCategory.SubCategories = new List<Category>();
            BuildCategories(parentCategory, null);
           
            parentCategory.SubCategoriesDiscovered = true;
            parentCategory.SubCategories = parentCategory.SubCategories.OrderBy(x => x.Name).ToList();
            
            return parentCategory.SubCategories.Count;
        }

        /// <summary>
        /// For the web browser automation we'll send the video id, which is stored in the other element
        /// </summary>
        /// <param name="video"></param>
        /// <returns></returns>
        public override string getUrl(VideoInfo video)
        {
            return video.Other.ToString();
        }

        /// <summary>
        /// Get the next page of categories
        /// </summary>
        /// <param name="category"></param>
        /// <returns></returns>
        public override int DiscoverNextPageCategories(NextPageCategory category)
        {
            return this.DiscoverSubCategories(category);
        }

        /// <summary>
        /// Build the specified category list - needs to be called from an STA thread
        /// </summary>
        /// <param name="parentCategory"></param>
        /// <param name="categoriesToPopulate"></param>
        private void BuildCategories(Category parentCategory, IList<Category> categoriesToPopulate)
        {
            //if (categoriesToPopulate != null) categoriesToPopulate.Clear();

            var results = _connector.LoadCategories(parentCategory);
            if (parentCategory == null)
                results.ForEach(categoriesToPopulate.Add);
        }

        /// <summary>
        /// Build the video list for the specified category
        /// </summary>
        /// <param name="videosToPopulate"></param>
        /// <param name="parentCategory"></param>
        private void BuildVideos(IList<VideoInfo> videosToPopulate, Category parentCategory)
        {
            var results = _connector.LoadVideos(parentCategory);
            results.OrderBy(x=>x.Title).ToList().ForEach(videosToPopulate.Add);
        }
    }
}
