using System.Globalization;
using cycloid;
using Serializer = cycloid.Serialization.Serializer;

using FileStream json = File.OpenRead(@"C:\Users\stefa\OneDrive\Dokumente\TPBR24\Route\TPBR24.track");

Track track = new(false);
await Serializer.LoadAsync(json, track, null);


using StreamWriter output = File.CreateText(@"C:\Users\stefa\OneDrive\Dokumente\TPBR24\Route\TPPBR24.txt");

foreach (var point in track.Points)
{
    await output.WriteLineAsync(string.Create(CultureInfo.InvariantCulture, $"[{point.Longitude},{point.Latitude}],"));
}