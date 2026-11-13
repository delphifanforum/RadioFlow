using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RadioFlow.App.Interop;
using RadioFlow.Core.Interfaces;
using RadioFlow.Infrastructure.WindowsIntegration;
using Windows.ApplicationModel;

namespace RadioFlow.App.Services;

/// <summary>
/// Dağıtım modeline göre doğru başlangıç entegrasyonunu seçer:
/// MSIX paketli → Windows.ApplicationModel.StartupTask (manifestte "RadioFlowStartup" görevi gerekir),
/// bağımsız EXE → HKCU\...\Run kaydı.
/// </summary>
public sealed class StartupServiceSelector(RegistryStartupService registry, ILogger<StartupServiceSelector> logger) : IStartupService
{
    public const string StartupTaskId = "RadioFlowStartup";

    private readonly bool _packaged = NativeMethods.IsPackaged();

    public bool IsPackaged => _packaged;

    public bool IsSupported => _packaged || registry.IsSupported;

    public async Task<bool> IsEnabledAsync()
    {
        if (!_packaged)
        {
            return await registry.IsEnabledAsync().ConfigureAwait(false);
        }

        try
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "StartupTask durumu okunamadı.");
            return false;
        }
    }

    public async Task<bool> SetEnabledAsync(bool enabled)
    {
        if (!_packaged)
        {
            return await registry.SetEnabledAsync(enabled).ConfigureAwait(false);
        }

        try
        {
            var task = await StartupTask.GetAsync(StartupTaskId);
            if (enabled)
            {
                var state = await task.RequestEnableAsync();
                return state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
            }

            task.Disable();
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "StartupTask değiştirilemedi.");
            return false;
        }
    }
}
