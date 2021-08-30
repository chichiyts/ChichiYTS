using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace ChichiYTS.Helpers
{
    public static class UIElementExtensions
    {
        public static T FindControl<T>(this UIElement parent, string controlName) where T : FrameworkElement
        {
            if (parent == null)
                return null;

            if (parent.GetType() == typeof(T) && ((T)parent).Name == controlName)
            {
                return (T)parent;
            }
            T result = null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                UIElement child = (UIElement)VisualTreeHelper.GetChild(parent, i);

                if (FindControl<T>(child, controlName) != null)
                {
                    result = FindControl<T>(child, controlName);
                    break;
                }
            }
            return result;
        }
    }
}
