using System;
using System.Windows.Threading;

namespace RadioFlow.App.Services;

/// <summary>Arka plan olaylarını UI iş parçacığına güvenli şekilde aktarır.</summary>
public interface IUiDispatcher
{
    void Post(Action action);
}

public sealed class WpfUiDispatcher(Dispatcher dispatcher) : IUiDispatcher
{
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.InvokeAsync(action, DispatcherPriority.Normal);
        }
    }
}
