using System;
using System.Xml;
using System.Collections.Generic;
using MediaPortal.GUI.Library;
using MediaPortal.Configuration;
using System.Xml.Serialization;
using System.ComponentModel;

namespace OnlineVideos
{
    /// <summary>
    /// Description of OnlineVideoSettings.
    /// </summary>
    public class OnlineVideoSettings
    {
        public const string UserAgent = "Mozilla/5.0 (Windows; U; Windows NT 6.1; de; rv:1.9.1.3) Gecko/20090824 Firefox/3.5.3";
        public const int RTMP_PROXY_PORT = 30004;
        public const int APPLE_PROXY_PORT = 30005;
        public const string PLUGIN_NAME = "Online Videos";

        public const string SECTION = "onlinevideos";
        public const string SITEVIEW_MODE = "siteview";
        public const string SITEVIEW_ORDER = "siteview_order";
        public const string CATEGORYVIEW_MODE = "categoryview";
        public const string VIDEOVIEW_MODE = "videoview";
        
        const string SETTINGS_FILE = "OnlineVideoSites.xml";
        const string BASICHOMESCREEN_NAME = "basicHomeScreenName";
        const string THUMBNAIL_DIR = "thumbDir";
        const string DOWNLOAD_DIR = "downloadDir";
        const string FILTER = "filter";
        const string USE_AGECONFIRMATION = "useAgeConfirmation";
        const string PIN_AGECONFIRMATION = "pinAgeConfirmation";
        const string CACHE_TIMEOUT = "cacheTimeout";
        const string UTIL_TIMEOUT = "utilTimeout";
        const string WMP_BUFFER = "wmpbuffer";
        
        public bool ageHasBeenConfirmed = false;

        string bannerIconsDir = Config.GetFolder(Config.Dir.Thumbs) + @"\OnlineVideos\";
        public string BannerIconsDir { get { return bannerIconsDir; } }
        
        public string BasicHomeScreenName = "Online Videos";
        public String msThumbLocation;
        public String msDownloadDir;
        public String[] msFilterArray;
        public bool useAgeConfirmation = false;
        public string pinAgeConfirmation = "";
        public int cacheTimeout = 30; // minutes
        public int utilTimeout = 15; // seconds
        public int wmpbuffer = 5000; // milliseconds

        public BindingList<SiteSettings> SiteSettingsList { get; set; }
        public Dictionary<string, Sites.SiteUtilBase> SiteList = new Dictionary<string,OnlineVideos.Sites.SiteUtilBase>();
        
        public SortedList<string, bool> videoExtensions = new SortedList<string, bool>();
        public CodecConfiguration CodecConfiguration;        

        private static OnlineVideoSettings instance = new OnlineVideoSettings();
        public static OnlineVideoSettings getInstance()
        {
            if (instance == null) instance = new OnlineVideoSettings();
            return instance;
        }

        private OnlineVideoSettings()
        {
            Load();
            CodecConfiguration = new CodecConfiguration();
        }

        XmlSerializerImplementation _XmlSerImp;
        public XmlSerializerImplementation XmlSerImp
        {
            get
            {
                if (_XmlSerImp == null)
                {
                    _XmlSerImp = (XmlSerializerImplementation)Activator.CreateInstance(GetType().Assembly.GetType("Microsoft.Xml.Serialization.GeneratedAssembly.XmlSerializerContract", false));
                }
                return _XmlSerImp;
            }
        }

