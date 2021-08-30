using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;

namespace ChichiYTS.Converters
{
    public class VisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value == null || value.Equals("") || value.Equals(0) || value.Equals(0.0) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}