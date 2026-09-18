using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PlistExplorer.Models;
using PlistExplorer.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Xml.Linq;

namespace PlistExplorer.Viewmodels;

public partial class MainViewModel : ObservableObject
{
    private string? _currentFilePath;
    private readonly IRecentFilesService _recentFilesService;
    private string _baseWindowTitle = "PlistExplorer";

    // The full XML document (declaration + DOCTYPE + root) for the currently loaded/created plist.
    // Using XDocument instead of a bare XElement preserves the <?xml ... ?> declaration and the
    // Apple plist DOCTYPE across load/save, which XElement.Load/Save silently discard.
    private XDocument? _document;

    public PlistElementContainerViewModel ContainerViewModel { get; } = new();
    public XElement? LoadedElement => _document?.Root;
    public ObservableCollection<string> RecentFiles { get; } = [];
    public bool CanSavePlist() => _document != null;

    [ObservableProperty] public partial string WindowTitle { get; set; } = "PlistExplorer";
    [ObservableProperty] public partial bool HasUnsavedChanges { get; set; }

    partial void OnHasUnsavedChangesChanged(bool value) => UpdateWindowTitle();

    private void UpdateWindowTitle() => WindowTitle = _baseWindowTitle + (HasUnsavedChanges ? " *" : "");

    public MainViewModel(IRecentFilesService recentFilesService)
    {
        _recentFilesService = recentFilesService;

        // Load persisted files on startup
        var loaded = _recentFilesService.LoadRecentFiles();
        foreach (var path in loaded)
            RecentFiles.Add(path);

        // Track unsaved changes whenever the element tree is structurally or value-modified
        ContainerViewModel.DataChanged += () => HasUnsavedChanges = true;
    }

