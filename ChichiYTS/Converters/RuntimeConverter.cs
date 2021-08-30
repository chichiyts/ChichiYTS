using System;
using Windows.UI.Xaml.Data;

namespace ChichiYTS.Converters
{
    public class RuntimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int runtime && runtime > 0)
            {
                var timespan = TimeSpan.FromMinutes(runtime);
                return $"{timespan.Hours}h{timespan.Minutes:D2}";
            }

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}