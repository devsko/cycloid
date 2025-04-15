namespace cycloid;

public class PeriodicAction<TSender, TAmount, TParameter>(Action<TSender, TAmount> scroll, Func<TSender, TAmount, TParameter> convertAmount, TimeSpan interval)
{
    private CancellationTokenSource _cts;

    public bool IsRunning => _cts is not null;

    public void Start<TState>(TSender sender, TAmount amount, Action<TSender, TState, TParameter> payload, TState state)
    {
        if (!IsRunning)
        {
            RunAsync().FireAndForget();
        }

        async Task RunAsync()
        {
            _cts = new CancellationTokenSource();
            try
            {
                while (true)
                {
                    scroll(sender, amount);
                    payload(sender, state, convertAmount(sender, amount));
                    await Task.Delay(interval, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                _cts = null;
            }
        }
    }

    public void Stop()
    {
        if (IsRunning)
        {
            _cts.Cancel();
        }
    }
}