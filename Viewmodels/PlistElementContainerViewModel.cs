using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlistExplorer.Models;
using PlistExplorer.Views;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Xml.Linq;

namespace PlistExplorer.Viewmodels
{
    public partial class PlistElementContainerViewModel : ObservableObject
    {
        private readonly Stack<PlistElementViewModel?> _backStack = new();
        private readonly Stack<PlistElementViewModel?> _forwardStack = new();
        private readonly List<string> _pathSegments = [];
        private List<PlistElementViewModel> _rootElements = [];
        private PlistElementViewModel? _currentContainer;
        private bool CanNavigateBack() => _backStack.Count > 0;
        private bool CanNavigateForward() => _forwardStack.Count > 0;

        // The collection currently backing the displayed view (root list or the active container's children).
        // This is the actual source of truth: mutating it (add/remove) persists structural changes.
        private IList<PlistElementViewModel> CurrentSource => (IList<PlistElementViewModel>?)_currentContainer?.Children ?? _rootElements;

        public string RootElementType { get; private set; } = "dict";

        public ObservableCollection<PlistElementViewModel> LoadedElements { get; } = [];

        [ObservableProperty] public partial PlistViewMode CurrentViewMode { get; set; } = PlistViewMode.Icon;

        [RelayCommand]
        public void SetViewMode(PlistViewMode viewMode) => CurrentViewMode = viewMode;

        // Raised whenever the underlying element tree is structurally or value-modified
        // (add/delete/paste/leaf edit), so MainViewModel can track unsaved changes.
        public event Action? DataChanged;

        [ObservableProperty] public partial string CurrentPath { get; set; }

        #region Commands

        [RelayCommand]
        public void OpenElement(PlistElementViewModel element)
        {
            bool isContainer = element.ElementType == PlistElementType.Dictionary ||
                               element.ElementType == PlistElementType.Array;

            if (isContainer)
            {
                // Remember the current container so we can restore it when navigating back
                _backStack.Push(_currentContainer);
                _forwardStack.Clear();

                _pathSegments.Add(element.ElementName);
                UpdatePathText();

                _currentContainer = element;
                RefreshDisplayFromSource();

                NotifyCanExecuteChanged();
            }
            else
            {
                if (OpenEditWindowForLeaf(element))
                {
                    DataChanged?.Invoke();
                }
            }
        }

        [RelayCommand]
        public void CopySelectedElements()
        {
            var selectedItems = LoadedElements.Where(e => e.IsSelected).ToList();
            if (selectedItems.Count == 0) return;

            var xmlBuilder = new StringBuilder();

            foreach (var item in selectedItems)
            {
                // Convert PlistElementViewModel back to standard XElement
                XElement elementXml = ToXElement(item);
                xmlBuilder.AppendLine(elementXml.ToString());
            }

            // Place raw XML text onto the OS clipboard
            Clipboard.SetText(xmlBuilder.ToString().Trim());

            RefreshCanPasteElements();
        }

        // Determines whether the clipboard currently holds text that represents one or more
        // valid plist elements, used both as the PasteElements CanExecute check and to
        // control the visibility of the "Paste" context menu item.
        [ObservableProperty] public partial bool CanPasteElements { get; set; }

        // Re-evaluates CanPasteElements against the current clipboard contents. Should be called
        // whenever the clipboard may have changed (e.g. after a copy, or right before the
        // "Paste" context menu is shown) since there is no clipboard-changed notification to bind to.
        public void RefreshCanPasteElements()
        {
            CanPasteElements = ComputeCanPasteElements();
        }

        private bool ComputeCanPasteElements()
        {
            if (!Clipboard.ContainsText()) return false;

            string clipboardText = Clipboard.GetText().Trim();
            if (string.IsNullOrWhiteSpace(clipboardText)) return false;

            try
            {
                string wrappedXml = $"<root>{clipboardText}</root>";
                var parsedXml = XElement.Parse(wrappedXml);

                return parsedXml.Elements().Any(node => ParseElementFromXml(node) != null);
            }
            catch
            {
                return false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanPasteElements))]
        public void PasteElements()
        {
            if (!Clipboard.ContainsText()) return;

            string clipboardText = Clipboard.GetText().Trim();
            if (string.IsNullOrWhiteSpace(clipboardText)) return;

            try
            {
                // Wrap pasted text in a temporary root if multiple nodes were copied
                string wrappedXml = $"<root>{clipboardText}</root>";
                var parsedXml = XElement.Parse(wrappedXml);

                foreach (var node in parsedXml.Elements())
                {
                    // Convert XML node back into PlistElementViewModel
                    var elementViewModel = ParseElementFromXml(node);
                    if (elementViewModel != null)
                    {
                        elementViewModel.ElementName = GetUniqueElementName(elementViewModel.ElementName);

                        // Add to the actual source collection so the change persists (not just the display view)
                        CurrentSource.Add(elementViewModel);
                        LoadedElements.Add(elementViewModel);
                    }
                }
            }
            catch
            {
                // Fail quietly or log if the text on clipboard isn't valid XML
            }

            DataChanged?.Invoke();
        }

