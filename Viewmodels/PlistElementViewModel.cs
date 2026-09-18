using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlistExplorer.Models;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml.Linq;

namespace PlistExplorer.Viewmodels;

public partial class PlistElementViewModel : ObservableObject
{
    public PlistElement Model { get; }
    public ObservableCollection<PlistElementViewModel> Children { get; } = [];
    public bool IsFalseBoolean => ElementType == PlistElementType.Boolean &&
                            string.Equals(ElementValue!.ToString(), "false", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty] public partial string ElementName { get; set; } = string.Empty;

    [ObservableProperty] public partial PlistElementType ElementType { get; set; } = PlistElementType.String;

    [ObservableProperty] public partial object? ElementValue { get; set; }

    [ObservableProperty] public partial bool IsSelected { get; set; } = false;

    [RelayCommand]
    public void ToggleSelect() => IsSelected = !IsSelected;

    public PlistElementViewModel(PlistElement model)
    {
        Model = model;
        ElementName = model.ElementName;
        ElementType = model.ElementType;
        ElementValue = model.ElementValue;

        // Populate children recursively if the element is a Dictionary or Array
        if (model.RawXElement != null && (ElementType == PlistElementType.Dictionary || ElementType == PlistElementType.Array))
        {
            ParseChildren(model.RawXElement);
        }
    }

    public PlistElementViewModel()
    {
        Model = new PlistElement
        {
            ElementName = "BooleanElement",
            ElementType = PlistElementType.Boolean,
            ElementValue = false
        };

        ElementName = Model.ElementName;
        ElementType = Model.ElementType;
        ElementValue = Model.ElementValue;
    }

    private void ParseChildren(XElement containerNode)
    {
        Children.Clear();

        if (ElementType == PlistElementType.Dictionary)
        {
            // Read alternating <key> and value pairs inside <dict>
            var elements = containerNode.Elements().ToList();
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i].Name == "key" && i + 1 < elements.Count)
                {
                    string keyName = elements[i].Value;
                    XElement valNode = elements[i + 1];

                    var childModel = CreateModelFromNode(keyName, valNode);

                    Children.Add(new PlistElementViewModel(childModel));
                    i++; // Skip the value node on next iteration
                }
            }
        }
        else if (ElementType == PlistElementType.Array)
        {
            // Read items directly inside <array>
            int index = 0;
            foreach (var valNode in containerNode.Elements())
            {
                var childModel = CreateModelFromNode($"Item {index++}", valNode);

                Children.Add(new PlistElementViewModel(childModel));
            }
        }
    }

    private static PlistElementType GetTypeFromNode(XElement node) => node.Name.LocalName.ToLower() switch
    {
        "string" => PlistElementType.String,
        "integer" => PlistElementType.Number,
        "real" => PlistElementType.Number,
        "true" => PlistElementType.Boolean,
        "false" => PlistElementType.Boolean,
        "date" => PlistElementType.Date,
        "data" => PlistElementType.Data,
        "uid" => PlistElementType.UID,
        "array" => PlistElementType.Array,
        "dict" => PlistElementType.Dictionary,
        _ => PlistElementType.String
    };

    // Builds a correctly-typed PlistElement from a raw XML node: booleans become real bool values,
    // data becomes decoded bytes (plist <data> is base64, not hex), and the original integer/real
    // tag name is preserved so numbers round-trip correctly on save.
    internal static PlistElement CreateModelFromNode(string keyName, XElement node)
    {
        var type = GetTypeFromNode(node);

        object? value = type switch
        {
            PlistElementType.Boolean => node.Name.LocalName.Equals("true", StringComparison.OrdinalIgnoreCase),
            PlistElementType.Data => TryDecodeBase64(node.Value.Trim()),
            _ => node.Value
        };

        return new PlistElement
        {
            ElementName = keyName,
            ElementType = type,
            ElementValue = value,
            RawXElement = node,
            NumericSubType = node.Name.LocalName.Equals("real", StringComparison.OrdinalIgnoreCase) ? "real" : "integer"
        };
    }

    internal static object TryDecodeBase64(string base64)
    {
        try
        {
            return Convert.FromBase64String(base64);
        }
        catch
        {
            // Fall back to the raw text if it isn't valid base64 (malformed input)
            return base64;
        }
    }

    // Typed accessor for Boolean DataGrid template
    public bool BoolValue
    {
        get => ElementValue is bool b && b;
        set
        {
            ElementValue = value;

            OnPropertyChanged(nameof(BoolValue));
        }
    }

    // Typed/Formatted accessor for Hex / Data Byte Arrays
    public string HexValue
    {
        get => ElementValue is byte[] bytes ? Convert.ToHexString(bytes) : ElementValue?.ToString() ?? string.Empty;
        set
        {
            try
            {
                ElementValue = Convert.FromHexString(value.Replace(" ", ""));
            }
            catch
            {
                ElementValue = value; // Fallback to raw input if invalid hex
            }
            OnPropertyChanged(nameof(HexValue));
        }
    }

    // Base64 accessor for Data elements, matching the plist <data> element's actual encoding.
    public string Base64Value
    {
        get => ElementValue is byte[] bytes ? Convert.ToBase64String(bytes) : ElementValue?.ToString() ?? string.Empty;
        set
        {
            ElementValue = TryDecodeBase64(value);
            OnPropertyChanged(nameof(Base64Value));
            OnPropertyChanged(nameof(HexValue));
        }
    }

    public string DisplayName
    {
        get
        {
            // If this item has children (e.g. Dictionary representing Item 0), search for specific keys
            if (Children.Count > 0)
            {
                var commentChild = Children.FirstOrDefault(c => c.ElementName.Equals("Comment", StringComparison.OrdinalIgnoreCase));
                if (commentChild != null && commentChild.ElementValue is string comment && !string.IsNullOrWhiteSpace(comment))
                {
                    return comment;
                }

                var bundlePathChild = Children.FirstOrDefault(c => c.ElementName.Equals("BundlePath", StringComparison.OrdinalIgnoreCase));
                if (bundlePathChild != null && bundlePathChild.ElementValue is string bundlePath && !string.IsNullOrWhiteSpace(bundlePath))
                {
                    return Path.GetFileName(bundlePath);
                }

                var pathChild = Children.FirstOrDefault(c => c.ElementName.Equals("Path", StringComparison.OrdinalIgnoreCase));
                if (pathChild != null && pathChild.ElementValue is string path && !string.IsNullOrWhiteSpace(path))
                {
                    return path;
                }
            }

            // Fallback to default ElementName (e.g. "Item 0") if no matching child exists
            return ElementName;
        }
    }

    // Keep DisplayName updated whenever a child's name or value changes.
    // Note: this only updates the view model; the underlying XML document is rebuilt from
    // view-model state at save time (see PlistElementContainerViewModel.BuildRootElement),
    // so there is no need to keep Model.RawXElement in sync here.
    partial void OnElementNameChanged(string value)
    {
        Model.ElementName = value;
        OnPropertyChanged(nameof(DisplayName));
    }

    partial void OnElementTypeChanged(PlistElementType value)
    {
        Model.ElementType = value;

        if (value == PlistElementType.Number)
        {
            Model.NumericSubType = "integer";
        }

        OnPropertyChanged(nameof(ElementType));
    }

    partial void OnElementValueChanged(object? value)
    {
        Model.ElementValue = value;

        OnPropertyChanged(nameof(IsFalseBoolean));
        OnPropertyChanged(nameof(DisplayName));
    }

}