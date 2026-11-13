using System;
using System.Threading;

namespace RadioFlow.App.Services;

/// <summary>
/// Tek örnek garantisi. İkinci örnek açılırsa ilk örneğe sinyal gönderilir ve ilk örnek widget'ı gösterir.
/// Bekleme ThreadPool.RegisterWaitForSingleObject ile yapılır (polling yok).
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = @"Local\RadioFlow.SingleInstance.Mutex";
    private const string EventName = @"Local\RadioFlow.SingleInstance.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activateEvent;
    private RegisteredWaitHandle? _registration;

    public SingleInstanceGuard()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsFirstInstance = createdNew;
        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
    }

    public bool IsFirstInstance { get; }

    public void SignalFirstInstance() => _activateEvent.Set();

    public void ListenForActivation(Action onActivate)
    {
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            (_, _) => onActivate(),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _activateEvent.Dispose();
        if (IsFirstInstance)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Farklı iş parçacığından bırakma denemesi; işlem kapanırken önemsiz.
            }
        }

        _mutex.Dispose();
    }
}
