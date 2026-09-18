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

    // Preserves the original plist numeric tag ("integer" or "real") so round-tripping
    // a <real> value doesn't get silently rewritten as <integer> on save.
    public string NumericSubType { get; set; } = "integer";
}

public enum PlistElementType
{
    Dictionary, Array, Boolean, Data, Date, Number, UID, String
}