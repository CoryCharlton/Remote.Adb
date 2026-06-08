using Microsoft.Extensions.DependencyInjection;
using Remote.Adb.Core;
using Remote.Adb.Desktop.Common;
using Remote.Adb.Desktop.Devices;
using Remote.Adb.Desktop.Emulators;
using Remote.Adb.Desktop.Settings;
using Remote.Adb.Desktop.Shell;
using Remote.Adb.Desktop.Theming;
using Remote.Adb.Desktop.Tunnel;

namespace Remote.Adb.Desktop;

/// <summary>
/// Registers the Desktop head's services: the shared Core services, theming/density appliers, dialogs, the page
/// view models and their DI-resolved views, and the device-catalog warm-up background service.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRemoteAdbDesktop(this IServiceCollection services)
    {
        services.AddRemoteAdbCore();

        services.AddSingleton<IThemeApplier, ThemeApplier>();
        services.AddSingleton<IDensityApplier, DensityApplier>();
        services.AddSingleton<IConfirmDialog, ConfirmDialog>();
        services.AddSingleton<IAvdCreateDialog, AvdCreateDialog>();

        services.AddHostedService<DeviceCatalogWarmup>();

        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<EmulatorViewModel>();
        services.AddTransient<DevicesViewModel>();
        services.AddTransient<TunnelViewModel>();
        services.AddTransient<SettingsViewModel>();

        // The four page views the ViewLocator resolves through DI; every other view is instantiated by XAML.
        services.AddTransient<EmulatorView>();
        services.AddTransient<DevicesView>();
        services.AddTransient<TunnelView>();
        services.AddTransient<SettingsView>();

        return services;
    }
}
