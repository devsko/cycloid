using System;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;
using Windows.UI.Input;
using Windows.UI.Xaml.Controls.Maps;
using CommunityToolkit.Mvvm.Messaging;
using Windows.UI.Xaml.Input;

namespace cycloid.Controls;

partial class Map
{
    private readonly AsyncThrottle<object, Map> _cameraThrottle = new(
        static (value, @this, cancellationToken) => @this.SetCameraAsync());

    private bool _pointerPanelPointerMoved;

    private async Task SetCameraAsync(bool noAnimation = false)
    {
        BasicGeoposition position, actualPosition = MapControl.ActualCamera.Location.Position;
        if (noAnimation)
        {
            position = actualPosition;
            position.Altitude = ViewModel.CameraAltitude;
        }
        else
        {
            position = ViewModel.CameraPosition.ToBasicGeoposition(ViewModel.CameraAltitude);
        }

        bool animate = !noAnimation && MathF.Abs(GeoCalculation.Distance(ViewModel.CameraPosition, actualPosition.ToMapPoint())) < 5_000;

        await MapControl.TrySetSceneAsync(
            MapScene.CreateFromCamera(new MapCamera(
                location: new Geopoint(position, AltitudeReferenceSystem.Geoid),
                headingInDegrees: ViewModel.CameraHeading,
                pitchInDegrees: ViewModel.CameraPitch)), 
            animate ? MapAnimationKind.Linear : MapAnimationKind.None);
    }

    private void HandleTrainingPointerMoved(PointerPoint point)
    {
        if (ViewModel.IsPlaying)
        {
            DragBearingMode? mode = point.Properties switch
            {
                { IsLeftButtonPressed: true } => DragBearingMode.Height,
                { IsRightButtonPressed: true } => DragBearingMode.HeadingAndPitch,
                _ => null
            };
            if (mode is not null)
            {
                ViewModel.ContinueDragBearing(point.Position, mode.Value);
                SetCameraAsync(noAnimation: true).FireAndForget();
            }
        }
    }

    private void PointerPanel_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (ViewModel.Mode == Modes.Train && ViewModel.IsPlaying)
        {
            PointerPoint point = e.GetCurrentPoint(PointerPanel);
            PointerPanel.CapturePointer(e.Pointer);
            _pointerPanelPointerMoved = false;
            e.Handled = true;
            
            ViewModel.StartDragBearing(point.Position);
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
            if (ViewModel.DeltaBearingDistance(e.GetCurrentPoint(PointerPanel).Properties.MouseWheelDelta))
            {
                SetCameraAsync(noAnimation: false).FireAndForget();
            }
            e.Handled = true;
        }
    }

    void IRecipient<CurrentPointChanged>.Receive(CurrentPointChanged message)
    {
        if (ViewModel.CurrentPoint.IsValid && ViewModel.IsPlaying)
        {
            _cameraThrottle.Next(null, this);
        }
        Nudge();
    }
}