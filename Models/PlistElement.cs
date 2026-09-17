using System.Xml.Linq;

namespace PlistExplorer.Models;

public class PlistElement
{
    public string ElementName { get; set; } = string.Empty;
    public PlistElementType ElementType { get; set; } = PlistElementType.String;

    // Natively holds bool, int, double, byte[], DateTime, or string
    public object? ElementValue { get; set; }

    public List<PlistElement> Children { get; set; } = [];

    // Property required for holding raw XML nodes during deep serialization
    public XElement? RawXElement { get; set; }
}

public enum PlistElementType
{
    String,
    Number,
    UID,
    Boolean,
    Date,
    Data,
    Array,
    Dictionary
}