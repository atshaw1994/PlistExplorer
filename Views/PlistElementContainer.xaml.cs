using PlistExplorer.Viewmodels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PlistExplorer.Views
{
    /// <summary>
    /// Interaction logic for PlistElementContainer.xaml
    /// </summary>
    public partial class PlistElementContainer : UserControl
    {
        public PlistElementContainer()
        {
            InitializeComponent();
        }

        private void OnContainerPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source && DataContext is PlistElementContainerViewModel containerVm)
            {
                if (!IsClickOnElementTile(source))
                {
                    // Set focus so keyboard shortcuts (Ctrl+C / Ctrl+V) work immediately
                    Focus();

                    // Deselect all items in the container
                    containerVm.DeselectAll();
                }
            }
        }

        private void OnContainerPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Check if the right-click landed directly on the container/whitespace 
            // rather than an individual PlistElementView child
            if (e.OriginalSource is DependencyObject && DataContext is PlistElementContainerViewModel containerVm)
            {
                // Deselect all items in the container
                containerVm.DeselectAll();
            }
        }

        private static bool IsClickOnElementTile(DependencyObject source)
        {
            // Walk up the visual tree from the clicked target
            DependencyObject? current = source;
            while (current != null && current != source)
            {
                if (current is PlistElementView)
                {
                    return true;
                }
                current = VisualTreeHelper.GetParent(current);
            }
            return source is PlistElementView;
        }
    }
}
