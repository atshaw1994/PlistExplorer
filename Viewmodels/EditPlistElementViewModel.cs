using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlistExplorer.Models;
using System.Collections.ObjectModel;

namespace PlistExplorer.Viewmodels;

public partial class EditPlistElementViewModel(PlistElementViewModel targetElement) : ObservableObject
{
    public PlistElementViewModel TargetElement { get; } = targetElement;

    public ObservableCollection<PlistElementViewModel> Properties { get; } = targetElement.Children;

    [ObservableProperty] public partial PlistElementViewModel? SelectedProperty { get; set; } = null;

    // Fields for adding a new entry
    [ObservableProperty] public partial string NewKey { get; set; } = string.Empty;

    [ObservableProperty] public partial PlistElementType NewType { get; set; } = PlistElementType.String;

    public static Array AvailableTypes => Enum.GetValues<PlistElementType>();

    [RelayCommand]
    public void AddProperty()
    {
        if (string.IsNullOrWhiteSpace(NewKey)) return;

        Properties.Add(new PlistElementViewModel
        {
            ElementName = NewKey,
            ElementType = NewType,
            ElementValue = string.Empty
        });

        NewKey = string.Empty;
    }

    [RelayCommand]
    public void DeleteProperty(PlistElementViewModel property)
    {
        if (property != null && Properties.Contains(property))
        {
            Properties.Remove(property);
        }
    }
}
