using System;
using Windows.Storage;

namespace ChichiYTS.Helpers
{
    internal class SettingHelper
    {
        public static void SaveMediaPosition(int id, double lastPosition, double mediaDuration)
        {
            if (lastPosition > 0 && mediaDuration > 0)
            {
                var settings = ApplicationData.Current.RoamingSettings;
                var composite = new ApplicationDataCompositeValue
                {
                    ["pos"] = lastPosition,
                    ["dur"] = mediaDuration
                };
                settings.Values[id + ""] = composite;
            }
        }

        public static Tuple<double, double> LoadMediaPosition(int id)
        {
            var settings = ApplicationData.Current.RoamingSettings;
            var composite = (ApplicationDataCompositeValue)settings.Values[id + ""];
            return composite == null ? null : new Tuple<double, double>((double) composite["pos"], (double) composite["dur"]);
        }

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