using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using ChichiYTS.Helpers;
using ChichiYTS.ViewModels;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace ChichiYTS.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomePage
    {
        private readonly MovieViewModel _movieViewModel;
        private readonly YtsRequest _request;

        public HomePage()
        {
            InitializeComponent();

            AutoSuggestBox.Focus(FocusState.Programmatic);

            _request = new YtsRequest
            {
                Page = 1,
                SortBy = "download_count",
                Limit = 20
            };

            _movieViewModel = new MovieViewModel(_request);
            _movieViewModel.OnLoadMoreStart += OnLoadMoreStart;
            _movieViewModel.OnLoadMoreEnd += OnLoadMoreEnd;
            MovieGridView.ItemsSource = _movieViewModel;
        }

        private void OnLoadMoreEnd(object sender, EventArgs e)
        {
            ProgressBar.Visibility = Visibility.Collapsed;
        }

        private void OnLoadMoreStart(object sender, EventArgs e)
        {
            ProgressBar.Visibility = Visibility.Visible;
        }

        private void MovieItem_OnTapped(object sender, TappedRoutedEventArgs e)
        {
            if (sender is Panel panel)
            {
                if (panel.DataContext is MovieItem movieItem)
                {
                    Frame.Navigate(typeof(DetailPage), movieItem);
                }
            }
        }

        private async void AutoSuggestBox_OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            _request.QueryTerm = args.QueryText;
            await _movieViewModel.UpdateMovies(true);
        }

        private async void AutoSuggestBoxDeleteButton_OnClick(object sender, RoutedEventArgs e)
        {
            _request.QueryTerm = null;
            await _movieViewModel.UpdateMovies(true);
        }

        private async void SortByComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SortByComboBox.SelectedItem is ComboBoxItem item)
            {
                _request.SortBy = item.Tag?.ToString();
                await _movieViewModel.UpdateMovies(true);
            }
        }

        private async void GenreComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GenreComboBox.SelectedItem is ComboBoxItem item)
            {
                _request.Genre = item.Tag?.ToString();
                await _movieViewModel.UpdateMovies(true);
            }
        }
    }
}