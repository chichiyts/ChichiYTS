using System;

namespace ChichiYTS.Helpers
{
    internal class Utils
    {
        public static string HumanReadableByteCount(long bytes, bool si)
        {
            var unit = si ? 1000 : 1024;
            if (bytes < unit) return bytes + " B";
            var exp = (int)(Math.Log(bytes) / Math.Log(unit));
            var pre = (si ? "kMGTPE" : "KMGTPE")[exp - 1] + (si ? "" : "i");
            return $"{bytes / Math.Pow(unit, exp):N1} {pre}B";
        }
    }
}
