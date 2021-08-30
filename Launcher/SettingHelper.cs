using Windows.Storage;

namespace Launcher
{
    internal class SettingHelper
    {
        public static T LocalGet<T>(string key)
        {
            var settings = ApplicationData.Current.LocalSettings;
            if (settings.Values.TryGetValue(key, out var value))
            {
                return (T) value;
            }

            return default;
        }

        public static void LocalSet<T>(string key, T value)
        {
            var settings = ApplicationData.Current.LocalSettings;
            settings.Values[key] = value;
        }
    }
}