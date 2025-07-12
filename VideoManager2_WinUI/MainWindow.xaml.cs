using Microsoft.UI.Xaml;

namespace VideoManager2_WinUI
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            this.InitializeComponent();
            ViewModel = new MainViewModel();
            ViewModel.Initialize(this);
        }
    }
}
