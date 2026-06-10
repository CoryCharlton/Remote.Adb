using Microsoft.Extensions.DependencyInjection;
using Remote.Adb.Core;
using Remote.Adb.Core.Emulators;
using Remote.Adb.Desktop.Common;
using Remote.Adb.Desktop.Common.Notifications;
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
        services.AddSingleton<NotificationService>();
        services.AddSingleton<INotificationService>(provider => provider.GetRequiredService<NotificationService>());

        // A fresh details VM per row, with its store resolved from DI and the configuration/back callback passed in.
        services.AddSingleton<EmulatorDetailsViewModelFactory>(provider =>
            (configuration, back) => new EmulatorDetailsViewModel(configuration, provider.GetRequiredService<IAvdConfigStore>(), back));

        // A fresh wizard VM per dialog open (the dialog service is a singleton, so it cannot capture a transient).
        services.AddTransient<CreateAvdViewModel>();
        services.AddTransient<Func<CreateAvdViewModel>>(provider => () => provider.GetRequiredService<CreateAvdViewModel>());

        services.AddHostedService<DeviceCatalogWarmup>();

        services.AddTransient<MainWindow>();
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
