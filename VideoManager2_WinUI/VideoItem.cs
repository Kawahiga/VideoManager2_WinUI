using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;

namespace VideoManager2_WinUI
{
    public class VideoItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            if (_dispatcherQueue.HasThreadAccess)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
            else
            {
                _dispatcherQueue?.TryEnqueue(() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)));
            }
        }

        public int Id { get; set; }
        public string FilePath { get; }
        public string FileName { get; }
        public ulong FileSize { get; }
        public DateTimeOffset DateModified { get; }
        public TimeSpan Duration { get; }
        public ObservableCollection<Tag> Tags { get; } = new ObservableCollection<Tag>();

        private BitmapImage? _thumbnail;
        public BitmapImage? Thumbnail
        {
            get => _thumbnail;
            private set
            {
                _thumbnail = value;
                OnPropertyChanged();
            }
        }

        private readonly DispatcherQueue _dispatcherQueue;

        public VideoItem(string filePath, string fileName, ulong fileSize, DateTimeOffset dateModified, TimeSpan duration)
        {
            FilePath = filePath;
            FileName = fileName;
            FileSize = fileSize;
            DateModified = dateModified;
            Duration = duration;
            
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        }

        public async Task LoadDetailsAsync()
        {
            await LoadThumbnailAsync();
        }

        private async Task LoadThumbnailAsync()
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(FilePath);
                const ThumbnailMode mode = ThumbnailMode.VideosView;
                const uint requestedSize = 200;

                using (var thumbnailStream = await file.GetThumbnailAsync(mode, requestedSize, ThumbnailOptions.UseCurrentScale))
                {
                    if (thumbnailStream != null)
                    {
                        var memoryStream = new InMemoryRandomAccessStream();
                        await RandomAccessStream.CopyAsync(thumbnailStream, memoryStream);
                        memoryStream.Seek(0);

                        _dispatcherQueue.TryEnqueue(async () =>
                        {
                            try
                            {
                                var bitmapImage = new BitmapImage();
                                await bitmapImage.SetSourceAsync(memoryStream);
                                Thumbnail = bitmapImage;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error setting thumbnail source for {FileName}: {ex.Message}");
                            }
                            finally
                            {
                                memoryStream.Dispose();
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load thumbnail for {FileName}: {ex.Message}");
            }
        }
    }
}
