﻿using HandyControl.Controls;
using HandySub.Models;
using HtmlAgilityPack;
using ModernWpf.Navigation;
using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net;
using System.Windows;
using System.Windows.Navigation;
using Page = ModernWpf.Controls.Page;
using Downloader;
using System.IO;
using HandyControl.Tools;
using System.ComponentModel;
using System.Windows.Controls;
using HandyControl.Data;
using HandyControl.Tools.Extension;
using System.Diagnostics;
using static HandySub.Assets.Helper;
using System.IO.Compression;

namespace HandySub.Views
{
    /// <summary>
    /// Interaction logic for WorldSubtitleDownload.xaml
    /// </summary>
    public partial class WorldSubtitleDownload : Page
    {
        ObservableCollection<DownloadModel> DataList = new ObservableCollection<DownloadModel>();
        private string location = string.Empty;
        private string subtitleUrl = string.Empty;
        public WorldSubtitleDownload()
        {
            InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var link = e?.Parameter()?.ToString();
            if (!string.IsNullOrEmpty(link))
                subtitleUrl = link;

            if (!string.IsNullOrEmpty(subtitleUrl))
            {
                GetSubtitle();
            }
        }

        private async void GetSubtitle()
        {
            try
            {
                tgBlock.IsChecked = false;

                var web = new HtmlWeb();
                var doc = await web.LoadFromWebAsync(subtitleUrl);

                var items = doc.DocumentNode.SelectNodes(@"//div[@id='new-link']/ul/li");
                if (items == null)
                {
                    Growl.ErrorGlobal(LocalizationManager.LocalizeString("SubNotFound"));
                }
                else
                {
                    DataList?.Clear();
                    foreach (var node in items)
                    {
                        var displayName = node.SelectSingleNode(".//div[@class='new-link-1']").InnerText;
                        var status = node.SelectSingleNode(".//div[@class='new-link-2']").InnerText;
                        var link = node.SelectSingleNode(".//a")?.Attributes["href"]?.Value;

                        if (status.Contains("&nbsp;")) status = status.Replace("&nbsp;", "");

                        displayName = displayName.Trim() + " - " + status.Trim();

                        var item = new DownloadModel 
                        {
                            DisplayName = displayName,
                            DownloadLink = link
                        };

                        DataList.Add(item);
                    }

                    listView.ItemsSource = DataList;
                }

                tgBlock.IsChecked = true;
            }
            catch (ArgumentOutOfRangeException)
            {
            }
            catch (ArgumentNullException)
            {
            }
            catch (NullReferenceException)
            {
            }
            catch (WebException ex)
            {
                if (!string.IsNullOrEmpty(ex.Message))
                {
                    Growl.ErrorGlobal(LocalizationManager.LocalizeString("ServerNotFound") + "\n" + ex.Message);
                }
            }
            catch (HttpRequestException hx)
            {
                if (!string.IsNullOrEmpty(hx.Message))
                {
                    Growl.ErrorGlobal(LocalizationManager.LocalizeString("ServerNotFound") + "\n" + hx.Message);
                }
            }
            finally
            {
                tgBlock.IsChecked = true;
            }
        }
        private void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            GetSubtitle();
        }

        private async void btnDownload_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            try
            {
                tgBlock.IsChecked = false;
                prgStatus.Value = 0;
                prgStatus.IsIndeterminate = false;
                var btnDownload = sender as Button;
                var link = btnDownload.Tag.ToString();
                if (!string.IsNullOrEmpty(link))
                {
                    // if luanched from ContextMenu set location next to the movie file
                    if (!string.IsNullOrEmpty(App.StartUpArguments.Name))
                        location = App.StartUpArguments.Path;
                    else // get location from config
                        location = Settings.StoreLocation;
                    
                    if (!Settings.IsIDMEnabled)
                    {
                        var downloader = new DownloadService();
                        downloader.DownloadProgressChanged += Downloader_DownloadProgressChanged;
                        downloader.DownloadFileCompleted += Downloader_DownloadFileCompleted;
                        await downloader.DownloadFileTaskAsync(link, new DirectoryInfo(location));
                    }
                    else
                    {
                        tgBlock.IsChecked = true;
                        OpenLinkWithIDM(link, IDMNotFound);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                Growl.ErrorGlobal(LocalizationManager.LocalizeString("AdminError"));
            }
            catch (NotSupportedException)
            {
            }
            catch (ArgumentException)
            {
            }
            finally
            {
                tgBlock.IsChecked = true;
            }
        }

        private void IDMNotFound()
        {
            Growl.WarningGlobal(LocalizationManager.LocalizeString("IDMNot"));
        }
        private void Downloader_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                Growl.ErrorGlobal("Download Canceled");
            }
            else if (e.Error != null)
            {
                Growl.ErrorGlobal(e.Error.Message);
            }
            else
            {
                DispatcherHelper.RunOnMainThread(() =>
                {
                    tgBlock.IsChecked = true;
                    prgStatus.Value = 0;
                    prgStatus.IsIndeterminate = true;
                    txtStatus.Text = string.Empty;
                    var downloadedFileName = ((DownloadPackage)e.UserState).FileName;

                    if (Settings.IsShowNotification)
                    {
                        Growl.ClearGlobal();
                        Growl.AskGlobal(new GrowlInfo
                        {
                            CancelStr = LocalizationManager.LocalizeString("Cancel"),
                            ConfirmStr = LocalizationManager.LocalizeString("OpenFolder"),
                            Message = LocalizationManager.LocalizeString("FileDownloaded").Format(Path.GetFileNameWithoutExtension(downloadedFileName)),
                            ActionBeforeClose = b =>
                            {
                                if (!b) return true;

                                Process.Start("explorer.exe", "/select, \"" + downloadedFileName + "\"");
                                return true;
                            }
                        });
                    }

                    if (Settings.IsAutoUnZip)
                    {
                        if (Path.GetExtension(downloadedFileName).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            using (ZipArchive archive = ZipFile.OpenRead(downloadedFileName))
                            {
                                archive.ExtractToDirectory(Path.GetDirectoryName(downloadedFileName), true);
                            }
                            try
                            {
                                File.Delete(downloadedFileName);
                            }
                            catch (IOException)
                            {
                            }
                        }
                    }
                });
            }
        }

        private void Downloader_DownloadProgressChanged(object sender, Downloader.DownloadProgressChangedEventArgs e)
        {
            DispatcherHelper.RunOnMainThread(() => {
                prgStatus.Value = e.ProgressPercentage;
                txtStatus.Text = $"{(int)e.ProgressPercentage}%";
            });
        }
    }
}
