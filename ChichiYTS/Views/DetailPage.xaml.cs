using System;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using ChichiYTS.Helpers;
using ChichiYTS.ViewModels;
using System.Collections.Generic;
using Windows.UI.Core;
using ListView = Windows.UI.Xaml.Controls.ListView;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace ChichiYTS.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class DetailPage
    {
        public MovieItem ViewModel { get; set; }

        private bool _playProcessing;

        public DetailPage()
        {
            InitializeComponent();
            SystemNavigationManager.GetForCurrentView().AppViewBackButtonVisibility = AppViewBackButtonVisibility.Visible;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter is MovieItem parameter)
            {
                ViewModel = parameter;
            }
        }

        private async void Play_OnTapped(object sender, TappedRoutedEventArgs e)
        {
            if (_playProcessing)
                return;

            _playProcessing = true;
            ProgressBar.Visibility = Visibility.Visible;
            var subtitleData = await YtsServerHelper.GetSubtitleDataAsync(ViewModel.Title, ViewModel.Year);
            ProgressBar.Visibility = Visibility.Collapsed;
            var torrentUrl = ((TorrentItem) ((Button) sender).DataContext).Url;
            if (subtitleData?.Subtitles?.Count > 0)
            {
                var subtitles = subtitleData.Subtitles;
                var subtitlesList = new ListView
                {
                    ItemsSource = new ObservableCollection<string>(subtitles.Keys),
                    IsItemClickEnabled = true
                };
                subtitlesList.ItemClick += (o, args) =>
                {
                    ((ContentDialog) subtitlesList.Parent).Hide();

                    var language = (string) args.ClickedItem;
                    var links = subtitles[language];
                    //await YtsServerHelper.SaveInitialSubtitle(links[0]);

                    Play(torrentUrl, links);
                };

                var subtitleDialog = new ContentDialog
                {
                    Title = "Choose subtitle language",
                    Content = subtitlesList,
                    PrimaryButtonText = "Play without subtitle",
                    CloseButtonText = "Close"
                };
                var result = await subtitleDialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    // Don't use subtitle
                    Play(torrentUrl);
                }
            }
            else if (subtitleData?.Titles?.Count > 0)
            {
                var titles = subtitleData.Titles;
                var titlesList = new ListView
                {
                    ItemsSource = new ObservableCollection<string>(titles.Keys),
                    IsItemClickEnabled = true
                };
                titlesList.ItemClick += async (o1, args1) =>
                {
                    ((ContentDialog) titlesList.Parent).Hide();

                    var title = (string) args1.ClickedItem;
                    var link = titles[title];
                    ProgressBar.Visibility = Visibility.Visible;
                    var subtitles = await YtsServerHelper.GetSubtitlesAsync(link);
                    ProgressBar.Visibility = Visibility.Collapsed;

                    var subtitlesList = new ListView
                    {
                        ItemsSource = new ObservableCollection<string>(subtitles.Keys),
                        IsItemClickEnabled = true
                    };
                    subtitlesList.ItemClick += (o, args) =>
                    {
                        ((ContentDialog)subtitlesList.Parent).Hide();

                        var language = (string)args.ClickedItem;
                        var links = subtitles[language];
                        //await YtsServerHelper.SaveInitialSubtitle(links[0]);

                        Play(torrentUrl, links);
                    };

                    var subtitleDialog = new ContentDialog
                    {
                        Title = "Choose subtitle language",
                        Content = subtitlesList,
                        PrimaryButtonText = "Play without subtitle",
                        CloseButtonText = "Close"
                    };
                    var result = await subtitleDialog.ShowAsync();
                    if (result == ContentDialogResult.Primary)
                    {
                        // Don't use subtitle
                        Play(torrentUrl);
                    }
                };

                var titleDialog = new ContentDialog
                {
                    Title = "Select subtitle",
                    Content = titlesList,
                    PrimaryButtonText = "Play without subtitle",
                    CloseButtonText = "Close"
                };
                var result1 = await titleDialog.ShowAsync();
                if (result1 == ContentDialogResult.Primary)
                {
                    // Don't use subtitle
                    Play(torrentUrl);
                }
            }
            else
            {
                var dialog = new ContentDialog
                {
                    Title = "No subtitle found",
                    Content = "Play without subtitle?",
                    PrimaryButtonText = "Sure",
                    CloseButtonText = "No"
                };
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    // Don't use subtitle
                    Play(torrentUrl);
                }
            }

            _playProcessing = false;
        }

        private async void YtTrailer_OnClick(object sender, RoutedEventArgs e)
        {
            var code = (string) ((Button) sender).Tag;
            var uri = new Uri($"https://www.youtube.com/watch?v={code}");
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        private async void Play(string torrentUrl, Dictionary<string, string> subtitleLinks = null)
        {
            ProgressBar.Visibility = Visibility.Visible;
            await YtsServerHelper.RegisterAsync(torrentUrl);
            ProgressBar.Visibility = Visibility.Collapsed;
            Frame.Navigate(typeof(PlayerPage), new Tuple<MovieItem, Dictionary<string, string>>(ViewModel, subtitleLinks));
        }
    }
}