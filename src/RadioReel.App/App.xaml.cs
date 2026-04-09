using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Windows;

namespace RadioReel.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Serilog — init before anything else
        Log.Logger = Infrastructure.Logging.LoggingConfiguration.CreateLogger();
        Log.Information("RadioReel starting");

        // Global error handlers
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Unhandled UI exception");
            MessageBox.Show(
                $"Помилка: {args.Exception.Message}",
                "RadioReel", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Unobserved task exception");
            args.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                Log.Fatal(ex, "Fatal unhandled exception");
            Log.CloseAndFlush();
        };

        // DI
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Show main window
        var mainWindow = _serviceProvider.GetRequiredService<UI.Views.MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<Core.Storage.SettingsStore>();
        services.AddSingleton<UI.ViewModels.MainViewModel>();
        services.AddSingleton<UI.ViewModels.StreamsViewModel>();
        services.AddSingleton<UI.Views.MainWindow>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        Log.Information("RadioReel shutting down");

        var mainVm = _serviceProvider?.GetService<UI.ViewModels.MainViewModel>();
        if (mainVm is not null)
            await mainVm.ShutdownAsync();

        Log.CloseAndFlush();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
