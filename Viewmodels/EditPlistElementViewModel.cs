using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace PlistExplorer.Viewmodels;

public partial class EditPlistElementViewModel(PlistElementViewModel targetElement) : ObservableObject
{
    public PlistElementViewModel TargetElement { get; } = targetElement;

    public ObservableCollection<PlistElementViewModel> Properties { get; } = targetElement.Children;

    [ObservableProperty] public partial PlistElementViewModel? SelectedProperty { get; set; } = null;

    [RelayCommand]
    public static void Save(Window window)
    {
        if (window != null)
        {
            window.DialogResult = true; // Signals ShowDialog() to return true
            window.Close();
        }
    }
}
