using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.UI.Popups;
using ChichiYTS.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nikse.SubtitleEdit.Core;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding;

namespace ChichiYTS.Helpers
{
    internal class YtsServerHelper
    {
        /*public const string InitialSubtitleFileName = "subtitle.txt";*/
        private const string SubtitleFolderName = "subtitles";
        private const string RequestFolderName = "requests";
        private const string ResponseFolderName = "responses";
        public const string MovieFolderName = "movie";
        private const string ErrorMessage = "something went wrong, please try again later";
        private static StorageFolder __subtitleFolder;
        private static volatile bool __alreadyStarted;
        private static readonly string __requestFile;
        private static readonly string __movieInputFile;
        private static readonly string __movieOutputFile;

        static YtsServerHelper()
        {
            __requestFile = $"{ApplicationData.Current.LocalFolder.Path}\\{RequestFolderName}\\request.qza";
            __movieInputFile = $"{ApplicationData.Current.LocalFolder.Path}\\{MovieFolderName}\\movie_input.qza";
            __movieOutputFile = $"{ApplicationData.Current.LocalFolder.Path}\\{MovieFolderName}\\movie_output.qza";
        }

        #region server

        public static async Task StartServerAsync()
        {
            if (ApiInformation.IsApiContractPresent("Windows.ApplicationModel.FullTrustAppContract", 1, 0))
            {
                // setup subtitle folder
                await SafeDeleteAsync(SubtitleFolderName);
                await SafeDeleteAsync(RequestFolderName);
                await SafeDeleteAsync(ResponseFolderName);
                await SafeDeleteAsync(MovieFolderName);

                __subtitleFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(SubtitleFolderName);
                await ApplicationData.Current.LocalFolder.CreateFolderAsync(RequestFolderName);
                await ApplicationData.Current.LocalFolder.CreateFolderAsync(ResponseFolderName);
                await ApplicationData.Current.LocalFolder.CreateFolderAsync(MovieFolderName, CreationCollisionOption.OpenIfExists);

                ApplicationData.Current.LocalSettings.Values["parameters"] =
                    $"{Process.GetCurrentProcess().Id} \"{ApplicationData.Current.LocalFolder.Path}\"";
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                __alreadyStarted = true;
            }
            else
            {
                await new MessageDialog("Sorry, this app is not compatible with this Windows version.").ShowAsync();
            }
        }

        public static async Task RegisterAsync(string link)
        {
            // await ForceDeleteAsync(__movieInputFile);

            try
            {
                if (File.Exists(__movieInputFile))
                {
                    var input = JObject.Parse(File.ReadAllText(__movieInputFile));
                    if (input.Value<string>("link") != link)
                    {
                        await ForceDeleteAsync(__movieOutputFile);
                    }
                }

                var request = JsonConvert.SerializeObject(new
                {
                    link = link,
                    position = 0
                });
                
                await File.WriteAllTextAsync(__movieInputFile, request);
                
                while (!File.Exists(__movieOutputFile))
                {
                    await Task.Delay(200);
                }
            }
            catch (Exception e)
            {
                // var message = JsonConvert.SerializeObject(e);
                await new MessageDialog(ErrorMessage, "error").ShowAsync();
            }
        }

        public static void SetPosition(ulong position)
        {
            try
            {
                var request = JObject.Parse(File.ReadAllText(__movieInputFile));
                request["position"] = position;
                File.WriteAllText(__movieInputFile, request.ToString());
            }
            catch (Exception e)
            {
                // var message = JsonConvert.SerializeObject(e);
                var message = e.Message;
            }
        }

        public static async Task UnregisterAsync()
        {
            var content = new JObject
            {
                {"type", "unregister"}
            };

            await RequestAsync<string>(content, null);
        }

        #endregion

        #region subtitle

        public static async Task<SubtitleData> GetSubtitleDataAsync(string title, int year, int limit = 10)
        {
            var outFile = $"subtitle_data_{CreateMd5(title + year)}.qza";
            var content = new JObject
            {
                {"type", "subtitle_data"},
                {"title", title},
                {"year", year},
                {"limit", limit}
            };

            return await RequestAsync<SubtitleData>(content, outFile);
        }

        public static async Task<Dictionary<string, Dictionary<string, string>>> GetSubtitlesAsync(string link, int limit = 10)
        {
            var outFile = $"subtitles_{CreateMd5(link)}.qza";
            var content = new JObject
            {
                {"type", "subtitles"},
                {"link", link},
                {"limit", limit}
            };

            return await RequestAsync<Dictionary<string, Dictionary<string, string>>>(content, outFile);
        }

