using Microsoft.Extensions.DependencyInjection;
using Remote.Adb.Core.Common;
using Remote.Adb.Core.Diagnostics;
using Remote.Adb.Core.Emulators;
using Remote.Adb.Core.Settings;

namespace Remote.Adb.Core;

/// <summary>
/// Registers the shared <c>Remote.Adb.Core</c> services so both front-ends wire up identically.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRemoteAdbCore(this IServiceCollection services)
    {
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IAndroidSdk, AndroidSdk>();
        services.AddSingleton<IAvdCatalog, AvdCatalog>();
        services.AddSingleton<IAvdConfigStore, AvdConfigStore>();
        services.AddSingleton<IAvdProvisioningService, AvdProvisioningService>();
        services.AddSingleton<IEmulatorService, EmulatorService>();
        services.AddSingleton<ISdkDiagnostics, SdkDiagnostics>();
        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<ISettingsService, SettingsService>();

        return services;
    }
}
