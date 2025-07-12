/* MainViewModel.cs */
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

        // コマンド名をより実態に合わせた名前に変更することも検討できますが、
        // 今回はロジックの修正に留めます。
        public ICommand SelectFolderCommand { get; }

        private IntPtr _hWnd;

        public MainViewModel()
        {
            _databaseService = new DatabaseService();
            _settingsService = new SettingsService();
            // メソッド名を変更していませんが、実質的な動作は「フォルダをライブラリに追加」になります。
            SelectFolderCommand = new AsyncRelayCommand(SelectAndAddFolderAsync);
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
            // lastSourcePathの有無で、一度でもライブラリが作成されたかを判断します。
            if (!string.IsNullOrEmpty(lastSourcePath))
            {
                System.Diagnostics.Debug.WriteLine("Validating library...");
                await _databaseService.ValidateLibraryAsync();
                System.Diagnostics.Debug.WriteLine("Validation complete.");

                await LoadLibraryFromDbAsync();
                System.Diagnostics.Debug.WriteLine($"Loaded last library. Primary source: {lastSourcePath}");
            }
        }

        // メソッド名を SelectNewLibraryAsync から SelectAndAddFolderAsync に変更しました。
        private async Task SelectAndAddFolderAsync()
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
                // ★ 修正点: データベースをクリアする処理を削除
                // これにより、既存のライブラリにアイテムが追加されるようになります。
                // await _databaseService.ClearLibraryDataAsync();

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
                        fileSize: 0,
                        basicProperties.DateModified,
                        duration: TimeSpan.Zero
                    );
                    itemsToSave.Add(folderItem);
                }

                // 2. 直下の動画ファイルをスキャンしてリストに追加
                var videoExtensions = new[] { ".mp4", ".wmv", ".mov", ".mkv", ".avi" };
                var files = await folder.GetFilesAsync();
                foreach (var file in files)
                {
                    if (videoExtensions.Contains(Path.GetExtension(file.Name).ToLowerInvariant()))
                    {
                        BasicProperties basicProperties = await file.GetBasicPropertiesAsync();
                        VideoProperties videoProperties = await file.Properties.GetVideoPropertiesAsync();
                        var videoItem = new VideoItem(
                            file.Path,
                            file.DisplayName,
                            isFolder: false,
                            basicProperties.Size,
                            basicProperties.DateModified,
                            videoProperties.Duration
                        );
                        itemsToSave.Add(videoItem);
                    }
                }

                if (itemsToSave.Any())
                {
                    // DatabaseService側のINSERT OR IGNOREにより、重複するアイテムは追加されません。
                    await _databaseService.AddOrUpdateFilesAsync(itemsToSave);
                }

                // DBから再読み込みしてUIを更新します。
                await LoadLibraryFromDbAsync();

                // ★ 修正点: 最初にライブラリを作成したときのみ、ソースフォルダのパスを保存する
                // これにより、2つ目以降のフォルダを追加しても、この設定が上書きされるのを防ぎます。
                if (string.IsNullOrEmpty(_settingsService.LastLibrarySourceFolderPath))
                {
                    _settingsService.LastLibrarySourceFolderPath = folder.Path;
                    System.Diagnostics.Debug.WriteLine($"Primary library source folder set to: {folder.Path}");
                }
            }
        }

        private async Task LoadLibraryFromDbAsync()
        {
            var itemsFromDb = await _databaseService.GetFilesAsync();

            VideoItems.Clear();
            foreach (var item in itemsFromDb)
            {
                VideoItems.Add(item);
                // サムネイルなどの詳細情報は非同期で読み込みます。
                _ = item.LoadDetailsAsync();
            }
            System.Diagnostics.Debug.WriteLine($"{itemsFromDb.Count} items loaded from DB.");
        }
    }
}
