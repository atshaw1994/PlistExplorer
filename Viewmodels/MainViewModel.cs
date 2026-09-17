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

    public PlistElementContainerViewModel ContainerViewModel { get; } = new();
    public XElement LoadedElement { get; set; } = new("Root");
    public ObservableCollection<string> RecentFiles { get; } = [];
    public bool CanSavePlist() => LoadedElement != null && _currentFilePath != null;

    [ObservableProperty] public partial string WindowTitle { get; set; } = "PlistExplorer";

    public MainViewModel(IRecentFilesService recentFilesService)
    {
        _recentFilesService = recentFilesService;

        // Load persisted files on startup
        var loaded = _recentFilesService.LoadRecentFiles();
        foreach (var path in loaded)
        {
            RecentFiles.Add(path);
        }
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
            try
            {
                _currentFilePath = dialog.FileName;
                LoadedElement = XElement.Load(_currentFilePath);

                PopulateElements();

                WindowTitle = "PlistExplorer - " + Path.GetFileName(_currentFilePath);
                AddRecentFile(_currentFilePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load plist file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void OpenFile(string filePath)
    {
        try
        {
            _currentFilePath = filePath;
            LoadedElement = XElement.Load(_currentFilePath);

            PopulateElements();

            WindowTitle = "PlistExplorer - " + Path.GetFileName(_currentFilePath);
            AddRecentFile(_currentFilePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load plist file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

        try
        {
            _currentFilePath = filePath;
            LoadedElement = XElement.Load(_currentFilePath);

            PopulateElements();

            WindowTitle = "PlistExplorer - " + Path.GetFileName(_currentFilePath);
            AddRecentFile(_currentFilePath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load plist file:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanSavePlist))]
    public void SavePlist()
    {
        if (LoadedElement == null || _currentFilePath == null) return;

        try
        {
            LoadedElement.Save(_currentFilePath);
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
        if (LoadedElement == null) return;

        if (string.IsNullOrEmpty(_currentFilePath))
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Property List (*.plist)|*.plist|XML Files (*.xml)|*.xml|All Files (*.*)|*.*",
                Title = "Save Plist File",
                FileName = "document.plist"
            };

            if (dialog.ShowDialog() != true) return;
            _currentFilePath = dialog.FileName;
        }

        try
        {
            LoadedElement.Save(_currentFilePath);
            MessageBox.Show("File saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public static void Exit() => Application.Current.Shutdown();

    public void PopulateElements()
    {
        var initialElements = new ObservableCollection<PlistElementViewModel>();

        XElement? targetRoot = LoadedElement.Name.LocalName == "plist"
            ? LoadedElement.Elements().FirstOrDefault()
            : LoadedElement;

        if (targetRoot != null)
        {
            ParseContainerChildren(targetRoot, initialElements);
        }

        ContainerViewModel.Initialize(initialElements);
    }

    private void ParseContainerChildren(XElement containerElement, ObservableCollection<PlistElementViewModel> targetCollection)
    {
        PlistElementType containerType = GetPlistElementType(containerElement);

        if (containerType == PlistElementType.Dictionary)
        {
            var nodes = containerElement.Elements().ToList();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Name.LocalName == "key")
                {
                    string childKey = nodes[i].Value;
                    if (i + 1 < nodes.Count)
                    {
                        XElement valueElement = nodes[i + 1];
                        AddElementToCollection(valueElement, childKey, targetCollection);
                        i++;
                    }
                }
            }
        }
        else if (containerType == PlistElementType.Array)
        {
            int index = 0;
            foreach (XElement childElement in containerElement.Elements())
            {
                AddElementToCollection(childElement, $"Item {index++}", targetCollection);
            }
        }
    }

    private void AddElementToCollection(XElement element, string keyName, ObservableCollection<PlistElementViewModel> targetCollection)
    {
        PlistElementType type = GetPlistElementType(element);

        var model = new PlistElement
        {
            ElementName = keyName,
            ElementType = type,
            ElementValue = type switch
            {
                PlistElementType.Boolean => element.Name.LocalName.Equals("true", StringComparison.OrdinalIgnoreCase),
                PlistElementType.Number => long.TryParse(element.Value, out var i) ? i : 0L,
                PlistElementType.Data => element.Value.Trim(),
                _ => element.Value
            }
        };

        var viewModel = new PlistElementViewModel(model);
        targetCollection.Add(viewModel);

        if (type == PlistElementType.Dictionary || type == PlistElementType.Array)
        {
            ParseContainerChildren(element, viewModel.Children);
        }
    }

    private static PlistElementType GetPlistElementType(XElement element) => element.Name.LocalName.ToLowerInvariant() switch
    {
        "dict" => PlistElementType.Dictionary,
        "array" => PlistElementType.Array,
        "string" => PlistElementType.String,
        "integer" or "real" => PlistElementType.Number,
        "true" or "false" => PlistElementType.Boolean,
        "date" => PlistElementType.Date,
        "data" => PlistElementType.Data,
        "uid" => PlistElementType.UID,
        _ => PlistElementType.String
    };

    private void LoadSampleData()
    {
        var samplePlist = new XElement("plist",
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

        LoadedElement = samplePlist;
        PopulateElements(); // This parses elements and calls ContainerViewModel.Initialize()
    }

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