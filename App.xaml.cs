using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PlistExplorer.Services;
using PlistExplorer.Viewmodels;
using PlistExplorer.Views;

namespace PlistExplorer;

public partial class App : Application
{
    public new static App Current => (App)Application.Current;

    public IServiceProvider Services { get; }

    public App()
    {
        Services = ConfigureServices();
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Services
        services.AddSingleton<IRecentFilesService, RecentFilesService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<PlistElementContainerViewModel>();
        services.AddTransient<PlistElementViewModel>();

        // Views
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var mainWindow = Services.GetRequiredService<MainWindow>();
            var mainVm = (MainViewModel)mainWindow.DataContext; // Resolve instance tied to Window

            if (e.Args.Length > 0)
            {
                string filePath = e.Args[0].Trim('"');
                if (System.IO.File.Exists(filePath))
                {
                    mainVm.OpenFile(filePath);
                }
            }

            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup Error: {ex.Message}", "PlistExplorer Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}