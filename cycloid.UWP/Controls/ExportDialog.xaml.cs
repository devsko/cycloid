using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace cycloid.Controls;

public sealed partial class ExportDialog : ContentDialog
{
    private RequestExportDetails Request { get; }
    private ExportDetails Response { get; set; }
    
    public ExportDialog()
    {
        InitializeComponent();
    }

    public ExportDialog(RequestExportDetails request) : this()
    {
        Request = request;
    }

    public async Task<ExportDetails> GetResultAsync()
    {
        Response = new();
        Response.Sections = (Response.SectionsFile = await GetFileAsync("Sections.csv")) is not null;
        Response.Water = (Response.WaterFile = await GetFileAsync("Water.csv")) is not null;
        Response.Pois = (Response.PoisFile = await GetFileAsync("POIs.csv")) is not null;
        Response.Tracks = (Response.TracksFile = await GetFileAsync("Tracks.zip")) is not null;
         
        if (await ShowAsync() != ContentDialogResult.Primary)
        {
            return null;
        }

        return Response;

        async Task<IStorageFile> GetFileAsync(string name)
        {
            string key = GetKey(name);
            if (StorageApplicationPermissions.FutureAccessList.ContainsItem(key))
            {
                try
                {
                    return await StorageApplicationPermissions.FutureAccessList.GetFileAsync(key);
                }
                catch (FileNotFoundException)
                { }
            }

            return null;
        }
    }

    private async void CheckBox_Checked(object sender, RoutedEventArgs e)
    {
        string name = (string)((CheckBox)sender).Content;

        if (Response[name] is null)
        {
            FileSavePicker picker = new() { SuggestedFileName = name };
            picker.FileTypeChoices.Add(Path.GetExtension(name)[1..].ToUpperInvariant(), [ Path.GetExtension(name) ]);
            if ((Response[name] = await picker.PickSaveFileAsync()) is null)
            {
                ((CheckBox)sender).IsChecked = false;
            }
            else
            {
                StorageApplicationPermissions.FutureAccessList.AddOrReplace(GetKey(name), Response[name]);
            }
        }
    }

    private string GetKey(string name) => $"{Request.TrackFile.Name}-{name}";
}
