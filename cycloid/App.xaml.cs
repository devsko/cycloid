using System.Diagnostics;
using System.Numerics;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using cycloid.Routing;
using Microsoft.VisualStudio.Threading;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Services.Maps;
using Windows.Storage;
using Windows.System.Display;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.StartScreen;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace cycloid;

public class TitleBarLayoutChanged();

sealed partial class App : Application,
    IRecipient<PlayerStatusChanged>
{
    public const string NewTrackArgument = "-new";

    public Secrets Secrets { get; }

    public ViewModel ViewModel { get; private set; }

    public Vector2 TitleBarInset { get; private set; }

    private readonly DisplayRequest _displayRequest = new();
    private CoreDispatcher _dispatcher;

    public App()
    {
        UnhandledException += (_, e) =>
        {
            e.Handled = true;
            ShowExceptionAsync(e.Exception?.ToString() ?? e.Message).FireAndForget();
        };

        RouteBuilder.ExceptionHandler = Current.ShowExceptionAsync;

        InitJumpListAsync().FireAndForget();

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using Stream secretsJson = typeof(App).Assembly.GetManifestResourceStream("cycloid.secrets.json");
        Secrets = JsonSerializer.Deserialize(secretsJson, SecretsContext.Default.Secrets);

        MapService.ServiceToken = Secrets.BingServiceApiKey;

        InitializeComponent();

        StrongReferenceMessenger.Default.Register<PlayerStatusChanged>(this);

        static async Task InitJumpListAsync()
        {
            JumpList list = await JumpList.LoadCurrentAsync();
            list.SystemGroupKind = JumpListSystemGroupKind.Recent;
            list.Items.Clear();
            list.Items.Add(JumpListItem.CreateWithArguments(NewTrackArgument, "New track"));
            await list.SaveAsync();
        }
    }

    public static new App Current => (App)Application.Current;

    protected override void OnLaunched(LaunchActivatedEventArgs e)
    {
        if (Window.Current.Content is null)
        {
            Initialize();

            Window.Current.Content = new StartPage(e.Arguments == NewTrackArgument);
            Window.Current.Activate();
        }
    }

    protected override void OnFileActivated(FileActivatedEventArgs args)
    {
        if (Window.Current.Content is null)
        {
            Initialize();

            if (args.Files is [StorageFile file, ..])
            {
                Window.Current.Content = new StartPage(file);
                Window.Current.Activate();
            }
        }
    }

    private void Initialize()
    {
        ViewModel = new ViewModel();

        _dispatcher = Window.Current.Dispatcher;

        ApplicationView.GetForCurrentView().TitleBar.ButtonBackgroundColor = Colors.Transparent;
        ApplicationView.GetForCurrentView().TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

        CoreApplicationViewTitleBar titleBar = CoreApplication.GetCurrentView().TitleBar;
        titleBar.ExtendViewIntoTitleBar = true;
        titleBar.LayoutMetricsChanged += TitleBar_LayoutMetricsChanged;
    }

    private void TitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
    {
        TitleBarInset = new Vector2((float)sender.SystemOverlayLeftInset, (float)sender.SystemOverlayRightInset);
        
        StrongReferenceMessenger.Default.Send(new TitleBarLayoutChanged());
    }

    public Task ShowExceptionAsync(Exception ex) => ShowExceptionAsync(ex.ToString());

    public async Task ShowExceptionAsync(string message)
    {
        try
        {
            Debug.WriteLine(message);
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
            await _dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
            {
                try
                {
                    ErrorDialog dialog = new(message);
                    await dialog.ShowAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Cannot show error dialog. {ex.Message}");
                }
            });
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Cannot show error dialog. {ex.Message}");
        }
    }

    void IRecipient<PlayerStatusChanged>.Receive(PlayerStatusChanged message)
    {
        if (message.Value)
        {
            _displayRequest.RequestActive();
        }
        else
        {
            _displayRequest.RequestRelease();
        }
    }
}
