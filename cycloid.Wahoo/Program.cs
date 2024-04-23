// https://www.youtube.com/watch?v=Sl--gcJ95XM

using System.Text.Json;
using CliWrap;
using CliWrap.Buffered;
using cycloid.Wahoo;
using Microsoft.Data.Sqlite;

try
{
    if (args is not [.., string trackFilePath] || !File.Exists(trackFilePath) || Path.GetExtension(trackFilePath) != ".track")
    {
        throw new ArgumentException("Usage: cycloid.Wahoo <track file>");
    }

    Command adb = Cli.Wrap(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "adb/adb.exe"));

    PointOfInterest[] pointsOfInterest;
    using (Stream json = File.OpenRead(trackFilePath))
    {
        Track track = await JsonSerializer.DeserializeAsync(json, TrackContext.Default.Track) ?? throw new InvalidOperationException($"Error deserializing {trackFilePath}");
        pointsOfInterest = track.PointsOfInterest;
    }

    string trackName = Path.GetFileName(trackFilePath);

    string localDirectory = Path.GetTempPath();
    string localFilePath = Path.Combine(localDirectory, "BoltApp.sqlite");

    string devices = await AdbAsync($"devices");

    if (!devices.Contains("\tdevice"))
    {
        throw new InvalidOperationException($"Device not {(devices.Contains("\tunauthorized") ? "authorized" : "connected")}");
    }

    await DownloadAsync(localFilePath);
    await DownloadAsync(localFilePath + "-shm");
    await DownloadAsync(localFilePath + "-wal");

    try
    {
        using (SqliteConnection connection = new($"data source={localFilePath}"))
        {
            await connection.OpenAsync();

            string? customBinary = await ExecuteScalarAsync<string>("SELECT quote(custom) FROM CloudPoiDao WHERE custom IS NOT NULL LIMIT 1");

            int deleted = await ExecuteAsync($"DELETE FROM CloudPoiDao WHERE address='[cycloid] {trackName}'");

            Console.WriteLine($"{deleted} POI deleted");

            await ExecuteAsync("UPDATE sqlite_sequence SET seq = (SELECT MAX(id) FROM CloudPoiDao) WHERE name='CloudPoiDao'");

            int added = 0;
            foreach (PointOfInterest pointOfInterest in pointsOfInterest)
            {
                if (pointOfInterest.Type is not "Split" and not null)
                {
                    string hash = GeoHasher.Encode(pointOfInterest.Location.Lat, pointOfInterest.Location.Lon);
                    await ExecuteAsync(FormattableString.Invariant($"""
                    INSERT INTO CloudPoiDao (address, custom, geoHash, isDeleted, latDeg, lonDeg, name, poiToken, poiType, objectCloudId, updateTimeMs, userCloudId) 
                    VALUES ('[cycloid] {trackName}', {customBinary}, '{hash}', 0, {pointOfInterest.Location.Lat:f13}, {pointOfInterest.Location.Lon:f13}, '{Escape(pointOfInterest.Name)} ({pointOfInterest.Type})', '{Guid.NewGuid()}', 0, 0, 1677695093935, 0)
                    """));
                    added++;
                }
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

        await UploadAsync(localFilePath);
        await UploadAsync(localFilePath + "-shm");
        await UploadAsync(localFilePath + "-wal");

        await AdbAsync("reboot");
    }
    finally
    {
        SqliteConnection.ClearAllPools();

        foreach (string dbFile in Directory.GetFiles(localDirectory, Path.GetFileName(localFilePath) + '*'))
        {
            try
            {
                File.Delete(dbFile);
            }
            catch { }
        }
    }

    async Task DownloadAsync(string localFilePath)
    {
        File.Delete(localFilePath);
        await AdbAsync($"pull /data/data/com.wahoofitness.bolt/databases/{Path.GetFileName(localFilePath)} {localFilePath}");
    }

    async Task UploadAsync(string localFilePath)
    {
        if (File.Exists(localFilePath))
        {
            await AdbAsync($"push {localFilePath} /data/data/com.wahoofitness.bolt/databases/");
        }
        else
        {
            await AdbAsync($"shell rm data/data/com.wahoofitness.bolt/databases/{Path.GetFileName(localFilePath)}");
        }
    }

    async Task<string> AdbAsync(string parameter)
    {
        Console.WriteLine(parameter);

        BufferedCommandResult result = await adb
            .WithArguments(parameter)
            .ExecuteBufferedAsync();

        string output = result.StandardOutput;
    
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(output + Environment.NewLine + result.StandardError);
        }

        Console.Write(output);

        return output;
    }
}
catch (Exception ex)
{
    Console.WriteLine(ex);
}

Console.Write("Press enter");
Console.ReadLine();
