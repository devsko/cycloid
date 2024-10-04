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

    private float _playerSpeed = 7.5f;

    public TrackPoint CameraPoint { get; set; }

    public bool IsPlaying => PlayCommand.CanBeCanceled;

    [RelayCommand(CanExecute = nameof(CanPlay), IncludeCancelCommand = true)]
    public async Task PlayAsync(CancellationToken cancellationToken)
    {
        if (!CanPlay())
        {
            return;
        }

        await Task.Yield();
        OnPropertyChanged(nameof(IsPlaying));

        StrongReferenceMessenger.Default.Send(new PlayerStatusChanged(true));

        if (!CurrentPoint.IsValid || CurrentPoint == Track.Points.Last())
        {
            CurrentPoint = Track.Points.First();
        }

        TaskCompletionSource<object> tcs = new();
        using (cancellationToken.Register(() => tcs.TrySetResult(null)))
        {
            Track.RootPin positionPin = new(Track, CurrentPoint);
            Track.Pin cameraPin = positionPin.CreateChild(TimeSpan.FromMinutes(-2));

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
                positionPin.AdvanceBy(elapsed * _playerSpeed);
                CurrentPoint = positionPin.CurrentPoint;
                CameraPoint = cameraPin.CurrentPoint;

                if (positionPin.IsAtEndOfTrack)
                {
                    PlayCancelCommand.Execute(null);
                }
            };
            timer.Start();

            await tcs.Task;
        }

        await Task.Yield();
        OnPropertyChanged(nameof(IsPlaying));

        StrongReferenceMessenger.Default.Send(new PlayerStatusChanged(false));
    }

    private bool CanPlay() => 
        Mode == Modes.Train && Track is not null;
}