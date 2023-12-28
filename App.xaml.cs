using cycloid.External;
using Microsoft.Extensions.Configuration;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.Services.Maps;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace cycloid;

sealed partial class App : Application
{
    public IConfigurationRoot Configuration { get; }

    private CoreDispatcher _dispatcher;

    public App()
    {
        UnhandledException += (_, e) => ShowExceptionAsync(e.Exception?.ToString() ?? e.Message).FireAndForget();
        
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var builder = new ConfigurationBuilder();
        builder.AddUserSecrets("not used");
        Configuration = builder.Build();

        MapService.ServiceToken = Configuration["Bing:ServiceApiKey"];

        InitializeComponent();
    }

    public static new App Current => (App)Application.Current;

    public ViewModel ViewModel => (ViewModel)Resources[nameof(ViewModel)];

    public Strava Strava { get; } = new();

    protected override void OnLaunched(LaunchActivatedEventArgs e)
    {
        _dispatcher = Window.Current.Dispatcher;

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
    }

    public Task ShowExceptionAsync(Exception ex) => ShowExceptionAsync(ex.ToString());

    public async Task ShowExceptionAsync(string message)
    {
        try
        {
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
                catch
                {
                    Debugger.Break();
                }
            });
        }
        catch
        {
            Debugger.Break();
        }
    }
}
