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
using Windows.UI.Xaml.Controls;

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

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using Stream secretsJson = typeof(App).Assembly.GetManifestResourceStream("cycloid.secrets.json");
        Secrets = JsonSerializer.Deserialize(secretsJson, SecretsContext.Default.Secrets);

        MapService.ServiceToken = Secrets.BingServiceApiKey;

        InitializeComponent();

        StrongReferenceMessenger.Default.Register<PlayerStatusChanged>(this);
    }

    public static new App Current => (App)Application.Current;

    protected override void OnLaunched(LaunchActivatedEventArgs e)
    {
        if (Window.Current.Content is null)
        {
            Initialize();

            UserControl page;
            if (e.Arguments is { Length: > 0 } and not ['-', ..])
            {
                page = new MainPage(new InitializeTrackOptions { FilePath = e.Arguments });
            }
            else
            {
                page = new StartPage(e.Arguments == NewTrackArgument);
            }
            Window.Current.Content = page;
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
                Window.Current.Content = new MainPage(new InitializeTrackOptions { File = file });
                Window.Current.Activate();
            }
        }
    }

    private void Initialize()
    {
        _dispatcher = Window.Current.Dispatcher;

        ViewModel = new ViewModel();

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

    public async Task UpdateJumpListAsync(IList<TrackListItem> items)
    {
        JumpList jumpList = await JumpList.LoadCurrentAsync();
        jumpList.SystemGroupKind = JumpListSystemGroupKind.None;
        jumpList.Items.Clear();
        jumpList.Items.Add(JumpListItem.CreateWithArguments(NewTrackArgument, "New track"));

        bool first = true;
        foreach (TrackListItem item in items.Where(item => item.IsPinned).Take(4))
        {
            if (first)
            {
                jumpList.Items.Add(JumpListItem.CreateSeparator());
                first = false;
            }
            JumpListItem jumpListItem = JumpListItem.CreateWithArguments(item.File.Path, item.Name);
            jumpListItem.Description = $"{Format.Distance(item.TrackDistance)}\r\n{item.DirectoryPath}";
            jumpList.Items.Add(jumpListItem);
        }

        await jumpList.SaveAsync();
    }

    public Task ShowExceptionAsync(Exception ex) => ShowExceptionAsync(ex.ToString());

    public async Task ShowExceptionAsync(string message)
    {
        try
        {
            Debug.WriteLine(message);
#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
            if (_dispatcher is not null)
            {
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
            }
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