        // The following commented-out code is for when c# 15 union types are available
        //
        //[RelayCommand]
        //public void AddNewElement(string elementType)
        //{
        //    public union PlistValue(string, double, long, bool, DateTime, byte[]);
        //
        //    // Guard against invalid XAML parameters
        //    // 1. Convert the UI string into the closed union domain at the boundary
        //    if (!PlistType.TryParse(elementType, out PlistType selectedType))
        //        return; // Guard early against invalid XAML parameters

        //    // 2. Exhaustive match with NO fallback needed!
        //    PlistElementViewModel newElement = selectedType switch
        //    {
        //        PlistType.Dict => new(new PlistElement { ElementName = "NewDictionary", ElementType = PlistElementType.Dictionary }),
        //        PlistType.Array => new(new PlistElement { ElementName = "NewArray", ElementType = PlistElementType.Array }),
        //        PlistType.Boolean => new(new PlistElement { ElementName = "NewBoolean", ElementType = PlistElementType.Boolean, ElementValue = false }),
        //        PlistType.Data => new(new PlistElement { ElementName = "NewData", ElementType = PlistElementType.Data, ElementValue = string.Empty }),
        //        PlistType.Date => new(new PlistElement { ElementName = "NewDate", ElementType = PlistElementType.Date, ElementValue = DateTime.Now }),
        //        PlistType.Number => new(new PlistElement { ElementName = "NewNumber", ElementType = PlistElementType.Number, ElementValue = 0 }),
        //        PlistType.UID => new(new PlistElement { ElementName = "NewUID", ElementType = PlistElementType.UID, ElementValue = string.Empty }),
        //        PlistType.String => new(new PlistElement { ElementName = "NewString", ElementType = PlistElementType.String, ElementValue = string.Empty })
        //    };

        //    newElement.ElementName = GetUniqueElementName(newElement.ElementName);
        //    CurrentSource.Add(newElement);
        //    LoadedElements.Add(newElement);
        //    DataChanged?.Invoke();
        //}

        [RelayCommand]
        public void AddNewElement(string elementType)
        {
            PlistElementViewModel? newElement = elementType switch
            {
                "Dict" => new PlistElementViewModel(new PlistElement
                {
                    ElementName = "NewDictionary",
                    ElementType = PlistElementType.Dictionary
                }),
                "Array" => new PlistElementViewModel(new PlistElement
                {
                    ElementName = "NewArray",
                    ElementType = PlistElementType.Array
                }),
                "Boolean" => new PlistElementViewModel(new PlistElement
                {
                    ElementName = "NewBoolean",
                    ElementType = PlistElementType.Boolean,
                    ElementValue = false
                }),
                "Data" => new PlistElementViewModel(new PlistElement
                {
                    ElementName = "NewData",
                    ElementType = PlistElementType.Data,
                    ElementValue = string.Empty
                }),
                "Date" => new PlistElementViewModel(new PlistElement
                {
                    ElementName = "NewDate",
                    ElementType = PlistElementType.Date,
                    ElementValue = DateTime.Now
                }),
                "Number" => new PlistElementViewModel(new PlistElement
                {
                    ElementName = "NewNumber",
                    ElementType = PlistElementType.Number,
                    ElementValue = 0
                }),
                "UID" => new PlistElementViewModel(new PlistElement
                {
                    ElementName = "NewUID",
                    ElementType = PlistElementType.UID,
                    ElementValue = string.Empty
                }),
                "String" => new PlistElementViewModel(new PlistElement
                {
                    ElementName = "NewString",
                    ElementType = PlistElementType.String,
                    ElementValue = string.Empty
                }),
                _ => null
            };

            if (newElement != null)
            {
                newElement.ElementName = GetUniqueElementName(newElement.ElementName);

                // Add to the actual source collection so the change persists (not just the display view)
                CurrentSource.Add(newElement);
                LoadedElements.Add(newElement);
                DataChanged?.Invoke();
            }
        }

