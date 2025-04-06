using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.Web.Http;

namespace cycloid.Controls;

public sealed partial class PopupBrowser : Control, IDisposable
{
    private readonly TaskCompletionSource<bool> _constructionTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Popup _popup;
    private Border _border;
    private TextBlock _textBlock;
    private Button _closeButton;
    private WebView2 _webView;

    private (ulong Id, Uri Uri, bool IsRedirect) _lastNavigation;
    private TaskCompletionSource<bool> _navigationTcs;
    private Func<Uri, bool, bool> _onNavigation;

    public bool ClearCookies { get; set; }

    public PopupBrowser()
    {
        DefaultStyleKey = typeof(PopupBrowser);

        _popup = new Popup()
        {
            Child = this,
            PlacementTarget = (FrameworkElement)Window.Current.Content,
            IsOpen = true,
            Visibility = Visibility.Collapsed,
        };
    }

    public async Task StartAsync(Uri navigateTo, Func<Uri, bool, bool> onNavigation = null)
    {
        await _constructionTcs.Task;

        _onNavigation = onNavigation ?? ((uri, _) => uri == navigateTo);
        _navigationTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _webView.Source = navigateTo;

        await _navigationTcs.Task;

        _onNavigation = null;
    }

    public void Open()
    {
        _popup.Visibility = Visibility.Visible;
    }

    public void Close()
    {
        _popup.Visibility = Visibility.Collapsed;
    }

    public async Task<int> GetHttpStatusAsync()
    {
        return int.Parse(await _webView.CoreWebView2.ExecuteScriptAsync("window.performance.getEntries()[0].responseStatus"));
    }

    public async Task<string> GetHtmlAsync()
    {
        return await _webView.CoreWebView2.ExecuteScriptAsync("document.body.outerHTML");
    }

    public async Task<IEnumerable<HttpCookie>> GetCookiesAsync(Uri uri, Func<CoreWebView2Cookie, bool> predicate = null)
    {
        IEnumerable<CoreWebView2Cookie> cookies = await _webView.CoreWebView2.CookieManager.GetCookiesAsync(uri.ToString());
        if (predicate is not null)
        {
            cookies = cookies.Where(predicate);
        }

        return cookies.Select(cookie => 
            new HttpCookie(cookie.Name, cookie.Domain, cookie.Path) 
            { 
                Value = cookie.Value, 
                HttpOnly = cookie.IsHttpOnly, 
                Secure = cookie.IsSecure 
            });
    }

    public async Task DeleteCookiesAsync(Uri uri)
    {
        foreach (CoreWebView2Cookie cookie in await _webView.CoreWebView2.CookieManager.GetCookiesAsync(uri.ToString()))
        {
            _webView.CoreWebView2.CookieManager.DeleteCookie(cookie);
        }
    }

    public void Dispose()
    {
        if (_webView is not null)
        {
            Window.Current.SizeChanged -= Window_SizeChanged;
            _closeButton.Click -= CloseButton_Click;
            _webView.CoreProcessFailed -= WebView_CoreProcessFailed;
            _webView.CoreWebView2Initialized -= WebView_CoreWebView2Initialized;
            _webView.NavigationStarting -= WebView_NavigationStarting;
            _webView.NavigationCompleted -= WebView_NavigationCompleted;
            if (_webView.CoreWebView2 is not null)
            {
                _webView.CoreWebView2.ContentLoading -= WebView_ContentLoading;
            }
            _webView = null;
        }

        if (_popup is not null)
        {
            _popup.IsOpen = false;
            _popup = null;
        }
    }

    protected override void OnApplyTemplate()
    {
        _constructionTcs.TrySetResult(true);

        Window.Current.SizeChanged += Window_SizeChanged;
        _border = (Border)GetTemplateChild("Border");
        _textBlock = (TextBlock)GetTemplateChild("TextBlock");
        _closeButton = (Button)GetTemplateChild("CloseButton");
        _closeButton.Click += CloseButton_Click;

        _webView = (WebView2)GetTemplateChild("WebView");
        _webView.CoreProcessFailed += WebView_CoreProcessFailed;
        _webView.CoreWebView2Initialized += WebView_CoreWebView2Initialized;
        _webView.NavigationStarting += WebView_NavigationStarting;
        _webView.NavigationCompleted += WebView_NavigationCompleted;

        SetSize();
    }

    private void WebView_CoreProcessFailed(WebView2 sender, CoreWebView2ProcessFailedEventArgs args)
    {
        Debug.WriteLine($"{args.ProcessDescription} {args.ProcessFailedKind} {args.Reason}");
    }

    private void WebView_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
    {
        _webView.CoreWebView2.ContentLoading += WebView_ContentLoading;
        if (ClearCookies)
        {
            _webView.CoreWebView2.CookieManager.DeleteAllCookies();
        }
    }

    private void WebView_ContentLoading(CoreWebView2 sender, CoreWebView2ContentLoadingEventArgs args)
    {
        if (args.NavigationId == _lastNavigation.Id)
        {
            if (_onNavigation(_lastNavigation.Uri, _lastNavigation.IsRedirect))
            {
                _navigationTcs?.TrySetResult(true);
            }
        }
        _lastNavigation = default;
    }

    private void WebView_NavigationStarting(WebView2 _, CoreWebView2NavigationStartingEventArgs args)
    {
        _lastNavigation = (args.NavigationId, new Uri(args.Uri), args.IsRedirected);
        _textBlock.Text = args.Uri;
    }

    private void WebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    { 
        _webView.Focus(FocusState.Programmatic);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
        _navigationTcs?.TrySetResult(true);
    }

    private void SetSize()
    {
        var bounds = Window.Current.Bounds;
        _border.Width = bounds.Width;
        _border.Height = bounds.Height;
        _border.Padding = new Thickness(bounds.Width * .07, bounds.Height * .07, bounds.Width * .07, bounds.Height * .07);
    }

    private void Window_SizeChanged(object sender, WindowSizeChangedEventArgs e)
    {
        SetSize();
    }
}