        public static async Task<Uri> CreateSubtitleUri(string link)
        {
            try
            {
                var outFile = $"subtitle_{CreateMd5(link)}.txt";
                var content = new JObject
                {
                    {"type", "subtitle"},
                    {"link", link}
                };

                await RequestAsync<string>(content, outFile, 200, false);
                var responseFile = $"{ApplicationData.Current.LocalFolder.Path}\\{ResponseFolderName}\\{outFile}";
                var subtitle = new Subtitle();
                subtitle.LoadSubtitle(responseFile, out _, null);
                var format = new SubStationAlpha();
                //var format = new SubRip();
                var ssa = subtitle.ToText(format);
                var file = await __subtitleFolder.CreateFileAsync($"{Guid.NewGuid()}{format.Extension}");
                await FileIO.WriteTextAsync(file, ssa, UnicodeEncoding.Utf8);
                return new Uri(file.Path);
            }
            catch (Exception e)
            {
                return null;
            }
        }

        #endregion

        #region movie

        /*public static Uri GetStreamUri()
        {
            //return new Uri("http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4");
            return new Uri($"{__serverEndpoint}/stream");
        }*/

        public static async Task<List<MovieItem>> GetMoviesAsync(YtsRequest request)
        {
            while (!__alreadyStarted)
            {
                await Task.Delay(100);
            }

            var query = request.ToQueryString();
            var outFile = $"movies_{CreateMd5(query)}.qza";
            var content = new JObject
            {
                {"type", "movies"},
                {"query", query}
            };

            return await RequestAsync<List<MovieItem>>(content, outFile);
        }

        #endregion

        #region helper

        private static async Task SafeDeleteAsync(string folder, StorageDeleteOption option = StorageDeleteOption.PermanentDelete)
        {
            try
            {
                var f = await ApplicationData.Current.LocalFolder.GetFolderAsync(folder);
                await f.DeleteAsync(option);
            }
            catch (Exception e)
            {
                var s = e.Message;
            }
        }

        private static async Task ForceDeleteAsync(string filePath)
        {
            while (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (IOException)
                {
                    await Task.Delay(100);
                }
            }
        }

        private static async Task<T> RequestAsync<T>(JObject content, string outFile, int delayMili = 100, bool deserializeResponse = true)
        {
            if (!string.IsNullOrEmpty(outFile))
                content["out"] = outFile;
            
            try
            {
                while (true)
                {
                    try
                    {
                        await File.WriteAllTextAsync(__requestFile, content.ToString());
                        break;
                    }
                    catch (IOException)
                    {
                        await Task.Delay(50);
                    }
                }

                if (string.IsNullOrEmpty(outFile))
                    return default;
                
                var responseFile = $"{ApplicationData.Current.LocalFolder.Path}\\{ResponseFolderName}\\{outFile}";
                while (!File.Exists(responseFile))
                {
                    await Task.Delay(delayMili);
                }

                if (!deserializeResponse)
                    return default;
                
                string json;
                while (true)
                {
                    try
                    {
                        json = await File.ReadAllTextAsync(responseFile);
                        break;
                    }
                    catch (IOException)
                    {
                        await Task.Delay(50);
                    }
                }

                if (string.IsNullOrEmpty(json) || json == "null")
                    return default;

                if (!json.StartsWith("{") && !json.StartsWith("[")) //error
                {
                    await new MessageDialog(ErrorMessage, "error").ShowAsync();
                    return default;
                }

                return JsonConvert.DeserializeObject<T>(json);
            }
            catch (Exception e)
            {
                // var message = JsonConvert.SerializeObject(e);
                var message = e.Message;
                await new MessageDialog(ErrorMessage, "error").ShowAsync();
                return default;
            }
        }

        private static string CreateMd5(string input)
        {
            // Use input string to calculate MD5 hash
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                var inputBytes = Encoding.ASCII.GetBytes(input);
                var hashBytes = md5.ComputeHash(inputBytes);

                // Convert the byte array to hexadecimal string
                var sb = new StringBuilder();
                foreach (var b in hashBytes)
                {
                    sb.Append(b.ToString("x2"));
                }

                return sb.ToString();
            }
        }

        #endregion
    }
}