﻿using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using cycloid.Routing;
using FluentIcons.Uwp;
using Windows.ApplicationModel.Activation;
using Windows.Services.Maps;
using Windows.Storage;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.StartScreen;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace cycloid;

sealed partial class App : Application,
    IRecipient<PlayerStatusChanged>
{
    public const string NewTrackSentinel = "{NewTrack}";
    public Secrets Secrets { get; }

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

        static async Task InitJumpListAsync()
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

        if (rootFrame.Content == null && args.Files is [StorageFile file, ..])
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

    private readonly DisplayRequest _displayRequest = new();
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
