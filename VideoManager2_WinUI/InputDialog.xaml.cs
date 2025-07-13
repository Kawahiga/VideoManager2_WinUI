using Microsoft.UI.Xaml.Controls;

namespace VideoManager2_WinUI
{
    public sealed partial class InputDialog : ContentDialog
    {
        // ★★★ エラー修正ポイント ★★★
        // プロパティを宣言時に初期化して、コンパイラの警告を解決する
        public string InputText { get; private set; } = "";

        public InputDialog()
        {
            this.InitializeComponent();
        }
        
        public InputDialog(string title, string defaultText = "")
        {
            this.InitializeComponent();
            this.Title = title;
            this.InputTextBox.Text = defaultText;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            InputText = InputTextBox.Text;
        }

        private void ContentDialog_CloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // InputText は初期値 "" のまま
        }
    }
}