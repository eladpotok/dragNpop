using MusicLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BitBox
{
    /// <summary>
    /// Interaction logic for DirectoryDialog.xaml
    /// </summary>
    public partial class DirectoryDialog : Window, INotifyPropertyChanged
    {
        public string LibraryPath
        {
            get { return MusicManager.GetInstance().LibraryPath; }
            set { MusicManager.GetInstance().LibraryPath = value; OnPropertyChanged("LibraryPath"); }
        }

        public DirectoryDialog()
        {
            this.DataContext = this;

            InitializeComponent();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string properyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(properyName));
        }
    }
}
