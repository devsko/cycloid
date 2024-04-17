using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;

namespace cycloid.Routing;

partial class RouteBuilder
{
    public class ChangeLocker()
    {
        public readonly struct Releaser(ChangeLocker changeLock, bool calculation) : IDisposable
        {
            public void Dispose()
            {
                if (calculation)
                {
                    changeLock.ReleaseCalculation();
                }
                else
                {
                    changeLock.Release();
                }
            }
        }

        private readonly SemaphoreSlim _semaphore = new(1);
        private volatile int _runningCalculationCounter;

        public async Task<Releaser> EnterCalculationAsync(CancellationToken cancellationToken = default)
        {
            StrongReferenceMessenger.Default.Send(new RouteChanging());

            if (_runningCalculationCounter == 0)
            {
                await _semaphore.WaitAsync(cancellationToken);
            }
            _runningCalculationCounter++;

            return new Releaser(this, true);
        }

        public async Task<Releaser> EnterAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);

            return new Releaser(this, false);
        }

        private void ReleaseCalculation()
        {
            _runningCalculationCounter--;
            if (_runningCalculationCounter == 0)
            {
                _semaphore.Release();

                StrongReferenceMessenger.Default.Send(new RouteChanged(false));
            }
        }

        private void Release()
        {
            _semaphore.Release();
        }
    }
}