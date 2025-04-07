namespace cycloid;

public class PeriodicAction<TSender, TParameter>(Action<TSender, TParameter> action, TimeSpan interval)
{
    private CancellationTokenSource _cts;

    public bool IsRunning => _cts is not null;

    public void Start(TSender sender, TParameter parameter)
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
                    action(sender, parameter);
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