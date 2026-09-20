using PlistExplorer.Models;
using PlistExplorer.Viewmodels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PlistExplorer.Views
{
    /// <summary>
    /// Interaction logic for PlistElementView.xaml
    /// </summary>
    public partial class PlistElementView : UserControl
    {
        private Point _dragStartPoint;

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

        private void OnContextMenuOpened(object sender, RoutedEventArgs e)
        {
            if (Tag is PlistElementContainerViewModel containerVm)
            {
                containerVm.RefreshCanPasteElements();
            }
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || DataContext is not PlistElementViewModel vm)
            {
                return;
            }

            Point currentPosition = e.GetPosition(null);
            Vector diff = _dragStartPoint - currentPosition;

            if (Math.Abs(diff.X) >= SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) >= SystemParameters.MinimumVerticalDragDistance)
            {
                DragDrop.DoDragDrop(this, new DataObject(typeof(PlistElementViewModel), vm), DragDropEffects.Move);
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;

            if (DataContext is PlistElementViewModel targetVm &&
                (targetVm.ElementType == PlistElementType.Dictionary || targetVm.ElementType == PlistElementType.Array) &&
                e.Data.GetDataPresent(typeof(PlistElementViewModel)) &&
                e.Data.GetData(typeof(PlistElementViewModel)) is PlistElementViewModel sourceVm &&
                sourceVm != targetVm)
            {
                e.Effects = DragDropEffects.Move;
            }

            e.Handled = true;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (DataContext is PlistElementViewModel targetVm &&
                Tag is PlistElementContainerViewModel containerVm &&
                e.Data.GetDataPresent(typeof(PlistElementViewModel)) &&
                e.Data.GetData(typeof(PlistElementViewModel)) is PlistElementViewModel sourceVm)
            {
                containerVm.MoveElement(sourceVm, targetVm);
            }

            e.Handled = true;
        }
    }
}