    // Parameterless fallback constructor for WPF/XAML default instantiation
    public MainViewModel() : this(new RecentFilesService()) 
    {
        // Initialize design data
        if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(new DependencyObject()))
        {
            LoadSampleData();
        }
    }

    [RelayCommand]
    public void OpenPlist()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Property List (*.plist)|*.plist|XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
            Title = "Open Plist File"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadPlistFile(dialog.FileName);
        }
    }

    [RelayCommand]
    public void OpenFile(string filePath)
    {
        LoadPlistFile(filePath);
    }

    [RelayCommand]
    public void OpenRecentFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            MessageBox.Show($"File no longer exists at:\n{filePath}", "File Not Found",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            RecentFiles.Remove(filePath);
            return;
        }

        LoadPlistFile(filePath);
    }

    // Shared load logic for OpenPlist/OpenFile/OpenRecentFile: loads the document, refreshes the
    // view models, updates the window title, and records the recent-files entry.
    private void LoadPlistFile(string filePath)
    {
        try
        {
            _currentFilePath = filePath;
            _document = XDocument.Load(filePath);

            PopulateElements();

            _baseWindowTitle = "PlistExplorer - " + Path.GetFileName(_currentFilePath);
            HasUnsavedChanges = false;
            UpdateWindowTitle();
            AddRecentFile(_currentFilePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load plist file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSavePlist))]
    public void SavePlist()
    {
        if (_document == null || _currentFilePath == null) return;

        try
        {
            SyncDocumentFromViewModel();
            _document.Save(_currentFilePath);
            HasUnsavedChanges = false;
            MessageBox.Show("File saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSavePlist))]
    public void SavePlistAs()
    {
        if (_document == null) return;

        var dialog = new SaveFileDialog
        {
            Filter = "Property List (*.plist)|*.plist|XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
            Title = "Save Plist File",
            FileName = "document.plist"
        };

        if (dialog.ShowDialog() != true) return;
        _currentFilePath = dialog.FileName;

        try
        {
            SyncDocumentFromViewModel();
            _document.Save(_currentFilePath);
            _baseWindowTitle = "PlistExplorer - " + Path.GetFileName(_currentFilePath);
            HasUnsavedChanges = false;
            UpdateWindowTitle();
            MessageBox.Show("File saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // Rebuilds the document's root contents from the (possibly edited/added/deleted) view model tree.
    // Structural edits (add/delete/paste) are only ever applied to the ContainerViewModel's view
    // model hierarchy, not to the original XML document, so this must run before every save.
    private void SyncDocumentFromViewModel()
    {
        if (_document == null) return;

        var rebuiltRoot = ContainerViewModel.BuildRootElement();
        var root = _document.Root;

        if (root != null && root.Name.LocalName == "plist")
        {
            var existingRoot = root.Elements().FirstOrDefault();
            if (existingRoot != null)
            {
                existingRoot.ReplaceWith(rebuiltRoot);
            }
            else
            {
                root.Add(rebuiltRoot);
            }
        }
        else
        {
            _document.ReplaceNodes(rebuiltRoot);
        }
    }

    [RelayCommand]
    public static void Exit() => Application.Current.Shutdown();

    public void PopulateElements()
    {
        var initialElements = new ObservableCollection<PlistElementViewModel>();

        XElement? root = _document?.Root;
        XElement? targetRoot = root?.Name.LocalName == "plist"
            ? root.Elements().FirstOrDefault()
            : root;

        if (targetRoot != null)
        {
            ParseContainerChildren(targetRoot, initialElements);
        }

        ContainerViewModel.Initialize(initialElements, targetRoot?.Name.LocalName ?? "dict");

        // Refresh command states for Save / SaveAs UI buttons
        SavePlistCommand.NotifyCanExecuteChanged();
        SavePlistAsCommand.NotifyCanExecuteChanged();
    }

    private void ParseContainerChildren(XElement containerElement, ObservableCollection<PlistElementViewModel> targetCollection)
    {
        var childElements = containerElement.Elements().ToList();

        if (containerElement.Name.LocalName.Equals("dict", StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 0; i < childElements.Count; i++)
            {
                var keyElement = childElements[i];

                if (keyElement.Name.LocalName.Equals("key", StringComparison.OrdinalIgnoreCase))
                {
                    // Ensure a value element actually exists after the key tag
                    if (i + 1 < childElements.Count)
                    {
                        var valueElement = childElements[i + 1];
                        AddElementToCollection(valueElement, keyElement.Value, targetCollection);
                        i++; // Skip the value element on the next iteration
                    }
                    else
                    {
                        // Handle orphan key gracefully
                        break;
                    }
                }
            }
        }
        else if (containerElement.Name.LocalName.Equals("array", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var element in childElements)
            {
                AddElementToCollection(element, "Item", targetCollection);
            }
        }
    }

    private void AddElementToCollection(XElement element, string keyName, ObservableCollection<PlistElementViewModel> targetCollection)
    {
        // Delegates to the shared helper so booleans, base64 data, and integer/real numbers
        // are parsed with correct, consistent types across the whole app.
        var model = PlistElementViewModel.CreateModelFromNode(keyName, element);

        // Containers display an item-count summary instead of their (irrelevant) raw text value
        model.ElementValue = model.ElementType switch
        {
            PlistElementType.Dictionary => $"{element.Elements("key").Count()} items",
            PlistElementType.Array => $"{element.Elements().Count()} items",
            _ => model.ElementValue
        };

        // The PlistElementViewModel constructor handles parsing child elements via model.RawXElement
        var viewModel = new PlistElementViewModel(model);
        targetCollection.Add(viewModel);
    }

    private void LoadSampleData()
    {
        var samplePlist = new XElement("plist", new XAttribute("version", "1.0"),
            new XElement("dict",
                new XElement("key", "SampleDict"),
                new XElement("dict",
                    new XElement("key", "ChildKey"),
                    new XElement("string", "ChildValue")
                ),
                new XElement("key", "SampleArray"),
                new XElement("array",
                    new XElement("string", "ArrayValue1"),
                    new XElement("string", "ArrayValue2")
                ),
                new XElement("key", "SampleString"),
                new XElement("string", "Hello World"),
                new XElement("key", "SampleNumber"),
                new XElement("integer", "42"),
                new XElement("key", "SampleBoolean"),
                new XElement("true"),
                new XElement("key", "SampleDate"),
                new XElement("date", "2026-09-17T11:32:00Z"),
                new XElement("key", "SampleData"),
                new XElement("data", "SGVsbG8gV29ybGQ="),
                new XElement("key", "SampleUID"),
                new XElement("uid", "100")
            )
        );

        _document = CreatePlistDocument(samplePlist);
        PopulateElements(); // This parses elements and calls ContainerViewModel.Initialize()
    }

    // Builds a standard plist XDocument (XML declaration + Apple DOCTYPE) around the given root.
    private static XDocument CreatePlistDocument(XElement root) => new(
        new XDeclaration("1.0", "UTF-8", null),
        new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
        root);

    private void AddRecentFile(string filePath)
    {
        RecentFiles.Remove(filePath);

        RecentFiles.Insert(0, filePath);

        while (RecentFiles.Count > 5)
        {
            RecentFiles.RemoveAt(RecentFiles.Count - 1);
        }

        // Persist to disk via service
        _recentFilesService.SaveRecentFiles(RecentFiles);
    }
}