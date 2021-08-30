using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Timers;
using Windows.Foundation.Collections;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using ChichiYTS.Custom;
using ChichiYTS.Helpers;
using ChichiYTS.ViewModels;
using Exception = System.Exception;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace ChichiYTS.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PlayerPage
    {
        public static PlayerPage Current;
        /*private static readonly Color __subtitleBackgroundColor = Color.FromArgb(80, 20, 20, 20);*/

        private readonly Timer _updateDownloadRateTimer;

        private Dictionary<string, string> _subtitleLinks;
        private MovieItem _movieItem;
        private TimeSpan? _position;
        private ChichiStream _stream;
        private bool _canceled;
        //private Stopwatch _stopwatch;

        /*private Grid _gridTts;*/
        private ChichiMediaTransportControls _controls;

        public PlayerPage()
        {
            InitializeComponent();
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;

            _updateDownloadRateTimer = new Timer {AutoReset = true, Interval = 1000};
            _updateDownloadRateTimer.Elapsed += _timer_Elapsed;

            Current = this;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is Tuple<MovieItem, Dictionary<string, string>> parameter)
            {
                _movieItem = parameter.Item1;
                _subtitleLinks = parameter.Item2;
                var position = SettingHelper.LoadMediaPosition(_movieItem.Id)?.Item1;
                if (position > 0)
                    _position = TimeSpan.FromMilliseconds(position.Value);
            }

            StartPlay();
            _canceled = false;
            //_stopwatch = Stopwatch.StartNew();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            
            _updateDownloadRateTimer.Dispose();
            _canceled = true;
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);

            SaveCurrentPosition();
            _movieItem.NotifyPositionChanged();
            VideoPlayer?.MediaPlayer?.Pause();
            await YtsServerHelper.UnregisterAsync();
        }

        public void SaveCurrentPosition()
        {
            if (VideoPlayer.MediaPlayer == null)
                return;

            try
            {
                SettingHelper.SaveMediaPosition(
                    _movieItem.Id,
                    VideoPlayer.MediaPlayer.PlaybackSession.Position.TotalMilliseconds,
                    VideoPlayer.MediaPlayer.PlaybackSession.NaturalDuration.TotalMilliseconds);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void StartPlay()
        {
            //var source = MediaSource.CreateFromUri(YtsServerHelper.GetStreamUri());
            var storagePath = ApplicationData.Current.LocalFolder.Path;
            _stream = new ChichiStream(storagePath);
            var source = MediaSource.CreateFromStream(_stream, _stream.MimeType);
            var playbackItem = new MediaPlaybackItem(source);
            playbackItem.TimedMetadataTracksChanged += (item, args) =>
            {
                switch (args.CollectionChange)
                {
                    case CollectionChange.ItemInserted:
                        if (args.Index > 0)
                        {
                            var track = item.TimedMetadataTracks[(int) args.Index];
                            track.Label = $"Subtitle by {track.Language}";
                            if (args.Index == 1)
                            {
                                playbackItem.TimedMetadataTracks.SetPresentationMode(1,
                                    TimedMetadataTrackPresentationMode.PlatformPresented);
                            }
                        }

                        break;
                }
            };

            if (_subtitleLinks?.Count > 0)
            {
                //var tts = TimedTextSource.CreateFromUri(new Uri("ms-appdata:///local/" + YtsServerHelper.InitialSubtitleFileName));
                var tts = TimedTextSource.CreateFromUri(new Uri("ms-appx:///Assets/sample.srt"), "none");
                /*tts.Resolved += (textSource, args) =>
                {
                    var cues = ((TimedTextCue) args.Tracks[0].Cues[0]).CueStyle.FontFamily;
                };*/
                source.ExternalTimedTextSources.Add(tts);

                UpdateNewSubtitles(source);
            }

            if (_position != null)
                VideoPlayer.MediaPlayer.PlaybackSession.Position = _position.Value;
            VideoPlayer.Source = playbackItem;
            _controls = (ChichiMediaTransportControls) VideoPlayer.TransportControls;
            _updateDownloadRateTimer.Start();
        }

        private async void UpdateNewSubtitles(MediaSource source)
        {
            // wait for none loaded
            await Task.Delay(4567);

            foreach (var (link, user) in _subtitleLinks)
            {
                if (_canceled)
                    break;
                
                var uri = await YtsServerHelper.CreateSubtitleUri(link);
                if (uri != null)
                {
                    var tts = TimedTextSource.CreateFromUri(uri, user);
                    tts.Resolved += (textSource, args) =>
                    {
                        //var cues = ((TimedTextCue) args.Tracks[0].Cues[0]).Lines[0].Text;
                    };
                    source.ExternalTimedTextSources.Add(tts);
                }
            }
        }

        /*private async void MediaPlayer_SubtitleFrameChanged(MediaPlayer sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (_gridTts?.Children.FirstOrDefault() is Grid grid)
                {
                    if (grid.Children.FirstOrDefault() is Border border)
                    {
                        if (border.Child is StackPanel panel)
                        {
                            foreach (var gridSubtitle in panel.Children)
                            {
                                if (gridSubtitle is Grid g)
                                {
                                    g.Background = new SolidColorBrush(__subtitleBackgroundColor);
                                }
                            }
                        }
                    }
                }
            });
        }

        private async void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                () => { _gridTts = VideoPlayer.FindControl<Grid>("TimedTextSourcePresenter"); });
        }

        private async void MediaPlayer_CurrentStateChanged(MediaPlayer sender, object args)
        {
            var state = sender.PlaybackSession.PlaybackState;
            switch (state)
            {
                case MediaPlaybackState.Playing:
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () => { LoadingGrid.Visibility = Visibility.Collapsed; });
                    _updateDownloadRateTimer.Stop();
                    break;
                default:
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                        () => { LoadingGrid.Visibility = Visibility.Visible; });
                    _updateDownloadRateTimer.Start();
                    break;
            }
        }*/

        private async void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var downloadRate = _stream.GetDownloadRate();
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                try
                {
                    _controls.SetDownloadRateText($"{Utils.HumanReadableByteCount(downloadRate, true)}/s");
                }
                catch (Exception exception) //in case navigated to other
                {
                    Console.WriteLine(exception);
                }
            });
        }

        private void KeyboardAccelerator_OnInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            switch (args.KeyboardAccelerator.Key)
            {
                case VirtualKey.F:
                    VideoPlayer.IsFullWindow = !VideoPlayer.IsFullWindow;
                    args.Handled = true;
                    break;
                case VirtualKey.Escape:
                    VideoPlayer.IsFullWindow = false;
                    args.Handled = true;
                    break;
                case VirtualKey.B:
                    //todo: show position
                    VideoPlayer.MediaPlayer.PlaybackSession.Position -= TimeSpan.FromSeconds(10);
                    args.Handled = true;
                    break;
                case VirtualKey.N:
                    //todo: show position
                    VideoPlayer.MediaPlayer.PlaybackSession.Position += TimeSpan.FromSeconds(10);
                    args.Handled = true;
                    break;
                case VirtualKey.Up:
                    //todo: show volume
                    VideoPlayer.MediaPlayer.Volume += 0.1;
                    args.Handled = true;
                    break;
                case VirtualKey.Down:
                    //todo: show volume
                    VideoPlayer.MediaPlayer.Volume -= 0.1;
                    args.Handled = true;
                    break;
                case VirtualKey.P:
                    if (VideoPlayer.MediaPlayer.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
                        VideoPlayer.MediaPlayer.Pause();
                    else VideoPlayer.MediaPlayer.Play();
                    args.Handled = true;
                    break;
            }
        }
    }
}