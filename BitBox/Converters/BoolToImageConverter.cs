using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Media;

namespace BitBox.Converters
{
    class BoolToImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string strResult = "";

            if (value == null)
                return strResult;

            if(!(value is bool))
                return strResult;

            if (parameter == null)
                return strResult;

            string strParameter = parameter.ToString();
            bool bValue = (bool)value;

            if (strParameter == "Status")
            {
                if (bValue)
                {
                    return "Resources/Online.ico";
                }

                return "Resources/Listen.ico";
            }
            else if (strParameter == "Client")
            {
                if (bValue)
                {
                    return "Resources/Client.png";
                }

                return "Resources/Transmitter.ico";
            }
            else if (strParameter == "ClientStatus")
            {
                if (bValue)
                {
                    return "Resources/Available.ico";
                }

                return "Resources/Away.ico";
            }

            return strResult;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
