using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;

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

        private void TreeView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var originalSource = e.OriginalSource as FrameworkElement;
            if (originalSource == null) return;

            if (originalSource.DataContext is Tag clickedTag)
            {
                // ★★★ エラー修正ポイント 3: this.Resources から TagsTreeView.Resources に変更 ★★★
                // TreeViewに付けた名前を使って、そのリソースにアクセスします。
                var flyout = this.TagsTreeView.Resources["TagItemMenuFlyout"] as MenuFlyout;
                if (flyout == null) return;

                var addFlyoutItem = flyout.Items[0] as MenuFlyoutItem;
                if (addFlyoutItem != null) addFlyoutItem.CommandParameter = clickedTag;

                var renameFlyoutItem = flyout.Items[1] as MenuFlyoutItem;
                if (renameFlyoutItem != null) renameFlyoutItem.CommandParameter = clickedTag;

                var deleteFlyoutItem = flyout.Items[3] as MenuFlyoutItem;
                if (deleteFlyoutItem != null) deleteFlyoutItem.CommandParameter = clickedTag;

                FlyoutBase.ShowAttachedFlyout(originalSource);
            }
        }
    }
}