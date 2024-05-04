using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.VisualStudio.Threading;
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

        await TaskScheduler.Default;

        if (exportDetails.Sections)
        {
            await ExportSectionsAsync(exportDetails.SectionsFile);
        }
        if (exportDetails.Tracks)
        {
            await ExportTracksAsync(exportDetails.TracksFile);
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

    private async Task ExportSectionsAsync(IStorageFile sectionsFile)
    {
        using IRandomAccessStream winRtStream = await sectionsFile.OpenAsync(FileAccessMode.ReadWrite);
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

    private async Task ExportTracksAsync(IStorageFile tracksFile)
    {
        using IRandomAccessStream winRtStream = await tracksFile.OpenAsync(FileAccessMode.ReadWrite);
        winRtStream.Size = 0;
        using Stream zipStream = winRtStream.AsStreamForWrite();
        using ZipArchive archive = new(zipStream, ZipArchiveMode.Create);

        int i = 0;
        foreach (IEnumerable<TrackPoint> filePoints in Track.Points.EnumerateFiles())
        {
            DateTime start = DateTime.Now;
            string name = $"{Track.Name} {++i}";

            using Stream fileStream = archive.CreateEntry($"{name}.gpx").Open();
            using var writer = XmlWriter.Create(fileStream, new XmlWriterSettings { Async = true, Indent = true });

            const string ns = "http://www.topografix.com/GPX/1/1";
            await writer.WriteStartDocumentAsync().ConfigureAwait(false);
            await writer.WriteStartElementAsync(null, "gpx", ns).ConfigureAwait(false);
            await writer.WriteStartElementAsync(null, "trk", ns).ConfigureAwait(false);
            await writer.WriteElementStringAsync(null, "name", ns, name).ConfigureAwait(false);
            await writer.WriteStartElementAsync(null, "trkseg", ns).ConfigureAwait(false);

            foreach (TrackPoint point in filePoints)
            {
                await writer.WriteStartElementAsync(null, "trkpt", ns).ConfigureAwait(false);
                await writer.WriteAttributeStringAsync(null, "lat", null, point.Latitude.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                await writer.WriteAttributeStringAsync(null, "lon", null, point.Longitude.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                await writer.WriteElementStringAsync(null, "ele", ns, point.Altitude.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                await writer.WriteElementStringAsync(null, "time", ns, (start + point.Time).ToString("O")).ConfigureAwait(false);
                await writer.WriteEndElementAsync().ConfigureAwait(false);
            }

            await writer.WriteEndDocumentAsync().ConfigureAwait(false);
        }
    }

    private async Task ExportWahooAsync()
    {
        await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppWithArgumentsAsync(Track.File.Path);
    }
}