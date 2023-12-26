using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.Web.Http;

namespace cycloid.Controls;

public class PopupBrowser : Control, IDisposable
{
    public static PopupBrowser Create(Panel parent)
    {
        var popup = new PopupBrowser();
        parent.Children.Add(popup);

        return popup;
    }

    private Popup _popup;
    private Border _border;
    private TextBlock _textBlock;
    private Button _closeButton;
    private WebView2 _webView;

    private string _startUri;
    private (ulong Id, string Uri, bool IsRedirect) _lastNavigation;
    private TaskCompletionSource<bool> _tcs;

    public Action<Uri, bool> BrowserNavigated { get; set; }

    public PopupBrowser()
    {
        DefaultStyleKey = typeof(PopupBrowser);
    }

    public async Task StartAsync(Uri uri)
    {
        ApplyTemplate();

        _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _startUri = uri.ToString();
        _webView.Source = uri;

        await _tcs.Task;
    }

    public async Task<int> GetHttpStatusAsync()
    {
        return int.Parse(await _webView.CoreWebView2.ExecuteScriptAsync("window.performance.getEntries()[0].responseStatus"));
    }

    public async Task<string> GetHtmlAsync()
    {
        return await _webView.CoreWebView2.ExecuteScriptAsync("document.body.outerHTML");
    }

    public void Open()
    {
        _popup.IsOpen = true;
    }

    public void Close()
    {
        _popup.IsOpen = false;
        _tcs?.TrySetResult(true);
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
        Window.Current.SizeChanged -= Window_SizeChanged;
        _closeButton.Click -= CloseButton_Click;
        _webView.CoreProcessFailed -= WebView_CoreProcessFailed;
        _webView.CoreWebView2Initialized -= WebView_CoreWebView2Initialized;
        _webView.CoreWebView2.ContentLoading -= WebView_ContentLoading;
        _webView.NavigationStarting -= WebView_NavigationStarting;
        _webView.NavigationCompleted -= WebView_NavigationCompleted;
        _webView = null;
        ((Panel)Parent).Children.Remove(this);
    }

    protected override void OnApplyTemplate()
    {
        Window.Current.SizeChanged += Window_SizeChanged;
        _popup = (Popup)GetTemplateChild("Popup");
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
        base.OnApplyTemplate();
    }

    private void WebView_CoreProcessFailed(WebView2 sender, CoreWebView2ProcessFailedEventArgs args)
    {
        Debug.WriteLine($"{args.ProcessDescription} {args.ProcessFailedKind} {args.Reason}");
    }

    private void WebView_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
    {
        _webView.CoreWebView2.ContentLoading += WebView_ContentLoading;

        //_webView.CoreWebView2.CookieManager.DeleteAllCookies();
    }

    private void WebView_ContentLoading(CoreWebView2 sender, CoreWebView2ContentLoadingEventArgs args)
    {
        if (args.NavigationId == _lastNavigation.Id)
        {
            if (BrowserNavigated is not null)
            {
                BrowserNavigated?.Invoke(new Uri(_lastNavigation.Uri), _lastNavigation.IsRedirect);
            }
            else if (_lastNavigation.Uri == _startUri)
            {
                Close();
            }

        }
        _lastNavigation = default;
    }

    private void WebView_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        _lastNavigation = (args.NavigationId, args.Uri, args.IsRedirected);
        _textBlock.Text = args.Uri;
    }

    private void WebView_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    { 
        _webView.Focus(FocusState.Programmatic);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
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
