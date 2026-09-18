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
        private readonly Stack<List<PlistElementViewModel>> _backStack = new();
        private readonly Stack<List<PlistElementViewModel>> _forwardStack = new();
        private readonly List<string> _pathSegments = [];
        private bool CanNavigateBack() => _backStack.Count > 0;
        private bool CanNavigateForward() => _forwardStack.Count > 0;

        public ObservableCollection<PlistElementViewModel> LoadedElements { get; } = [];

        [ObservableProperty] public partial string CurrentPath { get; set; } = "Root";

        public void Initialize(ObservableCollection<PlistElementViewModel> rootElements)
        {
            LoadedElements.Clear();
            _backStack.Clear();
            _forwardStack.Clear();
            _pathSegments.Clear();

            _pathSegments.Add("Root");
            UpdatePathText();

            foreach (var element in rootElements)
            {
                LoadedElements.Add(element);
            }
        }

        [RelayCommand]
        public void OpenElement(PlistElementViewModel element)
        {
            bool isContainer = element.ElementType == PlistElementType.Dictionary ||
                               element.ElementType == PlistElementType.Array;

            if (isContainer)
            {
                // Snapshot active view onto back stack and clear forward history on new drill-down
                _backStack.Push([.. LoadedElements]);
                _forwardStack.Clear();

                _pathSegments.Add(element.ElementName);
                UpdatePathText();

                LoadedElements.Clear();
                foreach (var child in element.Children)
                {
                    LoadedElements.Add(child);
                }

                NotifyCanExecuteChanged();
            }
            else
            {
                OpenEditWindowForLeaf(element);
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
        }

        [RelayCommand]
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
                        LoadedElements.Add(elementViewModel);
                    }
                }
            }
            catch
            {
                // Fail quietly or log if the text on clipboard isn't valid XML
            }
        }

        [RelayCommand]
        public void AddNewElement(string elementType)
        {
            switch (elementType)
            {
                case "Dict":
                    LoadedElements.Add(new PlistElementViewModel(new PlistElement
                    {
                        ElementName = "NewDictionary",
                        ElementType = PlistElementType.Dictionary
                    }));
                    break;
                case "Array":
                    LoadedElements.Add(new PlistElementViewModel(new PlistElement
                    {
                        ElementName = "NewArray",
                        ElementType = PlistElementType.Array
                    }));
                    break;
                case "Boolean":
                    LoadedElements.Add(new PlistElementViewModel(new PlistElement
                    {
                        ElementName = "NewBoolean",
                        ElementType = PlistElementType.Boolean,
                        ElementValue = false
                    }));
                    break;
                case "Data":
                    LoadedElements.Add(new PlistElementViewModel(new PlistElement
                    {
                        ElementName = "NewData",
                        ElementType = PlistElementType.Data,
                        ElementValue = string.Empty
                    }));
                    break;
                case "Date":
                    LoadedElements.Add(new PlistElementViewModel(new PlistElement
                    {
                        ElementName = "NewDate",
                        ElementType = PlistElementType.Date,
                        ElementValue = DateTime.Now
                    }));
                    break;
                case "Number":
                    LoadedElements.Add(new PlistElementViewModel(new PlistElement
                    {
                        ElementName = "NewNumber",
                        ElementType = PlistElementType.Number,
                        ElementValue = 0
                    }));
                    break;
                case "UID":
                    LoadedElements.Add(new PlistElementViewModel(new PlistElement
                    {
                        ElementName = "NewUID",
                        ElementType = PlistElementType.UID,
                        ElementValue = string.Empty
                    }));
                    break;
                case "String":
                    LoadedElements.Add(new PlistElementViewModel(new PlistElement
                    {
                        ElementName = "NewString",
                        ElementType = PlistElementType.String,
                        ElementValue = string.Empty
                    }));
                    break;
                default:
                    break;
            }
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

        [RelayCommand]
        public void DeleteElement(PlistElementViewModel element)
        {
            if (element != null && LoadedElements.Contains(element))
            {
                LoadedElements.Remove(element);
            }
        }

        [RelayCommand]
        public void EditElement(PlistElementViewModel element)
        {
            if (element != null && LoadedElements.Contains(element))
            {
                OpenEditWindowForLeaf(element);
            }
        }

        [RelayCommand(CanExecute = nameof(CanNavigateBack))]
        public void NavigateBack()
        {
            if (_backStack.Count == 0) return;

            // Save current view state to forward stack before going back
            _forwardStack.Push([.. LoadedElements]);

            var previousState = _backStack.Pop();

            if (_pathSegments.Count > 1)
            {
                _pathSegments.RemoveAt(_pathSegments.Count - 1);
                UpdatePathText();
            }

            LoadedElements.Clear();
            foreach (var item in previousState)
            {
                LoadedElements.Add(item);
            }

            NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanNavigateForward))]
        public void NavigateForward()
        {
            if (_forwardStack.Count == 0) return;

            // Save current view state to back stack before going forward
            _backStack.Push([.. LoadedElements]);

            var nextState = _forwardStack.Pop();

            LoadedElements.Clear();
            foreach (var item in nextState)
            {
                LoadedElements.Add(item);
            }

            // Re-append folder label if we're moving forward into known history
            NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanNavigateBack))]
        public void NavigateUp()
        {
            // Moving up one directory level functions identically to Back
            NavigateBack();
        }

        private void UpdatePathText() => CurrentPath = string.Join("/", _pathSegments);

        private static void OpenEditWindowForLeaf(PlistElementViewModel element)
        {
            // 1. Create a deep or detached copy for editing so changes aren't live until saved
            var tempModel = new PlistElement
            {
                ElementName = element.ElementName,
                ElementType = element.ElementType,
                ElementValue = element.ElementValue,
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
                element.Model.RawXElement = tempViewModel.Model.RawXElement;
            }
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
                    valueNode = vm.ElementValue!.ToString()!.Equals("true", StringComparison.CurrentCultureIgnoreCase) ? new XElement("true") : new XElement("false");
                    break;

                case PlistElementType.Number:
                    valueNode = new XElement("integer", vm.ElementValue);
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
            var (type, value) = MapXmlToElementType(valueElement);

            var model = new PlistElement
            {
                ElementName = keyName,
                ElementType = type,
                ElementValue = value,
                // Store the raw XML node so nested children are preserved
                RawXElement = valueElement
            };

            return new PlistElementViewModel(model);
        }

        private static (PlistElementType Type, string Value) MapXmlToElementType(XElement element)
        {
            return element.Name.LocalName.ToLower() switch
            {
                "string" => (PlistElementType.String, element.Value),
                "integer" => (PlistElementType.Number, element.Value),
                "real" => (PlistElementType.Number, element.Value),
                "true" => (PlistElementType.Boolean, "true"),
                "false" => (PlistElementType.Boolean, "false"),
                "date" => (PlistElementType.Date, element.Value),
                "data" => (PlistElementType.Data, element.Value),
                "uid" => (PlistElementType.UID, element.Value),
                "array" => (PlistElementType.Array, $"{element.Elements().Count()} items"),
                "dict" => (PlistElementType.Dictionary, $"{element.Elements("key").Count()} items"),
                _ => (PlistElementType.String, element.Value)
            };
        }

        private void NotifyCanExecuteChanged()
        {
            NavigateBackCommand.NotifyCanExecuteChanged();
            NavigateForwardCommand.NotifyCanExecuteChanged();
            NavigateUpCommand.NotifyCanExecuteChanged();
        }
    }
}
