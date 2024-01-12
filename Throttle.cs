using System;
using System.Threading;
using System.Threading.Tasks;

namespace cycloid;

public class Throttle<TValue, TState>(Func<TValue, TState, CancellationToken, Task> action, TimeSpan delay)
{
    private readonly Func<TValue, TState, CancellationToken, Task> _action = action;
    private readonly TimeSpan _delay = delay;
    private volatile bool _isBusy;
    private volatile bool _hasValue;
    private CancellationTokenSource _cts = new();
    private TValue _value;

    public Throttle(Action<TValue, TState> action, TimeSpan delay)
        : this((value, state, _) =>
        {
            action(value, state);
            return Task.CompletedTask;
        }, delay)
    { }

    public void Next(TValue value, TState state)
    {
        NextAsync(value, state).FireAndForget();

        async Task NextAsync(TValue value, TState state)
        {
            if (_isBusy)
            {
                _cts.Cancel();
                _value = value;
                _hasValue = true;
                return;
            }

            do
            {
                if (_cts.IsCancellationRequested)
                {
                    _cts = new CancellationTokenSource();
                }

                _value = default;
                _hasValue = false;
                _isBusy = true;

                try
                {
                    await _action(value, state, _cts.Token);
                }
                catch (OperationCanceledException)
                { }
                catch (Exception ex)
                {
                    await App.Current.ShowExceptionAsync(ex);
                }
                
                // Do NOT cancel the delay when another Next() happens. That's the throttling.
                await Task.Delay(_delay);

                _isBusy = false;
                value = _value;
            }
            while (_hasValue);
        }
    }

    internal void Clear()
    {
        _hasValue = false;
        _value = default;
        _cts.Cancel();
    }
}
