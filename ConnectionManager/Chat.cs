using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectionManager
{
    public class Chat : INotifyPropertyChanged
    {
        private string _sender;
        public string Sender
        {
            get { return _sender; }
            set { _sender = value; OnPropertyChanged("Sender"); }
        }

        private string _text;
        public string Text
        {
            get { return _text; }
            set { _text = value; OnPropertyChanged("Text"); }
        }


        #region On Property Changed

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string properyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(properyName));
        }

        #endregion
    }
}
