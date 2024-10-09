using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Windows.UI.Xaml;

namespace cycloid;

public class PlayerStatusChanged(bool playing) : ValueChangedMessage<bool>(playing);

partial class ViewModel
{
    private const int FrameInterval = 20;

    private float _playerSpeed = 10f;
    private Track.RootPin _positionPin;

    public TrackPoint CameraPoint { get; set; }

    public bool IsPlaying => PlayCommand.CanBeCanceled;

    [RelayCommand(CanExecute = nameof(CanPlay), IncludeCancelCommand = true)]
    public async Task PlayAsync(CancellationToken cancellationToken)
    {
        if (!CanPlay())
        {
            return;
        }

        if (!CurrentPoint.IsValid || CurrentPoint == Track.Points.Last())
        {
            CurrentPoint = Track.Points.First();
        }

        // Erst nach Yield() werden IsRunning / CanBeCanceled geändert...
        await Task.Yield();
        OnPropertyChanged(nameof(IsPlaying));
        SkipMinutesCommand.NotifyCanExecuteChanged();

        StrongReferenceMessenger.Default.Send(new PlayerStatusChanged(true));

        TaskCompletionSource<object> tcs = new();
        using (cancellationToken.Register(() => tcs.TrySetResult(null)))
        {
            _positionPin = new(Track, CurrentPoint);
            Track.Pin cameraPin = _positionPin.CreateChild(TimeSpan.FromMinutes(-1.5));

            Stopwatch watch = new();
            DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(FrameInterval) };
            timer.Tick += (_, _) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                TimeSpan elapsed = watch.Elapsed;
                watch.Restart();
                _positionPin.AdvanceBy(elapsed * _playerSpeed);
                CurrentPoint = _positionPin.CurrentPoint;
                CameraPoint = cameraPin.CurrentPoint;

                if (_positionPin.IsAtEndOfTrack)
                {
                    PlayCancelCommand.Execute(null);
                }
            };
            timer.Start();

            await tcs.Task;
        }

        await Task.Yield();
        OnPropertyChanged(nameof(IsPlaying));
        SkipMinutesCommand.NotifyCanExecuteChanged();

        StrongReferenceMessenger.Default.Send(new PlayerStatusChanged(false));
    }

    private bool CanPlay() => Mode == Modes.Train && Track is not null;

    [RelayCommand(CanExecute = nameof(CanSkipMinutes))]
    public void SkipMinutes(int amount)
    {
        _positionPin?.AdvanceBy(TimeSpan.FromMinutes(amount));
    }

    private bool CanSkipMinutes() => IsPlaying;
}