        void Load()
        {
            try
            {                
                using (MediaPortal.Profile.Settings xmlreader = new MediaPortal.Profile.Settings(Config.GetFile(Config.Dir.Config, "MediaPortal.xml")))
                {
                    BasicHomeScreenName = xmlreader.GetValueAsString(SECTION, BASICHOMESCREEN_NAME, BasicHomeScreenName);
                    msThumbLocation = xmlreader.GetValueAsString(SECTION, THUMBNAIL_DIR, "");
                    msDownloadDir = xmlreader.GetValueAsString(SECTION, DOWNLOAD_DIR, "");
                    // enable pin by default -> child protection
                    useAgeConfirmation = xmlreader.GetValueAsBool(SECTION, USE_AGECONFIRMATION, true);
                    // set an almost random string by default -> user must enter pin in Configuration before beeing able to watch adult sites
                    pinAgeConfirmation = xmlreader.GetValueAsString(SECTION, PIN_AGECONFIRMATION, DateTime.Now.Millisecond.ToString());
                    cacheTimeout = xmlreader.GetValueAsInt(SECTION, CACHE_TIMEOUT, 30);
                    utilTimeout = xmlreader.GetValueAsInt(SECTION, UTIL_TIMEOUT, 15);
                    wmpbuffer = xmlreader.GetValueAsInt(SECTION, WMP_BUFFER, 5000);
                    string lsFilter = xmlreader.GetValueAsString(SECTION, FILTER, "");
                    msFilterArray = lsFilter != "" ? lsFilter.Split(new char[] { ',' }) : null;

                    // read the video extensions configured in MediaPortal
                    string[] mediaportal_user_configured_video_extensions;
                    string strTmp = xmlreader.GetValueAsString("movies", "extensions", ".avi,.mpg,.ogm,.mpeg,.mkv,.wmv,.ifo,.qt,.rm,.mov,.sbe,.dvr-ms,.ts");
                    mediaportal_user_configured_video_extensions = strTmp.Split(',');
                    foreach (string anExt in mediaportal_user_configured_video_extensions)
                    {
                        if (!videoExtensions.ContainsKey(anExt.ToLower().Trim())) videoExtensions.Add(anExt.ToLower().Trim(), true);
                    }

                    if (!videoExtensions.ContainsKey(".asf")) videoExtensions.Add(".asf", false);
                    if (!videoExtensions.ContainsKey(".flv")) videoExtensions.Add(".flv", false);
                    if (!videoExtensions.ContainsKey(".m4v")) videoExtensions.Add(".m4v", false);
                    if (!videoExtensions.ContainsKey(".mp4")) videoExtensions.Add(".mp4", false);
                }

                // set a default thumbnail location, and fix any existing one to include the last \
                if (String.IsNullOrEmpty(msThumbLocation)) msThumbLocation = Config.GetFolder(Config.Dir.Thumbs) + @"\OnlineVideos\";                
                msThumbLocation = msThumbLocation.Replace("/", @"\");
                if (!msThumbLocation.EndsWith(@"\")) msThumbLocation = msThumbLocation + @"\";

                string filename = Config.GetFile(Config.Dir.Config, SETTINGS_FILE);
                if (!System.IO.File.Exists(filename))
                {
                    Log.Error("ConfigFile {0} was not found!", filename);
                }
                else
                {
                    using (System.IO.FileStream fs = new System.IO.FileStream(filename, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                    {
                        System.Xml.Serialization.XmlSerializer ser = XmlSerImp.GetSerializer(typeof(SerializableSettings));
                        SerializableSettings s = (SerializableSettings)ser.Deserialize(fs);
                        fs.Close();
                        SiteSettingsList = s.Sites;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }       

        public void Save()
        {
            try
            {
                Log.Info("using MP config file:" + Config.GetFile(Config.Dir.Config, "MediaPortal.xml"));
                using (MediaPortal.Profile.Settings xmlwriter = new MediaPortal.Profile.Settings(Config.GetFile(Config.Dir.Config, "MediaPortal.xml")))
                {
                    String lsFilterList = "";                    
                    if (msFilterArray != null && msFilterArray.Length > 0) lsFilterList = string.Join(",", msFilterArray);
                    
                    xmlwriter.SetValue(SECTION, FILTER, lsFilterList);
                    xmlwriter.SetValue(SECTION, BASICHOMESCREEN_NAME, BasicHomeScreenName);
                    xmlwriter.SetValue(SECTION, THUMBNAIL_DIR, msThumbLocation);
                    Log.Info("OnlineVideoSettings - download Dir:" + msDownloadDir);
                    xmlwriter.SetValue(SECTION, DOWNLOAD_DIR, msDownloadDir);                    
                    xmlwriter.SetValueAsBool(SECTION, USE_AGECONFIRMATION, useAgeConfirmation);
                    xmlwriter.SetValue(SECTION, PIN_AGECONFIRMATION, pinAgeConfirmation);
                    xmlwriter.SetValue(SECTION, CACHE_TIMEOUT, cacheTimeout);
                    xmlwriter.SetValue(SECTION, UTIL_TIMEOUT, utilTimeout);
                    xmlwriter.SetValue(SECTION, WMP_BUFFER, wmpbuffer);
                }

                // only save if there are sites - otherwise an error might have occured on load
                if (SiteSettingsList != null && SiteSettingsList.Count > 0)
                {
                    string filename = Config.GetFile(Config.Dir.Config, SETTINGS_FILE);
                    if (System.IO.File.Exists(filename)) System.IO.File.Delete(filename);
                    
                    SerializableSettings s = new SerializableSettings();
                    s.Sites = SiteSettingsList;
                    System.Xml.Serialization.XmlSerializer ser = XmlSerImp.GetSerializer(s.GetType());
                    XmlWriterSettings xmlSettings = new XmlWriterSettings();
                    xmlSettings.Encoding = System.Text.Encoding.UTF8;
                    xmlSettings.Indent = true;

                    using (System.IO.FileStream fs = new System.IO.FileStream(filename, System.IO.FileMode.Create))
                    {
                        XmlWriter writer = XmlWriter.Create(fs, xmlSettings);
                        ser.Serialize(writer, s);
                        fs.Close();
                    }                    
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex);
            }
        }     
    }
}