        // Only dictionaries require unique keys; arrays/root arrays can have duplicate names freely.
        private bool IsCurrentSourceDictionary =>
            _currentContainer?.ElementType == PlistElementType.Dictionary ||
            (_currentContainer == null && RootElementType.Equals("dict", StringComparison.OrdinalIgnoreCase));

        private string GetUniqueElementName(string desiredName)
        {
            if (!IsCurrentSourceDictionary) return desiredName;

            var existingNames = new HashSet<string>(CurrentSource.Select(e => e.ElementName), StringComparer.Ordinal);
            if (!existingNames.Contains(desiredName)) return desiredName;

            int suffix = 2;
            string candidate;
            do
            {
                candidate = $"{desiredName} ({suffix})";
                suffix++;
            } while (existingNames.Contains(candidate));

            return candidate;
        }

        [RelayCommand]
        public void SelectElement(PlistElementViewModel targetElement)
        {
            // If Ctrl is held down, toggle selection for multi-select
            if (System.Windows.Input.Keyboard.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control))
            {
                targetElement.IsSelected = !targetElement.IsSelected;
            }
            else
            {
                // Clear selection on all other elements
                foreach (var element in LoadedElements)
                {
                    element.IsSelected = (element == targetElement);
                }
            }
        }

        [RelayCommand]
        public void DeselectAll()
        {
            foreach (var element in LoadedElements)
            {
                element.IsSelected = false;
            }
        }

        // Moves an element into a target Dictionary/Array element, relocating it from wherever it
        // currently lives (the visible collection backed by CurrentSource) into target.Children.
        public void MoveElement(PlistElementViewModel source, PlistElementViewModel target)
        {
            if (source == null || target == null || source == target) return;

            bool targetIsContainer = target.ElementType == PlistElementType.Dictionary ||
                                     target.ElementType == PlistElementType.Array;
            if (!targetIsContainer) return;

            // Prevent dropping a container into itself or one of its own descendants
            if (IsDescendant(source, target)) return;

            if (!LoadedElements.Contains(source)) return;

            CurrentSource.Remove(source);
            LoadedElements.Remove(source);

            if (target.ElementType == PlistElementType.Dictionary)
            {
                source.ElementName = GetUniqueChildName(source.ElementName, target.Children);
            }

            target.Children.Add(source);

            DataChanged?.Invoke();
        }

        private static bool IsDescendant(PlistElementViewModel node, PlistElementViewModel potentialDescendant)
        {
            foreach (var child in node.Children)
            {
                if (child == potentialDescendant || IsDescendant(child, potentialDescendant))
                {
                    return true;
                }
            }
            return false;
        }

        private static string GetUniqueChildName(string desiredName, IEnumerable<PlistElementViewModel> siblings)
        {
            var existingNames = new HashSet<string>(siblings.Select(e => e.ElementName), StringComparer.Ordinal);
            if (!existingNames.Contains(desiredName)) return desiredName;

            int suffix = 2;
            string candidate;
            do
            {
                candidate = $"{desiredName} ({suffix})";
                suffix++;
            } while (existingNames.Contains(candidate));

            return candidate;
        }

        [RelayCommand]
        public void DeleteElement(PlistElementViewModel element)
        {
            if (element != null && LoadedElements.Contains(element))
            {
                // Remove from the actual source collection so the change persists (not just the display view)
                CurrentSource.Remove(element);
                LoadedElements.Remove(element);
                DataChanged?.Invoke();
            }
        }

        [RelayCommand]
        public void EditElement(PlistElementViewModel element)
        {
            if (element != null && LoadedElements.Contains(element))
            {
                if (OpenEditWindowForLeaf(element))
                {
                    DataChanged?.Invoke();
                }
            }
        }

        [RelayCommand(CanExecute = nameof(CanNavigateBack))]
        public void NavigateBack()
        {
            if (_backStack.Count == 0) return;

            // Save current container to forward stack before going back
            _forwardStack.Push(_currentContainer);

            _currentContainer = _backStack.Pop();

            if (_pathSegments.Count > 1)
            {
                _pathSegments.RemoveAt(_pathSegments.Count - 1);
                UpdatePathText();
            }

            RefreshDisplayFromSource();

            NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanNavigateForward))]
        public void NavigateForward()
        {
            if (_forwardStack.Count == 0) return;

            // Save current container to back stack before going forward
            _backStack.Push(_currentContainer);

            _currentContainer = _forwardStack.Pop();

            RefreshDisplayFromSource();

            // Re-append folder label if we're moving forward into known history
            NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanNavigateBack))]
        public void NavigateUp()
        {
            // Moving up one directory level functions identically to Back
            NavigateBack();
        }

        // Attempts to navigate directly to the container described by a typed address such as
        // "Root/ACPI/Add". Returns false (leaving the current location unchanged) if the path
        // doesn't resolve to an existing Dictionary/Array container.
        public bool NavigateToPath(string path)
        {
            var segments = (path ?? string.Empty)
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (segments.Length == 0 || !segments[0].Equals("Root", StringComparison.OrdinalIgnoreCase))
            {
                UpdatePathText();
                return false;
            }

            PlistElementViewModel? target = null;
            IEnumerable<PlistElementViewModel> currentLevel = _rootElements;

            for (int i = 1; i < segments.Length; i++)
            {
                var match = currentLevel.FirstOrDefault(e =>
                    (e.ElementType == PlistElementType.Dictionary || e.ElementType == PlistElementType.Array) &&
                    e.ElementName.Equals(segments[i], StringComparison.OrdinalIgnoreCase));

                if (match == null)
                {
                    UpdatePathText();
                    return false;
                }

                target = match;
                currentLevel = match.Children;
            }

            if (target == _currentContainer && segments.Length > 1)
            {
                // Already at the requested location; nothing to do but keep the path text in sync.
                UpdatePathText();
                return true;
            }

            _backStack.Push(_currentContainer);
            _forwardStack.Clear();

            _pathSegments.Clear();
            _pathSegments.AddRange(segments.Length > 0 ? segments.Select((s, i) => i == 0 ? "Root" : s) : ["Root"]);
            UpdatePathText();

            _currentContainer = target;
            RefreshDisplayFromSource();

            NotifyCanExecuteChanged();

            return true;
        }

        #endregion

        public void Initialize(ObservableCollection<PlistElementViewModel> rootElements, string rootElementType = "dict")
        {
            _rootElements = [.. rootElements];
            _currentContainer = null;
            RootElementType = rootElementType;

            _backStack.Clear();
            _forwardStack.Clear();
            _pathSegments.Clear();

            _pathSegments.Add("Root");
            UpdatePathText();

            RefreshDisplayFromSource();
        }

        // Rebuilds the XML tree from the current (possibly edited/added/deleted) view model hierarchy.
        // This is the authoritative source used when saving, since structural edits (add/delete/paste)
        // are only ever applied to the view model tree (_rootElements / element.Children), not the
        // original XElement document.
        public XElement BuildRootElement()
        {
            var root = new XElement(RootElementType);

            foreach (var element in _rootElements)
            {
                var xml = ToXElement(element);

                if (RootElementType.Equals("array", StringComparison.OrdinalIgnoreCase))
                {
                    var valueOnly = xml.Elements().LastOrDefault();
                    if (valueOnly != null)
                    {
                        root.Add(valueOnly);
                    }
                }
                else
                {
                    root.Add(xml.Elements());
                }
            }

            return root;
        }

        private void RefreshDisplayFromSource()
        {
            LoadedElements.Clear();
            foreach (var element in CurrentSource)
            {
                LoadedElements.Add(element);
            }
        }

        private void UpdatePathText() => CurrentPath = string.Join("/", _pathSegments);

        private static bool OpenEditWindowForLeaf(PlistElementViewModel element)
        {
            // 1. Create a deep or detached copy for editing so changes aren't live until saved
            var tempModel = new PlistElement
            {
                ElementName = element.ElementName,
                ElementType = element.ElementType,
                ElementValue = element.ElementValue,
                NumericSubType = element.Model.NumericSubType,
                RawXElement = element.Model.RawXElement != null ? new XElement(element.Model.RawXElement) : null
            };

            var tempViewModel = new PlistElementViewModel(tempModel);

            var container = new PlistElementViewModel(new PlistElement
            {
                ElementName = tempModel.ElementName,
                ElementType = tempModel.ElementType
            });

            container.Children.Add(tempViewModel);

            var editViewModel = new EditPlistElementViewModel(container);
            var editWindow = new EditPlistElementWindow
            {
                DataContext = editViewModel,
                Owner = Application.Current.MainWindow
            };

            // 2. Wait for the modal dialog to complete
            if (editWindow.ShowDialog() == true)
            {
                // 3. Sync changes from the edited copy back onto the original ViewModel
                element.ElementName = tempViewModel.ElementName;
                element.ElementType = tempViewModel.ElementType;
                element.ElementValue = tempViewModel.ElementValue;
                element.Model.NumericSubType = tempViewModel.Model.NumericSubType;
                element.Model.RawXElement = tempViewModel.Model.RawXElement;
                return true;
            }

            return false;
        }

        private static XElement ToXElement(PlistElementViewModel vm)
        {
            var keyNode = new XElement("key", vm.ElementName);
            XElement valueNode;

            switch (vm.ElementType)
            {
                case PlistElementType.Dictionary:
                    valueNode = new XElement("dict");
                    foreach (var child in vm.Children)
                    {
                        var childXml = ToXElement(child);
                        valueNode.Add(childXml.Elements()); // Appends <key> and value tags
                    }
                    break;

                case PlistElementType.Array:
                    valueNode = new XElement("array");
                    foreach (var child in vm.Children)
                    {
                        var childXml = ToXElement(child);
                        // For arrays, append only the value element (drop the key)
                        valueNode.Add(childXml.Elements().LastOrDefault());
                    }
                    break;

                case PlistElementType.Boolean:
                    valueNode = vm.ElementValue is bool b
                        ? (b ? new XElement("true") : new XElement("false"))
                        : (vm.ElementValue?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true ? new XElement("true") : new XElement("false"));
                    break;

                case PlistElementType.Number:
                    valueNode = new XElement(vm.Model.NumericSubType == "real" ? "real" : "integer", vm.ElementValue);
                    break;

                case PlistElementType.Data:
                    valueNode = new XElement("data", vm.ElementValue is byte[] bytes ? Convert.ToBase64String(bytes) : vm.ElementValue);
                    break;

                default:
                    valueNode = new XElement(vm.ElementType.ToString().ToLower(), vm.ElementValue);
                    break;
            }

            return new XElement("entry", keyNode, valueNode);
        }

        private static PlistElementViewModel? ParseElementFromXml(XElement node)
        {
            // Case 1: Wrapped entry structure generated by ToXElement (<entry><key>...</key><type>...</type></entry>)
            if (node.Name == "entry")
            {
                var keyElement = node.Element("key");
                var valueElement = node.Elements().FirstOrDefault(e => e.Name != "key");

                if (keyElement != null && valueElement != null)
                {
                    return CreateViewModel(keyElement.Value, valueElement);
                }
            }

            // Case 2: Standard standalone Plist key node (<key>MyKey</key>)
            if (node.Name == "key")
            {
                var nextNode = node.ElementsAfterSelf().FirstOrDefault();
                string keyName = node.Value;

                if (nextNode != null)
                {
                    return CreateViewModel(keyName, nextNode);
                }

                // Fallback if no sibling value element is found
                return new PlistElementViewModel(new PlistElement
                {
                    ElementName = keyName,
                    ElementType = PlistElementType.String,
                    ElementValue = string.Empty
                });
            }

            // Case 3: Raw value element copied without a key tag (<string>Value</string>)
            return CreateViewModel("NewElement", node);
        }

        private static PlistElementViewModel CreateViewModel(string keyName, XElement valueElement)
        {
            // Reuse the shared parsing helper so pasted content gets the same correct typing
            // (real bool values, decoded base64 data, preserved integer/real subtype) as loaded files.
            var model = PlistElementViewModel.CreateModelFromNode(keyName, valueElement);

            model.ElementValue = model.ElementType switch
            {
                PlistElementType.Dictionary => $"{valueElement.Elements("key").Count()} items",
                PlistElementType.Array => $"{valueElement.Elements().Count()} items",
                _ => model.ElementValue
            };

            return new PlistElementViewModel(model);
        }

        private void NotifyCanExecuteChanged()
        {
            NavigateBackCommand.NotifyCanExecuteChanged();
            NavigateForwardCommand.NotifyCanExecuteChanged();
            NavigateUpCommand.NotifyCanExecuteChanged();
        }
    }
}
