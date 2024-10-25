using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Windows.Foundation;
using Windows.UI.Xaml;

namespace cycloid;

public class PlayerStatusChanged(bool playing) : ValueChangedMessage<bool>(playing);

public enum DragBearingMode
{
    Height,
    HeadingAndPitch,
}

partial class ViewModel
{
    private const int FrameInterval = 20;

    private float _playerSpeed = 10f;
    private float _distanceFactor = .6f;
    private float _addHeight = 60;
    private float _addHeading = 0;
    private float _addPitch = 10;

    private MapPoint _cameraPosition;
    private float _cameraAltitude;
    private float _cameraHeading;
    private float _cameraPitch;

    private Point _startDragBearing;
    private float _startDragHeight;
    private float _startDragHeading;
    private float _startDragPitch;

    private Track.RootPin _positionPin;
    private Track.Pin _bearingPin;

    public MapPoint CameraPosition => _cameraPosition;

    public float CameraAltitude => _cameraAltitude;

    public float CameraHeading => _cameraHeading;

    public float CameraPitch => _cameraPitch;

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
            _bearingPin = _positionPin.CreateChild(TimeSpan.FromMinutes(-1.5));

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

                CalculateCamera();

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
   
    public void StartDragBearing(Point point)
    {
        _startDragBearing = point;
        _startDragHeight = _addHeight;
        _startDragHeading = _addHeading;
        _startDragPitch = _addPitch;
    }

    public void ContinueDragBearing(Point point, DragBearingMode mode)
    {
        if (mode == DragBearingMode.Height)
        {
            float cameraPitch = _cameraPitch;
            float addPitch = _addPitch;

            _addHeight = _startDragHeight + (float)(point.Y - _startDragBearing.Y) / 5;

            CalculateCamera();

            _addPitch = Math.Clamp(_addPitch + cameraPitch - _cameraPitch, -70, 40);
            _cameraPitch += _addPitch - addPitch;
        }
        else
        {
            _addHeading = _startDragHeading - (float)(point.X - _startDragBearing.X) / 20;
            _addPitch = Math.Clamp(_startDragPitch + (float)(point.Y - _startDragBearing.Y) / 20, -70, 20);
        }
    }

    public bool DeltaBearingDistance(int delta)
    {
        float factor = _distanceFactor;
        _distanceFactor = Math.Clamp(_distanceFactor * MathF.Pow(2, -(float)delta / 5000), .25f, 2f);
        if (factor != _distanceFactor)
        {
            CalculateCamera();
            return true;
        }

        return false;
    }

    private void CalculateCamera()
    {
        (float bearingDistance, float heading) = GeoCalculation.DistanceAndHeading(_bearingPin.CurrentPoint, CurrentPoint);
        float distance = MathF.Sqrt(bearingDistance * _distanceFactor) * 10;
        float altitude = _addHeight + (_bearingPin.CurrentPoint.Altitude - CurrentPoint.Altitude) * distance / bearingDistance + CurrentPoint.Altitude;
        float pitch = 90 - MathF.Atan2(altitude - CurrentPoint.Altitude, distance) * 180 / MathF.PI;
        (float latitude, float longitude) = GeoCalculation.Add(CurrentPoint, heading + 180, distance);

        (_cameraPosition, _cameraAltitude, _cameraHeading, _cameraPitch) = (new MapPoint(latitude, longitude), altitude, heading + _addHeading, pitch + _addPitch);
    }
}