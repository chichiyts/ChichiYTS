using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using ChichiYTS.Helpers;
using ChichiYTS.Views;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ChichiYTS
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            await YtsServerHelper.StartServerAsync();
        }

        private void MainNavigator_OnLoaded(object sender, RoutedEventArgs e)
        {
            MainNavigator.SelectedItem = MainNavigator.MenuItems[0];
        }

        private void MainNavigator_OnSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            switch (((NavigationViewItem) args.SelectedItem).Tag)
            {
                case "Home":
                    MainFrame.Visibility = Visibility.Visible;
                    FavoritesFrame.Visibility = Visibility.Collapsed;

                    if (MainFrame.SourcePageType == null)
                        MainFrame.Navigate(typeof(HomePage));
                    break;
                case "Favorites":
                    MainFrame.Visibility = Visibility.Collapsed;
                    FavoritesFrame.Visibility = Visibility.Visible;

                    if (FavoritesFrame.SourcePageType == null)
                        FavoritesFrame.Navigate(typeof(FavoritesPage));
                    break;
            }
        }

        private void MainNavigator_OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {

        }

        private void Frame_OnNavigated(object sender, NavigationEventArgs e)
        {
            MainNavigator.IsBackEnabled = ((Frame) sender).CanGoBack;
        }

        private void MainNavigator_OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            switch (((NavigationViewItem) sender.SelectedItem).Tag)
            {
                case "Home":
                    if (MainFrame.CanGoBack)
                        MainFrame.GoBack();
                    break;
                case "Favorites":
                    if (FavoritesFrame.CanGoBack)
                        FavoritesFrame.GoBack();
                    break;
            }
        }
    }
}
