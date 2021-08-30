using Windows.UI.Xaml.Controls;

namespace ChichiYTS.Custom
{
    public class ChichiMediaTransportControls: MediaTransportControls
    {
        private TextBlock _downloadRateTextBlock;

        public ChichiMediaTransportControls()
        {
            DefaultStyleKey = typeof(ChichiMediaTransportControls);
        }

        protected override void OnApplyTemplate()
        {
            _downloadRateTextBlock = GetTemplateChild("DownloadRate") as TextBlock;
            base.OnApplyTemplate();
        }

        public void SetDownloadRateText(string text)
        {
            _downloadRateTextBlock.Text = text;
        }
    }
}
