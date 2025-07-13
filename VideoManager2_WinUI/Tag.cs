using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VideoManager2_WinUI
{
    public class Tag : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private int _id;
        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        private string _name;
        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        // ★ DBとの親和性を高めるため、Colorの型をstringに変更
        //    ViewModel側でBrushに変換します。
        private string? _color;
        public string? Color
        {
            get => _color;
            set { _color = value; OnPropertyChanged(); }
        }

        public int? ParentId { get; set; }
        public ObservableCollection<Tag> Children { get; set; } = new ObservableCollection<Tag>();

        // ★ コンストラクタをシンプルに
        public Tag(string name)
        {
            _name = name;
        }
    }
}