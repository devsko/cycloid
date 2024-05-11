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
{
    public IStorageFile TrackFile { get; init; }
}

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
        ExportDetails exportDetails = await StrongReferenceMessenger.Default.Send(new RequestExportDetails { TrackFile = Track.File });

        if (exportDetails is null)
        {
            return;
        }

        await TaskScheduler.Default;

        if (exportDetails.Sections)
        {
            await ExportSectionsAsync(exportDetails.SectionsFile);
        }
        if (exportDetails.Water)
        {
            await ExportWaterAsync(exportDetails.WaterFile);
        }
        if (exportDetails.Pois)
        {
            await ExportPoisAsync(exportDetails.PoisFile);
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

    private readonly CultureInfo _deCulture = CultureInfo.GetCultureInfo("de-DE");

    private async Task ExportSectionsAsync(IStorageFile sectionsFile)
    {
        using StreamWriter writer = await GetCsvWriterAsync(sectionsFile);

        foreach (OnTrack section in Sections)
        {
            if (!section.IsOffTrack)
            {
                FormattableString str = $"\"{section.TrackFilePosition}\";\"{section.Name}\";\"{section.Distance / 1000:F1}\";\"{section.Ascent:F0}\";\"{section.Descent:F0}\";\"{section.Time:hh\\:mm}\"";
                await writer.WriteLineAsync(str.ToString(_deCulture));
            }
        }
    }

    private async Task ExportWaterAsync(IStorageFile waterFile)
    {
        using StreamWriter writer = await GetCsvWriterAsync(waterFile);

        foreach (OnTrack onTrack in Points)
        {
            if (!onTrack.IsOffTrack && onTrack.PointOfInterest.Category == Info.InfoCategory.Water)
            {
                string name = onTrack.Name;
                if (onTrack.PointOfInterest.Type == Info.InfoType.Toilet)
                {
                    name = "WC " + name;
                }
                FormattableString str = $"\"{onTrack.TrackFilePosition}\";\"{name}\"";
                await writer.WriteLineAsync(str.ToString(_deCulture));
            }
        }
    }

    private async Task ExportPoisAsync(IStorageFile poisFile)
    {
        using StreamWriter writer = await GetCsvWriterAsync(poisFile);

        foreach (OnTrack onTrack in Points)
        {
            if (!onTrack.IsOffTrack && onTrack.PointOfInterest.Category != Info.InfoCategory.Water)
            {
                string type = onTrack.PointOfInterest.Type switch 
                {
                    Info.InfoType.Restaurant => "R",
                    Info.InfoType.FastFood => "F",
                    Info.InfoType.Bar => "C",
                    Info.InfoType.Supermarket => "S",
                    Info.InfoType.Bakery => "B",
                    Info.InfoType.FuelStation => "T",
                    Info.InfoType.Bed => "H",
                    Info.InfoType.Outdoor => "W",
                    Info.InfoType.Roof => "D",
                    _ => throw new InvalidOperationException() 
                };
                FormattableString str = $"\"{onTrack.TrackFilePosition}\";\"{type}\";\"{onTrack.Name}\"";
                await writer.WriteLineAsync(str.ToString(_deCulture));
            }
        }
    }

    private async Task<StreamWriter> GetCsvWriterAsync(IStorageFile file)
    {
        IRandomAccessStream winRtStream = await file.OpenAsync(FileAccessMode.ReadWrite);
        winRtStream.Size = 0;
        
        return new StreamWriter(winRtStream.AsStreamForWrite(), Encoding.GetEncoding(1252));
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