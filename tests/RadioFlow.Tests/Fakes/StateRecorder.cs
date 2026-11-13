using RadioFlow.Core.Enums;
using RadioFlow.Core.Interfaces;
using RadioFlow.Core.Models;

namespace RadioFlow.Tests.Fakes;

/// <summary>Oynatıcı durum geçişlerini kaydeder ve belirli bir duruma ulaşılmasını bekler.</summary>
internal sealed class StateRecorder
{
    private readonly IRadioPlayerService _player;
    private readonly List<PlayerStateChangedEventArgs> _events = [];

    public StateRecorder(IRadioPlayerService player)
    {
        _player = player;
        player.StateChanged += (_, e) =>
        {
            lock (_events)
            {
                _events.Add(e);
            }
        };
    }

    public IReadOnlyList<PlayerState> States
    {
        get
        {
            lock (_events)
            {
                return _events.Select(e => e.NewState).ToList();
            }
        }
    }

    public IReadOnlyList<PlayerStateChangedEventArgs> Events
    {
        get
        {
            lock (_events)
            {
                return _events.ToList();
            }
        }
    }

    public async Task WaitForAsync(PlayerState state, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (_player.CurrentState != state)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Beklenen durum {state}, mevcut {_player.CurrentState}. Geçişler: {string.Join(" → ", States)}");
            }

            await Task.Delay(10);
        }
    }
}
