using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace VideoManager2_WinUI
{
    public class MainViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private readonly DatabaseService _databaseService;
        private readonly SettingsService _settingsService;

        public ObservableCollection<VideoItem> VideoItems { get; } = new ObservableCollection<VideoItem>();
        public ICommand SelectFolderCommand { get; }

        private IntPtr _hWnd;

        public MainViewModel()
        {
            _databaseService = new DatabaseService();
            _settingsService = new SettingsService();
            SelectFolderCommand = new AsyncRelayCommand(SelectNewLibraryAsync);
        }
        
        public async void Initialize(object window)
        {
            _hWnd = WindowNative.GetWindowHandle(window);
            
            // DBファイル名を "library.db" に固定し、接続を確立する
            var localFolder = ApplicationData.Current.LocalFolder.Path;
            var dbPath = Path.Combine(localFolder, "library.db");
            await _databaseService.ConnectAsync(dbPath);

            // アプリ起動時に最後のライブラリを読み込む
            await LoadLastLibraryAsync();
        }

        /// <summary>
        /// 最後に使用したライブラリの情報をDBから読み込む
        /// </summary>
        private async Task LoadLastLibraryAsync()
        {
            var lastSourcePath = _settingsService.LastLibrarySourceFolderPath;
            // ソースフォルダのパスが保存されていれば、DBからデータを読み込む
            if (!string.IsNullOrEmpty(lastSourcePath))
            {
                // UIに表示を反映
                await LoadLibraryFromDbAsync();
                System.Diagnostics.Debug.WriteLine($"Loaded last library from source: {lastSourcePath}");
            }
        }

        /// <summary>
        /// 新しいフォルダを選択してライブラリとして設定する
        /// </summary>
        private async Task SelectNewLibraryAsync()
        {
            var folderPicker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.VideosLibrary
            };
            folderPicker.FileTypeFilter.Add("*");
            InitializeWithWindow.Initialize(folderPicker, _hWnd);

            StorageFolder? folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                // 1. 既存のライブラリ情報（ファイルとタグのマッピング）をDBからクリア
                await _databaseService.ClearLibraryDataAsync();
                
                // 2. フォルダをスキャンしてファイル情報を収集
                var videoItemsToSave = new List<VideoItem>();
                var videoExtensions = new[] { ".mp4", ".wmv", ".mov", ".mkv", ".avi" };
                var files = await folder.GetFilesAsync(Windows.Storage.Search.CommonFileQuery.OrderByName);

                foreach (var file in files)
                {
                    if (videoExtensions.Contains(Path.GetExtension(file.Name).ToLowerInvariant()))
                    {
                        BasicProperties basicProperties = await file.GetBasicPropertiesAsync();
                        VideoProperties videoProperties = await file.Properties.GetVideoPropertiesAsync();
                        var videoItem = new VideoItem(file.Path, file.DisplayName, basicProperties.Size, basicProperties.DateModified, videoProperties.Duration);
                        videoItemsToSave.Add(videoItem);
                    }
                }

                // 3. 収集したファイル情報をデータベースに保存
                if (videoItemsToSave.Any())
                {
                    await _databaseService.AddOrUpdateFilesAsync(videoItemsToSave);
                }
                
                // 4. 新しいライブラリ情報をDBから読み込んでUIに表示
                await LoadLibraryFromDbAsync();

                // 5. 新しいライブラリのソースフォルダパスを保存
                _settingsService.LastLibrarySourceFolderPath = folder.Path;
            }
        }

        /// <summary>
        /// 現在のデータベースからライブラリを読み込み、UIに表示する
        /// </summary>
        private async Task LoadLibraryFromDbAsync()
        {
            var itemsFromDb = await _databaseService.GetFilesAsync();
            
            VideoItems.Clear();
            foreach (var item in itemsFromDb)
            {
                VideoItems.Add(item);
                _ = item.LoadDetailsAsync(); // サムネイル読み込み
            }
            System.Diagnostics.Debug.WriteLine($"{itemsFromDb.Count} items loaded from DB.");
        }
    }
}
