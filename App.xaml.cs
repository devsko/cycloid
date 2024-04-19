using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Services.Maps;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.StartScreen;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace cycloid;

sealed partial class App : Application
{
    public const string NewTrackSentinel = "{NewTrack}";
    public IConfigurationRoot Configuration { get; }

    private CoreDispatcher _dispatcher;

    public App()
    {
        UnhandledException += (_, e) =>
        {
            e.Handled = true;
            ShowExceptionAsync(e.Exception?.ToString() ?? e.Message).FireAndForget();
        };
        
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var builder = new ConfigurationBuilder();
        builder.AddUserSecrets("not used");
        Configuration = builder.Build();

        MapService.ServiceToken = Configuration["Bing:ServiceApiKey"];

        InitializeComponent();
    }

    public static new App Current => (App)Application.Current;

    protected override void OnLaunched(LaunchActivatedEventArgs e)
    {
        _dispatcher = Window.Current.Dispatcher;

        InitJumpListAsync().FireAndForget();

        if (Window.Current.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            rootFrame.NavigationFailed += (_, e) => throw new Exception("Failed to load Page " + e.SourcePageType.FullName);

            Window.Current.Content = rootFrame;
        }

        if (!e.PrelaunchActivated)
        {
            if (rootFrame.Content == null)
            {
                rootFrame.Navigate(typeof(MainPage), e.Arguments);
            }
            Window.Current.Activate();
        }

        async Task InitJumpListAsync()
        {
            JumpList list = await JumpList.LoadCurrentAsync();
            list.SystemGroupKind = JumpListSystemGroupKind.Recent;
            list.Items.Clear();
            list.Items.Add(JumpListItem.CreateWithArguments(NewTrackSentinel, "New track"));
            await list.SaveAsync();
        }
    }

    protected override void OnFileActivated(FileActivatedEventArgs args)
    {
        _dispatcher = Window.Current.Dispatcher;

        if (Window.Current.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            rootFrame.NavigationFailed += (_, e) => throw new Exception("Failed to load Page " + e.SourcePageType.FullName);

            Window.Current.Content = rootFrame;
        }

        StorageFile file = args.Files.FirstOrDefault() as StorageFile;
        if (rootFrame.Content == null)
        {
            rootFrame.Navigate(typeof(MainPage), file);
        }
        Window.Current.Activate();
    }

    public Task ShowExceptionAsync(Exception ex) => ShowExceptionAsync(ex.ToString());

    public async Task ShowExceptionAsync(string message)
    {
        try
        {
            Debug.WriteLine(message);
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
            await _dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates
            {
                try
                {
                    ContentDialog dialog = new()
                    {
                        Title = "Error",
                        Content = message,
                        CloseButtonText = "Close",
                    };
                    await dialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Cannot show error dialog. {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Cannot show error dialog. {ex.Message}");
        }
    }
}
