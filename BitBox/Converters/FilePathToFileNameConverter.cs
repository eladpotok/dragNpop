using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace BitBox.Converters
{
    class FilePathToFileNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            string strValue = value.ToString();

            return Path.GetFileNameWithoutExtension(strValue);

        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            string strValue = value.ToString();

            return Path.GetFileName(strValue);
        }
    }
}
