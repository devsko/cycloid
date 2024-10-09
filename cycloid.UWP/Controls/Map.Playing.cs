using System;
using System.Threading.Tasks;
using System.Threading;
using Windows.Devices.Geolocation;
using Windows.UI.Input;
using Windows.UI.Xaml.Controls.Maps;
using CommunityToolkit.Mvvm.Messaging;
using Windows.UI.Xaml.Input;

namespace cycloid.Controls;

partial class Map
{
    private readonly AsyncThrottle<(TrackPoint Current, TrackPoint Camera), Map> _cameraThrottle = new(
        static (value, @this, cancellationToken) => @this.SetCameraAsync(value, cancellationToken));

    private float _distanceFactor = .6f;
    private float _height = 60;
    private float _angle = 0;
    private float _pitch = 10;

    private PointerPoint _startPoint;
    private float _startHeight;
    private float _startAngle;
    private float _startPitch;

    private bool _pointerPanelPointerMoved;

    private async Task SetCameraAsync((TrackPoint Current, TrackPoint Camera) value, CancellationToken _, bool noAnimation = false)
    {
        (double distance, double heading) = GeoCalculation.DistanceAndHeading(value.Camera, value.Current);
        double cameraDistance = Math.Sqrt(distance * _distanceFactor) * 10;
        double altitude = _height + (value.Camera.Altitude - value.Current.Altitude) * cameraDistance / distance + value.Current.Altitude;
        double pitch = _pitch + 90 - Math.Atan2(altitude - value.Current.Altitude, cameraDistance) * 180 / Math.PI;
        (float latitude, float longitude) = GeoCalculation.Add(value.Current, heading + 180, cameraDistance);
        heading += _angle;

        BasicGeoposition cameraPosition = new()
        {
            Latitude = latitude,
            Longitude = longitude,
            Altitude = altitude
        };
        MapCamera camera = new(
            location: new Geopoint(cameraPosition, AltitudeReferenceSystem.Geoid),
            headingInDegrees: heading,
            pitchInDegrees: pitch);

        bool animate = !noAnimation && MathF.Abs(GeoCalculation.Distance(
            cameraPosition.ToMapPoint(),
            MapControl.ActualCamera.Location.Position.ToMapPoint())) < 5_000;

        await MapControl.TrySetSceneAsync(MapScene.CreateFromCamera(camera), animate ? MapAnimationKind.Linear : MapAnimationKind.None);
        await Task.Delay(animate ? TimeSpan.FromMilliseconds(50) : TimeSpan.FromSeconds(2));
    }

    private void HandleTrainingPointerMoved(PointerPoint pointer)
    {
        if (ViewModel.IsPlaying)
        {
            bool setCamera = false;
            if (pointer.Properties.IsLeftButtonPressed)
            {
                _height = _startHeight + (float)(pointer.Position.Y - _startPoint.Position.Y) / 10;
                setCamera = true;
            }
            else if (pointer.Properties.IsRightButtonPressed)
            {
                _angle = _startAngle - (float)(pointer.Position.X - _startPoint.Position.X) / 20;
                _pitch = Math.Clamp(_startPitch + (float)(pointer.Position.Y - _startPoint.Position.Y) / 20, -70, 20);
                setCamera = true;
            }
            if (setCamera)
            {
                SetCameraAsync((ViewModel.CurrentPoint, ViewModel.CameraPoint), default, noAnimation: true).FireAndForget();
            }
        }
    }

    private void PointerPanel_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.Mode == Modes.Train && ViewModel.IsPlaying)
        {
            _startPoint = e.GetCurrentPoint(PointerPanel);
            _startHeight = _height;
            _startAngle = _angle;
            _startPitch = _pitch;
            PointerPanel.CapturePointer(e.Pointer);
            _pointerPanelPointerMoved = false;
            e.Handled = true;
        }
    }

    private void PointerPanel_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.Mode == Modes.Train && ViewModel.IsPlaying)
        {
            PointerPanel.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        }
    }

    private void PointerPanel_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.Mode == Modes.Train && ViewModel.IsPlaying)
        {
            var newFactor = Math.Clamp(_distanceFactor * MathF.Pow(2, -(float)e.GetCurrentPoint(PointerPanel).Properties.MouseWheelDelta / 10000), .25f, 2f);
            if (newFactor != _distanceFactor)
            {
                _distanceFactor = newFactor;
                SetCameraAsync((ViewModel.CurrentPoint, ViewModel.CameraPoint), default, noAnimation: true).FireAndForget();
            }
            _pointerPanelPointerMoved = true;
            e.Handled = true;
        }
    }

    void IRecipient<CurrentPointChanged>.Receive(CurrentPointChanged message)
    {
        if (ViewModel.CurrentPoint.IsValid && ViewModel.IsPlaying)
        {
            _cameraThrottle.Next((ViewModel.CurrentPoint, ViewModel.CameraPoint), this);
        }
        Nudge();
    }
}