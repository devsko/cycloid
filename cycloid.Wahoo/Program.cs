// https://www.youtube.com/watch?v=Sl--gcJ95XM

using System.Globalization;
using System.Text.Json;
using cycloid.Info;
using cycloid.Serialization;
using cycloid.Wahoo;
using Microsoft.Data.Sqlite;

try
{
    if (args is not [.., string trackFilePath] || !File.Exists(trackFilePath) || Path.GetExtension(trackFilePath) != ".track")
    {
        throw new ArgumentException("Usage: cycloid.Wahoo <track file>");
    }

    await using Connector connector = new();

    while (true)
    {
        ConnectionState state = await connector.GetStateAsync();
        if (state == ConnectionState.Connected)
        {
            break;
        }

        Console.WriteLine($"Device not {(state == ConnectionState.NotAuthorized ? "authorized" : "connected")}...");
        await Task.Delay(TimeSpan.FromSeconds(1));
    }

    await connector.DevicesAsync();

    PointOfInterest[] pointsOfInterest;
    using (Stream json = File.OpenRead(trackFilePath))
    {
        Track track = await JsonSerializer.DeserializeAsync(json, TrackContext.Default.Track) ?? throw new InvalidOperationException($"Error deserializing {trackFilePath}");
        pointsOfInterest = track.PointsOfInterest;
    }

    string trackName = Path.GetFileName(trackFilePath);

    string filePath = await connector.DownloadDatabaseAsync();

    using (SqliteConnection connection = new($"data source={filePath}"))
    {
        await connection.OpenAsync();

        string? customBinary = await ExecuteScalarAsync<string>("SELECT quote(custom) FROM CloudPoiDao WHERE custom IS NOT NULL LIMIT 1");

        int deleted = await ExecuteAsync($"DELETE FROM CloudPoiDao WHERE address='[cycloid] {trackName}'");

        Console.WriteLine($"{deleted} POI deleted");

        await ExecuteAsync("UPDATE sqlite_sequence SET seq = (SELECT MAX(id) FROM CloudPoiDao) WHERE name='CloudPoiDao'");

        int added = 0;
        char[] hash = new char[12];
        foreach (PointOfInterest pointOfInterest in pointsOfInterest.Where(poi => poi.Type != InfoType.Split))
        {
            GeoHasher.Encode(pointOfInterest.Location.Lat, pointOfInterest.Location.Lon, hash);
            await ExecuteAsync(string.Create(CultureInfo.InvariantCulture, $"""
                INSERT INTO CloudPoiDao (address, custom, geoHash, isDeleted, latDeg, lonDeg, name, poiToken, poiType, objectCloudId, updateTimeMs, userCloudId) 
                VALUES ('[cycloid] {trackName}', {customBinary}, '{hash}', 0, {pointOfInterest.Location.Lat:f13}, {pointOfInterest.Location.Lon:f13}, '{Escape(pointOfInterest.Name)} ({pointOfInterest.Type})', '{Guid.NewGuid()}', 0, 0, 1677695093935, 0)
                """));
            added++;
        }

        Console.WriteLine($"{added} POI added");

        string Escape(string value) => value.Replace("'", "''");

        async Task<int> ExecuteAsync(string sql)
        {
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.CommandText = sql;

            return await cmd.ExecuteNonQueryAsync();
        }

        async Task<T?> ExecuteScalarAsync<T>(string sql)
        {
            using SqliteCommand cmd = connection.CreateCommand();
            cmd.CommandText = sql;

            return (T?)await cmd.ExecuteScalarAsync();
        }
    }

    SqliteConnection.ClearAllPools();

    await connector.UploadDatabaseAsync();
    await connector.RebootAsync();
}
catch (Exception ex)
{
    Console.WriteLine(ex);
    Console.WriteLine();
    Console.Write("Press any key...");
    Console.ReadKey(true);

    return;
}

Console.WriteLine();
Console.WriteLine("Successfully updated...");
