using PlistExplorer.Viewmodels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PlistExplorer.Views
{
    /// <summary>
    /// Interaction logic for PlistElementListItemView.xaml
    /// </summary>
    public partial class PlistElementListItemView : UserControl
    {
        public PlistElementListItemView()
        {
            InitializeComponent();
        }

        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is PlistElementViewModel vm && !vm.IsSelected)
            {
                vm.IsSelected = true;
            }
        }

        private void OnContextMenuOpened(object sender, RoutedEventArgs e)
        {
            if (Tag is PlistElementContainerViewModel containerVm)
            {
                containerVm.RefreshCanPasteElements();
            }
        }
    }
}
