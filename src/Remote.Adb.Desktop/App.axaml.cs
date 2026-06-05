using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Remote.Adb.Core;
using Remote.Adb.Core.Settings;
using Remote.Adb.Desktop.Common;
using Remote.Adb.Desktop.Devices;
using Remote.Adb.Desktop.Emulators;
using Remote.Adb.Desktop.Settings;
using Remote.Adb.Desktop.Shell;
using Remote.Adb.Desktop.Theming;
using Remote.Adb.Desktop.Tunnel;

namespace Remote.Adb.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRemoteAdbCore();
        services.AddSingleton<IThemeApplier, ThemeApplier>();
        services.AddSingleton<IDensityApplier, DensityApplier>();
        services.AddSingleton<IConfirmDialog, ConfirmDialog>();
        services.AddSingleton<IAvdCreateDialog, AvdCreateDialog>();
        services.AddSingleton<IHostedService, DeviceCatalogWarmup>();
        services.AddTransient<EmulatorViewModel>();
        services.AddTransient<DevicesViewModel>();
        services.AddTransient<TunnelViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<MainWindowViewModel>();

        var provider = services.BuildServiceProvider();

        // Apply the persisted theme and density on launch so the app honors the saved preferences.
        var settings = provider.GetRequiredService<ISettingsService>();
        provider.GetRequiredService<IThemeApplier>().Apply(settings.Theme);
        provider.GetRequiredService<IDensityApplier>().Apply(settings.Density);

        // Run the background services (e.g. device-catalog warm-up). No generic Host here, so start them on
        // the manually-built provider and stop them when the desktop lifetime shuts down.
        var hostedServices = provider.GetServices<IHostedService>().ToArray();
        foreach (var hostedService in hostedServices)
        {
            _ = hostedService.StartAsync(CancellationToken.None);
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested += (_, _) =>
            {
                foreach (var hostedService in hostedServices)
                {
                    _ = hostedService.StopAsync(CancellationToken.None);
                }
            };

            desktop.MainWindow = new MainWindow
            {
                DataContext = provider.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
