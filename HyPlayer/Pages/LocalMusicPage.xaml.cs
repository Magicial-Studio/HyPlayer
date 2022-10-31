﻿#region

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using HyPlayer.Classes;
using HyPlayer.HyPlayControl;

#endregion

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace HyPlayer.Pages;

/// <summary>
///     可用于自身或导航至 Frame 内部的空白页。
/// </summary>
public sealed partial class LocalMusicPage : Page, INotifyPropertyChanged
{
    private static readonly string[] supportedFormats = { ".flac", ".mp3", ".ncm", ".ape", ".m4a", ".wav" };
    private ObservableCollection<HyPlayItem> localHyItems;
    private string _notificationText;
    private Task CurrentLocalFileScanTask;
    private int index;

    public delegate void CurrentLocalFileScanTaskFinished();
    public event CurrentLocalFileScanTaskFinished OnCurrentLocalFileScanTaskFinished;

    public LocalMusicPage()
    {
        InitializeComponent();
        localHyItems = new ObservableCollection<HyPlayItem>();
        OnCurrentLocalFileScanTaskFinished += LocalMusicPage_OnCurrentLocalFileScanTaskFinished;
    }

    private void LocalMusicPage_OnCurrentLocalFileScanTaskFinished()
    {
        CurrentLocalFileScanTask.Dispose();
        CurrentLocalFileScanTask = null;
    }

    public string NotificationText
    {
        get => _notificationText;
        set
        {
            _notificationText = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        localHyItems.Clear();
        localHyItems = null;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        DownloadPageFrame.Navigate(typeof(DownloadPage));
    }

    private void Playall_Click(object sender, RoutedEventArgs e)
    {
        HyPlayList.RemoveAllSong();
        HyPlayList.List.AddRange(localHyItems);
        HyPlayList.SongAppendDone();
        HyPlayList.SongMoveTo(0);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentLocalFileScanTask != null) return;
        index = 0;
        CurrentLocalFileScanTask = LoadLocalMusic(); 
    }

    private async Task LoadLocalMusic()
    {
        ListBoxLocalMusicContainer.SelectionChanged -= ListBoxLocalMusicContainer_SelectionChanged;
        localHyItems.Clear();
        NotificationText = "正在扫描...";
        var folder = !string.IsNullOrEmpty(Common.Setting.searchingDir)
            ? await StorageFolder.GetFolderFromPathAsync(Common.Setting.searchingDir)
            : KnownFolders.MusicLibrary;
        // Use Query to boost? maybe?
        FileLoadingIndicateRing.Visibility = Visibility.Visible;
        FileLoadingIndicateRing.IsActive = true;
        var queryOptions = new QueryOptions(CommonFileQuery.DefaultQuery, supportedFormats);
        queryOptions.FolderDepth = FolderDepth.Deep;
        var files = await folder.CreateFileQueryWithOptions(queryOptions).GetFilesAsync();

        if (!Common.Setting.localProgressiveLoad)
        {
            foreach (var storageFile in files)
                try
                {
                    var item = await HyPlayList.LoadStorageFile(storageFile);
                    localHyItems.Add(item);
                }
                catch
                {
                    //ignore
                }
        }
        else
        {
            var undeterminedAlbum = new NCAlbum
            {
                AlbumType = HyPlayItemType.LocalProgressive,
                name = "未知专辑 - 播放后加载"
            };
            var undeterminedArtistList = new List<NCArtist>
            {
                new()
                {
                    name = "未知歌手 - 播放后加载",
                    Type = HyPlayItemType.LocalProgressive
                }
            };
            foreach (var storageFile in files)
                localHyItems.Add(new HyPlayItem
                {
                    ItemType = HyPlayItemType.LocalProgressive,
                    PlayItem = new PlayItem
                    {
                        Album = undeterminedAlbum,
                        Artist = undeterminedArtistList,
                        Bitrate = 0,
                        DontSetLocalStorageFile = storageFile,
                        IsLocalFile = true,
                        LengthInMilliseconds = 0,
                        Name = storageFile.Name,
                        CDName = "01",
                        Size = null,
                        SubExt = storageFile.FileType,
                        TrackId = 0,
                        Tag = "本地歌曲",
                        Type = HyPlayItemType.LocalProgressive,
                        Url = storageFile.Path
                    }
                });
        }
        NotificationText = "扫描完成, 共 " + files.Count + " 首音乐";
        FileLoadingIndicateRing.IsActive = false;
        FileLoadingIndicateRing.Visibility = Visibility.Collapsed;
        ListBoxLocalMusicContainer.SelectionChanged += ListBoxLocalMusicContainer_SelectionChanged;
        OnCurrentLocalFileScanTaskFinished.Invoke();
    }


    private void ListBoxLocalMusicContainer_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ListBoxLocalMusicContainer.SelectedIndex == -1) return;
        HyPlayList.RemoveAllSong();
        HyPlayList.List.AddRange(localHyItems);
        HyPlayList.SongAppendDone();
        HyPlayList.SongMoveTo(ListBoxLocalMusicContainer.SelectedIndex);
    }

    private async void UploadCloud_Click(object sender, RoutedEventArgs e)
    {
        var sf = await StorageFile.GetFileFromPathAsync((sender as Button).Tag as string);
        await CloudUpload.UploadMusic(sf);
    }

    private void Add_Local(object sender, RoutedEventArgs e)
    {
        _ = HyPlayList.PickLocalFile();
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}