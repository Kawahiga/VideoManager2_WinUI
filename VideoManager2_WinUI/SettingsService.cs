using Windows.Storage;

namespace VideoManager2_WinUI
{
    /// <summary>
    /// アプリケーションの設定を管理するサービスクラス
    /// </summary>
    public class SettingsService
    {
        // 保存するキーを、DBパスからライブラリの元フォルダパスに変更
        private const string LastLibrarySourceFolderPathKey = "LastLibrarySourceFolderPath";
        private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;

        /// <summary>
        /// 最後に開いたライブラリのソースフォルダパスを取得または設定する
        /// </summary>
        public string? LastLibrarySourceFolderPath
        {
            get => _localSettings.Values[LastLibrarySourceFolderPathKey] as string;
            set => _localSettings.Values[LastLibrarySourceFolderPathKey] = value;
        }
    }
}