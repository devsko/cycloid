using System;
using Windows.Storage;
using Windows.UI.Xaml;

namespace cycloid.Controls;

public sealed partial class StreetView : TrackPointControl
{
    public bool IsCollapsed
    {
        get => (bool)GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    public static readonly DependencyProperty IsCollapsedProperty =
        DependencyProperty.Register(nameof(IsCollapsed), typeof(bool), typeof(StreetView), new PropertyMetadata(true, (sender, e) => ((StreetView)sender).IsCollapsedChanged(e)));

    public double HeadingOffset
    {
        get => (double)GetValue(HeadingOffsetProperty);
        set => SetValue(HeadingOffsetProperty, value);
    }

    public static readonly DependencyProperty HeadingOffsetProperty =
        DependencyProperty.Register(nameof(HeadingOffset), typeof(double), typeof(StreetView), new PropertyMetadata(0d, (sender, _) => ((StreetView)sender).Update()));

    public StreetView()
    {
        InitializeComponent();

        VisualStateManager.GoToState(this, "CollapsedState", false);
    }

    private async void IsCollapsedChanged(DependencyPropertyChangedEventArgs _)
    {
        DragableBehavior.IsEnabled = !IsCollapsed;

        if (!IsCollapsed)
        {
            StorageFile htmlFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/StreetView.html"));
            Uri fileUri = new Uri(htmlFile.Path);
            WebView.Source = fileUri;
            // Update
        }
    }

    private void Update()
    { }
}
