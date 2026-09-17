using PlistExplorer.Viewmodels;
using System.Windows.Controls;
using System.Windows.Input;

namespace PlistExplorer.Views
{
    /// <summary>
    /// Interaction logic for PlistElementView.xaml
    /// </summary>
    public partial class PlistElementView : UserControl
    {
        public PlistElementView()
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
    }
}
