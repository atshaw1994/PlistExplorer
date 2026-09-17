using PlistExplorer.Viewmodels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
    }
}
