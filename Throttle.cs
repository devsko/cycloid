using System;
using System.Threading.Tasks;

namespace cycloid;

public class Throttle<TValue, TState>(Func<TValue, TState, Task> action, TimeSpan delay)
{
    private readonly Func<TValue, TState, Task> _action = action;
    private readonly TimeSpan _delay = delay;
    private volatile bool _isBusy;
    private volatile bool _hasValue;
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

            do
            {
                _value = default;
                _hasValue = false;
                _isBusy = true;

                try
                {
                    await _action(value, state);
                }
                catch (Exception ex)
                {
                    await App.Current.ShowExceptionAsync(ex);
                }

                await Task.Delay(_delay);

                _isBusy = false;
                value = _value;
            }
            while (_hasValue);
        }
    }
}
