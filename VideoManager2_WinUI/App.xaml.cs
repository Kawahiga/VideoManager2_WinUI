using Microsoft.UI.Xaml;

namespace VideoManager2_WinUI
{
    public partial class App : Application
    {
        private Window m_window;

        public App()
        {
            this.InitializeComponent();

            // SQLiteのネイティブライブラリを初期化するためにこの行を追加します。
            SQLitePCL.Batteries.Init();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
        }
    }
}
