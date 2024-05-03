using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Streams;

namespace cycloid;

public class RequestExportDetails : AsyncRequestMessage<ExportDetails>
{ }

public class ExportDetails : INotifyPropertyChanged
{
    private static readonly PropertyChangedEventArgs _sectionsFileChangedEventArgs = new(nameof(SectionsFile));
    private static readonly PropertyChangedEventArgs _waterFileChangedEventArgs = new(nameof(WaterFile));
    private static readonly PropertyChangedEventArgs _poisFileChangedEventArgs = new(nameof(PoisFile));
    private static readonly PropertyChangedEventArgs _tracksFileChangedEventArgs = new(nameof(TracksFile));

    private PropertyChangedEventHandler _propertyChanged;

    public IStorageFile this[string name]
    {
        get => name switch
        {
            "Sections.csv" => SectionsFile,
            "Water.csv" => WaterFile,
            "POIs.csv" => PoisFile,
            "Tracks.zip" => TracksFile,
            _ => throw new ArgumentException(),
        };
        set
        {
            switch (name)
            {
                case "Sections.csv": 
                    SectionsFile = value;
                    _propertyChanged?.Invoke(this, _sectionsFileChangedEventArgs);
                    break;
                case "Water.csv": 
                    WaterFile = value;
                    _propertyChanged?.Invoke(this, _waterFileChangedEventArgs);
                    break;
                case "POIs.csv": 
                    PoisFile = value;
                    _propertyChanged?.Invoke(this, _poisFileChangedEventArgs);
                    break;
                case "Tracks.zip": 
                    TracksFile = value;
                    _propertyChanged?.Invoke(this, _tracksFileChangedEventArgs);
                    break;
            }
        }
    }
    public IStorageFile SectionsFile { get; set; }
    public IStorageFile WaterFile { get; set; }
    public IStorageFile PoisFile { get; set; }
    public IStorageFile TracksFile { get; set; }
    public bool Sections { get; set; }
    public bool Water { get; set; }
    public bool Pois { get; set; }
    public bool Tracks { get; set; }
    public bool Wahoo { get; set; }

    event PropertyChangedEventHandler INotifyPropertyChanged.PropertyChanged
    {
        add => _propertyChanged += value;
        remove => _propertyChanged -= value;
    }
}

partial class ViewModel
{
    [RelayCommand(CanExecute = nameof(CanExport))]
    public async Task ExportAsync()
    {
        ExportDetails exportDetails = await StrongReferenceMessenger.Default.Send(new RequestExportDetails());

        if (exportDetails is null)
        {
            return;
        }

        if (exportDetails.Sections)
        {
            using IRandomAccessStream winRtStream = await exportDetails.SectionsFile.OpenAsync(FileAccessMode.ReadWrite);
            winRtStream.Size = 0;
            using Stream stream = winRtStream.AsStreamForWrite();
            using StreamWriter writer = new(stream, Encoding.GetEncoding(1252));

            CultureInfo de = CultureInfo.GetCultureInfo("de-DE");
            foreach (var section in Sections)
            {
                if (!section.IsOffTrack)
                {
                    FormattableString str = $"\"{section.TrackFilePosition}\";\"{section.Name}\";\"{section.Distance / 1000:F1}\";\"{section.Ascent:F0}\";\"{section.Descent:F0}\";\"{section.Time:hh\\:mm}\"";
                    await writer.WriteLineAsync(str.ToString(de));
                }
            }
        }
        if (exportDetails.Wahoo)
        {
            await ExportWahooAsync();
        }
    }

    private bool CanExport()
    {
        return HasTrack && !IsEditMode;
    }

    private async Task ExportWahooAsync()
    {
        await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppWithArgumentsAsync(Track.File.Path);
    }
}