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
                System.Diagnostics.Debug.WriteLine("Validating library...");
                await _databaseService.ValidateLibraryAsync();
                System.Diagnostics.Debug.WriteLine("Validation complete.");

                await LoadLibraryFromDbAsync();
                System.Diagnostics.Debug.WriteLine($"Loaded last library from source: {lastSourcePath}");
            }
        }

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
                await _databaseService.ClearLibraryDataAsync();
                
                var itemsToSave = new List<VideoItem>();
                
                // 1. 直下のサブフォルダをスキャンしてリストに追加
                var subFolders = await folder.GetFoldersAsync();
                foreach (var subFolder in subFolders)
                {
                    BasicProperties basicProperties = await subFolder.GetBasicPropertiesAsync();
                    var folderItem = new VideoItem(
                        subFolder.Path,
                        subFolder.Name,
                        isFolder: true,
                        fileSize: 0, // フォルダのサイズは0として扱う
                        basicProperties.DateModified,
                        duration: TimeSpan.Zero
                    );
                    itemsToSave.Add(folderItem);
                }

                // ★★★ 修正点 ★★★
                // 2. 直下の動画ファイルをスキャンしてリストに追加する処理を復活させました。
                var videoExtensions = new[] { ".mp4", ".wmv", ".mov", ".mkv", ".avi" };
                var files = await folder.GetFilesAsync(); // CommonFileQueryは使用せず、直下のファイルのみ取得
                foreach (var file in files)
                {
                    if (videoExtensions.Contains(Path.GetExtension(file.Name).ToLowerInvariant()))
                    {
                        BasicProperties basicProperties = await file.GetBasicPropertiesAsync();
                        VideoProperties videoProperties = await file.Properties.GetVideoPropertiesAsync();
                        var videoItem = new VideoItem(
                            file.Path,
                            file.DisplayName,
                            isFolder: false, // ファイルなのでfalse
                            basicProperties.Size,
                            basicProperties.DateModified,
                            videoProperties.Duration
                        );
                        itemsToSave.Add(videoItem);
                    }
                }

                if (itemsToSave.Any())
                {
                    await _databaseService.AddOrUpdateFilesAsync(itemsToSave);
                }
                
                await LoadLibraryFromDbAsync();

                _settingsService.LastLibrarySourceFolderPath = folder.Path;
            }
        }

        private async Task LoadLibraryFromDbAsync()
        {
            var itemsFromDb = await _databaseService.GetFilesAsync();
            
            VideoItems.Clear();
            foreach (var item in itemsFromDb)
            {
                VideoItems.Add(item);
                _ = item.LoadDetailsAsync();
            }
            System.Diagnostics.Debug.WriteLine($"{itemsFromDb.Count} items loaded from DB.");
        }
    }
}