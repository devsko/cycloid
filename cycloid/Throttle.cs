using System;
using System.Threading;
using System.Threading.Tasks;

namespace cycloid;

public class Throttle<TValue, TState>(Action<TValue, TState> action, TimeSpan delay)
{
    private readonly Action<TValue, TState> _action = action;
    private readonly TimeSpan _delay = delay;
    private bool _isBusy;
    private bool _hasValue;
    private TValue _value;

    public void Next(TValue value, TState state)
    {
        NextAsync(value, state).FireAndForget();

        async Task NextAsync(TValue value, TState state)
        {
            if (_isBusy)
            {
                _value = value;
                _hasValue = true;
                return;
            }

            _isBusy = true;
            
            do
            {
                _value = default;
                _hasValue = false;

                _action(value, state);
                
                await Task.Delay(_delay);

                value = _value;
            }
            while (_hasValue);
            
            _isBusy = false;
        }
    }

    public void Clear()
    {
        _hasValue = false;
        _value = default;
    }
}

public class AsyncThrottle<TValue, TState>(Func<TValue, TState, CancellationToken, Task> action, TimeSpan? delay = null)
{
    private readonly Func<TValue, TState, CancellationToken, Task> _action = action;
    private readonly TimeSpan _delay = delay ?? TimeSpan.Zero;
    private volatile bool _isBusy;
    private volatile bool _hasValue;
    private CancellationTokenSource _cts = new();
    private TValue _value;

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

                if (_delay != TimeSpan.Zero)
                {
                    // Do NOT cancel the delay when another Next() happens. That's the throttling.
                    await Task.Delay(_delay);
                }

                _isBusy = false;
                value = _value;
            }
            while (_hasValue);
        }
    }

    public void Clear()
    {
        _hasValue = false;
        _value = default;
        _cts.Cancel();
    }
}
