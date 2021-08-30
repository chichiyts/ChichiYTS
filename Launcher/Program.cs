using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Launcher
{
    class Program
    {
        static void Main(string[] args)
        {
            // determine the package root, based on own location
            var result = Assembly.GetExecutingAssembly().Location;
            var index = result.LastIndexOf("\\", StringComparison.Ordinal);
            var rootPath = $"{result.Substring(0, index)}\\..";
            var pathExe = Path.GetFullPath($"{rootPath}\\server.exe");

            var launchers = Process.GetProcessesByName("python");
            var alreadyRun = launchers.Any(l =>
            {
                try
                {
                    return Path.GetFullPath(l?.MainModule?.FileName ?? ".") == pathExe;
                }
                catch // not have privilege
                {
                    return false;
                }
            });

            if (!alreadyRun)
            {
                var arguments = SettingHelper.LocalGet<string>("parameters");
                // Start server
                Process.Start(new ProcessStartInfo
                {
                    FileName = pathExe,
                    Arguments = arguments,
                    /*RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,*/
                    CreateNoWindow = true
                });
                //Process.Start(pathExe, arguments);
            }
        }
    }
}
