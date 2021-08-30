using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Web;
using Windows.Foundation;
using Windows.UI.Xaml.Data;
using ChichiYTS.Helpers;
using Newtonsoft.Json;

namespace ChichiYTS.ViewModels
{
    public class SubtitleData
    {
        [JsonProperty("subtitles")] public Dictionary<string, Dictionary<string, string>> Subtitles { get; set; } // lang -> {link->user}
        [JsonProperty("titles")] public Dictionary<string, string> Titles { get; set; }
    }

    public class YtsRequest
    {
        public int Limit { get; set; }
        public int Page { get; set; }
        public int MinimumRating { get; set; }
        public string Quality { get; set; }
        public string QueryTerm { get; set; }
        public string Genre { get; set; }
        public string SortBy { get; set; }
        public string OrderBy { get; set; }
        public bool WithRtRatings { get; set; }

        public string ToQueryString()
        {
            var queries = new List<string>();
            if (Limit > 0)
                queries.Add("limit=" + Limit);
            if (Page > 0)
                queries.Add("page=" + Page);
            if (MinimumRating > 0)
                queries.Add("minimum_rating=" + MinimumRating);
            if (!string.IsNullOrEmpty(Quality))
                queries.Add("quality=" + Quality);
            if (!string.IsNullOrEmpty(QueryTerm))
                queries.Add("query_term=" + HttpUtility.UrlEncode(QueryTerm));
            if (!string.IsNullOrEmpty(Genre))
                queries.Add("genre=" + Genre);
            if (!string.IsNullOrEmpty(SortBy))
                queries.Add("sort_by=" + SortBy);
            if (!string.IsNullOrEmpty(OrderBy))
                queries.Add("order_by=" + OrderBy);
            if (WithRtRatings)
                queries.Add("with_rt_ratings=" + WithRtRatings);
            return string.Join("&", queries);
        }
    }

    public class TorrentItem
    {
        [JsonProperty("url")] public string Url { get; set; }
        [JsonProperty("hash")] public string Hash { get; set; }
        [JsonProperty("quality")] public string Quality { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        public string DisplayText => $"{Type}.{Quality}";
    }

    public class MovieItem : INotifyPropertyChanged
    {
        [JsonProperty("id")] public int Id { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("title_long")] public string LongTitle { get; set; }
        [JsonProperty("year")] public int Year { get; set; }
        [JsonProperty("runtime")] public int Runtime { get; set; }
        [JsonProperty("rating")] public double Rating { get; set; }
        [JsonProperty("cover")] public string CoverUrl { get; set; }
        [JsonProperty("background")] public string BackgroundUrl { get; set; }
        [JsonProperty("genres")] public string[] Genres { get; set; }
        [JsonProperty("summary")] public string Summary { get; set; }
        [JsonProperty("trailer")] public string YtTrailerCode { get; set; }
        [JsonProperty("mpa_rating")] public string MpaRating { get; set; }
        [JsonProperty("torrents")] public TorrentItem[] Torrents { get; set; }

        private bool _loaded;
        private Tuple<double, double> _tuple;

        [JsonIgnore]
        public double Position
        {
            get
            {
                if (!_loaded)
                {
                    _tuple = SettingHelper.LoadMediaPosition(Id);
                    _loaded = true;
                }

                return _tuple?.Item1 ?? 0;
            }
        }

        [JsonIgnore]
        public double Duration
        {
            get
            {
                if (!_loaded)
                {
                    _tuple = SettingHelper.LoadMediaPosition(Id);
                    _loaded = true;
                }

                return _tuple?.Item2 ?? 0;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void NotifyPositionChanged()
        {
            _loaded = false;
            OnPropertyChanged(nameof(Position));
            OnPropertyChanged(nameof(Duration));
        }
    }

    public class MovieViewModel : ObservableCollection<MovieItem>, ISupportIncrementalLoading
    {
        public EventHandler OnLoadMoreStart;
        public EventHandler OnLoadMoreEnd;

        private bool _busy;
        private readonly List<int> _movieIds = new List<int>();

        private readonly YtsRequest _request;

        public MovieViewModel(YtsRequest request)
        {
            _request = request;
        }

        public async Task<int> UpdateMovies(bool clear = false)
        {
            if (clear)
            {
                Clear();
                _movieIds.Clear();
                _request.Page = 1;
                HasMoreItems = true;
            }

            var count = 0;
            while (true)
            {
                try
                {
                    var movies = await YtsServerHelper.GetMoviesAsync(_request);
                    if (movies?.Count > 0)
                    {
                        foreach (var movie in movies)
                        {
                            if (!_movieIds.Contains(movie.Id))
                            {
                                Add(movie);
                                _movieIds.Add(movie.Id);
                                count++;
                            }
                        }

                        _request.Page++;
                    }
                    else
                    {
                        HasMoreItems = false;
                    }

                    break;
                }
                catch (Exception e) //unknown error
                {
                    if (e is InvalidOperationException || e is HttpRequestException) // server not started yet
                    {
                        await Task.Delay(200);
                    }
                    else
                    {
                        HasMoreItems = false;
                        break;
                    }
                }
            }

            return count;
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            if (_busy)
            {
                throw new InvalidOperationException("Only one operation in flight at a time");
            }

            _busy = true;
            OnLoadMoreStart?.Invoke(this, EventArgs.Empty);
            return AsyncInfo.Run(c => LoadMoreItemsAsync());
        }

        public bool HasMoreItems { get; set; } = true;

        private async Task<LoadMoreItemsResult> LoadMoreItemsAsync()
        {
            try
            {
                var addedMovies = await UpdateMovies();
                return new LoadMoreItemsResult {Count = (uint) addedMovies};
            }
            finally
            {
                _busy = false;
                OnLoadMoreEnd?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}