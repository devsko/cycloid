using CliWrap.Buffered;
using CliWrap;
using Microsoft.Data.Sqlite;

namespace cycloid.Wahoo;

public enum ConnectionState
{
    Connected,
    NotConnected,
    NotAuthorized,
}

public class Connector : IAsyncDisposable
{
    private readonly Command _adb = Cli.Wrap(Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!, "adb", "adb.exe"));
    private DirectoryInfo? _tempDirectory;

    public async Task<ConnectionState> GetStateAsync() => 
        (await AdbAsync($"devices", false, true)).Output switch
        {
            string d when d.Contains("\tdevice") => ConnectionState.Connected,
            string d when d.Contains("\tunauthorized") => ConnectionState.NotAuthorized,
            _ => ConnectionState.NotConnected,
        };

    public async Task<string> DownloadDatabaseAsync()
    {
        if (_tempDirectory is not null)
        {
            throw new InvalidOperationException();
        }

        _tempDirectory = Directory.CreateTempSubdirectory("WahooBoltApp");

        string filePath = Path.Combine(_tempDirectory.FullName, "BoltApp.sqlite");
        await DownloadAsync(filePath, false);
        await DownloadAsync($"{filePath}-shm", true);
        await DownloadAsync($"{filePath}-wal", true);
        await DownloadAsync($"{filePath}-journal", true);

        return filePath;
    }

    public async Task UploadDatabaseAsync()
    {
        if (_tempDirectory is null)
        {
            throw new InvalidOperationException();
        }

        string filePath = Path.Combine(_tempDirectory.FullName, "BoltApp.sqlite");
        await UploadAsync(filePath);
        await UploadAsync($"{filePath}-shm");
        await UploadAsync($"{filePath}-wal");
        await UploadAsync($"{filePath}-journal");

        try
        {
            _tempDirectory.Delete(recursive: true);
        }
        catch { }

        _tempDirectory = null;
    }

    public Task DevicesAsync() => 
        AdbAsync("devices -l", false);

    public Task RebootAsync() =>
        AdbAsync("reboot", false);

    public async ValueTask DisposeAsync()
    {
        await AdbAsync("kill-server", false);

        SqliteConnection.ClearAllPools();

        try
        {
            _tempDirectory?.Delete(recursive: true);
        }
        catch { }

        GC.SuppressFinalize(this);
    }

    private Task<(bool Success, string Output)> DownloadAsync(string filePath, bool dontThrow) => 
        AdbAsync($"pull /data/data/com.wahoofitness.bolt/databases/{Path.GetFileName(filePath)} {filePath}", dontThrow);

    private Task<(bool Success, string Output)> UploadAsync(string filePath) => 
        File.Exists(filePath)
            ? AdbAsync($"push {filePath} /data/data/com.wahoofitness.bolt/databases/", false)
            : AdbAsync($"shell rm data/data/com.wahoofitness.bolt/databases/{Path.GetFileName(filePath)}", true);

    private async Task<(bool Success, string Output)> AdbAsync(string parameter, bool dontThrow, bool supressOutput = false)
    {
        if (!supressOutput)
        {
            Console.WriteLine(parameter);
        }

        BufferedCommandResult result = await _adb
            .WithArguments(parameter)
            .WithValidation(dontThrow ? CommandResultValidation.None : CommandResultValidation.ZeroExitCode)
            .ExecuteBufferedAsync();

        string output = result.StandardOutput;

        if (!result.IsSuccess)
        {
            output += Environment.NewLine + result.StandardError;
            if (dontThrow)
            {
                return (false, output);
            }
            else
            {
                throw new InvalidOperationException(output);
            }
        }

        if (!supressOutput)
        {
            Console.Write(output);
        }

        return (true, output);
    }
}
