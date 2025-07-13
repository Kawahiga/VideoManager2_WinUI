using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace VideoManager2_WinUI
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly DatabaseService _databaseService;
        private readonly SettingsService _settingsService;

        public ObservableCollection<VideoItem> VideoItems { get; } = new ObservableCollection<VideoItem>();
        public ObservableCollection<Tag> Tags { get; } = new ObservableCollection<Tag>();

        private IntPtr _hWnd;
        private XamlRoot? _xamlRoot;

        public MainViewModel()
        {
            _databaseService = new DatabaseService();
            _settingsService = new SettingsService();
        }

        public async void Initialize(Window window)
        {
            _hWnd = WindowNative.GetWindowHandle(window);
            _xamlRoot = window.Content.XamlRoot;

            var localFolder = ApplicationData.Current.LocalFolder.Path;
            var dbPath = Path.Combine(localFolder, "library.db");
            await _databaseService.ConnectAsync(dbPath);

            await LoadLastLibraryAsync();
        }

        private async Task LoadLastLibraryAsync()
        {
            var lastSourcePath = _settingsService.LastLibrarySourceFolderPath;
            if (!string.IsNullOrEmpty(lastSourcePath))
            {
                await _databaseService.ValidateLibraryAsync();
                await LoadLibraryDataAsync();
            }
        }

        [RelayCommand]
        private async Task SelectAndAddFolderAsync()
        {
            var folderPicker = new FolderPicker { SuggestedStartLocation = PickerLocationId.VideosLibrary };
            folderPicker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(folderPicker, _hWnd);
            StorageFolder? folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                var itemsToSave = new List<VideoItem>();
                var subFolders = await folder.GetFoldersAsync();
                foreach (var subFolder in subFolders)
                {
                    BasicProperties basicProperties = await subFolder.GetBasicPropertiesAsync();
                    itemsToSave.Add(new VideoItem(subFolder.Path, subFolder.Name, true, basicProperties.Size, basicProperties.DateModified, TimeSpan.Zero));
                }
                var videoExtensions = new[] { ".mp4", ".wmv", ".mov", ".mkv", ".avi" };
                var files = await folder.GetFilesAsync();
                foreach (var file in files)
                {
                    if (videoExtensions.Contains(Path.GetExtension(file.Name).ToLowerInvariant()))
                    {
                        // ★★★ エラー修正ポイント 1: GetBasicPropertiesAsyncの呼び出しを修正 ★★★
                        // .Properties を経由せずに、fileオブジェクトから直接呼び出します。
                        BasicProperties basicProperties = await file.GetBasicPropertiesAsync();
                        VideoProperties videoProperties = await file.Properties.GetVideoPropertiesAsync();
                        itemsToSave.Add(new VideoItem(file.Path, file.DisplayName, false, basicProperties.Size, basicProperties.DateModified, videoProperties.Duration));
                    }
                }
                if (itemsToSave.Any()) { await _databaseService.AddOrUpdateFilesAsync(itemsToSave); }
                await LoadLibraryDataAsync();
                if (string.IsNullOrEmpty(_settingsService.LastLibrarySourceFolderPath))
                {
                    _settingsService.LastLibrarySourceFolderPath = folder.Path;
                }
            }
        }
        
        [RelayCommand]
        private async Task AddTag(Tag? parentTag)
        {
            if (_xamlRoot == null) return;
            var dialog = new InputDialog("新しいタグ/グループの名前") { XamlRoot = _xamlRoot };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(dialog.InputText))
            {
                await _databaseService.AddTagAsync(dialog.InputText, parentTag?.Id);
                await LoadTagsAsync();
            }
        }

        [RelayCommand]
        private async Task RenameTag(Tag? tagToRename)
        {
            if (tagToRename == null || _xamlRoot == null) return;
            var dialog = new InputDialog("新しい名前", tagToRename.Name) { XamlRoot = _xamlRoot };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(dialog.InputText))
            {
                tagToRename.Name = dialog.InputText;
                await _databaseService.UpdateTagAsync(tagToRename);
            }
        }

        [RelayCommand]
        private async Task DeleteTag(Tag? tagToDelete)
        {
            if (tagToDelete == null || _xamlRoot == null) return;
            var dialog = new ContentDialog
            {
                Title = "削除の確認",
                Content = $"タグ「{tagToDelete.Name}」を削除しますか？\nこの操作は元に戻せません。",
                PrimaryButtonText = "削除",
                CloseButtonText = "キャンセル",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = _xamlRoot
            };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await _databaseService.DeleteTagAsync(tagToDelete);
                await LoadTagsAsync();
            }
        }

        private async Task LoadLibraryDataAsync()
        {
            await LoadTagsAsync();
            await LoadFilesAsync();
        }

        private async Task LoadFilesAsync()
        {
            var itemsFromDb = await _databaseService.GetFilesAsync();
            VideoItems.Clear();
            foreach (var item in itemsFromDb)
            {
                VideoItems.Add(item);
                _ = item.LoadDetailsAsync();
            }
        }

        private async Task LoadTagsAsync()
        {
            var tagsFromDb = await _databaseService.GetTagsAsync();
            Tags.Clear();
            foreach (var tag in tagsFromDb)
            {
                Tags.Add(tag);
            }
        }
    }
}
