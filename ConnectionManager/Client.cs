using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConnectionManager
{
    public class Client : INotifyPropertyChanged
    {

        private bool _isListener;
        public bool IsListener
        {
            get { return _isListener; }
            set { _isListener = value; OnPropertyChanged("IsListener"); }
        }

        private string _machineName;
        public string MachineName
        {
            get { return _machineName; }
            set { _machineName = value; OnPropertyChanged("MachineName"); }
        }

        public Client(bool bIsListener, string strMacineName)
        {
            IsListener = bIsListener;
            MachineName = strMacineName;
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
