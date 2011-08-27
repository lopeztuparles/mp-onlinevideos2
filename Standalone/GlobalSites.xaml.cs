﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using OnlineVideos;

namespace Standalone
{
    /// <summary>
    /// Interaktionslogik für GlobalSites.xaml
    /// </summary>
    public partial class GlobalSites : UserControl
    {
        public GlobalSites()
        {
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            DependencyPropertyDescriptor descriptor = DependencyPropertyDescriptor.FromProperty(UIElement.VisibilityProperty, typeof(UIElement));
            descriptor.AddValueChanged(this, new EventHandler(VisibilityChanged));           
        }

        void VisibilityChanged(object sender, EventArgs e)
        {
            if (changes)
            {
                (App.Current.MainWindow as OnlineVideosMainWindow).listViewMain.ItemsSource = null;
                (App.Current.MainWindow as OnlineVideosMainWindow).listViewMain.ItemsSource = OnlineVideoSettings.Instance.SiteUtilsList;
                (App.Current.MainWindow as OnlineVideosMainWindow).SelectAndFocusFirstItem();
            }
            changes = false;

            if (Visibility == System.Windows.Visibility.Visible) lvSites.ItemsSource = SiteManager.GetOnlineSites();
        }

        bool changes = false;

        private void AddSite(object sender, RoutedEventArgs e)
        {
            OnlineVideos.OnlineVideosWebservice.Site site = (sender as Button).DataContext as OnlineVideos.OnlineVideosWebservice.Site;
            SiteSettings newSite = SiteManager.GetRemoteSite(site.Name);
            if (newSite != null)
            {
                if (!string.IsNullOrEmpty(site.RequiredDll))
                {
                    bool? errorForDll = SiteManager.DownloadDll(SiteManager.GetOnlineDlls().FirstOrDefault(dll => dll.Name == site.RequiredDll));
                    if (errorForDll == true) MessageBox.Show(App.Current.MainWindow, Translation.NewDllDownloaded, Translation.RestartMediaPortal, MessageBoxButton.OK);
                    if (errorForDll == false) { MessageBox.Show(App.Current.MainWindow, Translation.Error, Translation.GettingDll, MessageBoxButton.OK); return; }
                }
                OnlineVideoSettings.Instance.SiteSettingsList.Add(newSite);
                OnlineVideoSettings.Instance.SaveSites();
                OnlineVideoSettings.Instance.BuildSiteUtilsList();
                // refresh this list
                lvSites.ItemsSource = null;
                lvSites.ItemsSource = SiteManager.GetOnlineSites();
                changes = true;
            }
        }

        private void RemoveSite(object sender, RoutedEventArgs e)
        {
            OnlineVideos.OnlineVideosWebservice.Site site = (sender as Button).DataContext as OnlineVideos.OnlineVideosWebservice.Site;
            int localSiteIndex = -1;
            for (int i = 0; i < OnlineVideoSettings.Instance.SiteSettingsList.Count; i++) 
                if (OnlineVideoSettings.Instance.SiteSettingsList[i].Name == site.Name) 
                {
                    localSiteIndex = i;
                    break;
                }
            if (localSiteIndex != -1)
            {
                OnlineVideoSettings.Instance.SiteSettingsList.RemoveAt(localSiteIndex);
                OnlineVideoSettings.Instance.SaveSites();
                OnlineVideoSettings.Instance.BuildSiteUtilsList();
                // refresh this list
                lvSites.ItemsSource = null;
                lvSites.ItemsSource = SiteManager.GetOnlineSites();
                changes = true;
            }
        }
    }
}