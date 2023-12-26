using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cycloid.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

namespace cycloid;

public class Strava
{
    private static readonly Uri LoginUri = new("https://www.strava.com/login");
    private static readonly Uri DashboardUri = new("https://www.strava.com/dashboard");
    private static readonly Uri CookieUri = new("https://strava.com");

    private Page Page => (Page)((Frame)Window.Current.Content).Content;

    public Uri BaseUri { get; }

    public string HeatmapUri { get; }

    public Strava()
    {
        BaseUri = new Uri("https://heatmap-external-" + (char)('a' + new Random().Next(3)) + ".strava.com");
        HeatmapUri = new Uri(BaseUri, "/tiles-auth/ride/hot/{zoomlevel}/{x}/{y}.png").ToString();
    }

    public async Task<HttpCookie[]> LoginAsync()
    {
        using PopupBrowser browser = PopupBrowser.Create((Panel)Page.Content);

        (bool loggedIn, IEnumerable<HttpCookie> cookies) = await LoginAsync(browser);

        return loggedIn ? cookies.ToArray() : null;
    }

    public async Task<bool> InitializeHeatmapAsync()
    {
        using HttpBaseProtocolFilter filter = new();
        using HttpClient client = new(filter);
        filter.CacheControl.ReadBehavior = HttpCacheReadBehavior.NoCache;

        if (await IsHeatmapAuthenticatedAsync())
        {
            return true;
        }
        
        using PopupBrowser browser = PopupBrowser.Create((Panel)Page.Content);

        (bool loggedIn, _) = await LoginAsync(browser);
        if (loggedIn)
        {
            await browser.StartAsync(new Uri("https://heatmap-external-a.strava.com/auth"));

            if (await browser.GetHttpStatusAsync() == 200)
            {
                IEnumerable<HttpCookie> cookies = await browser
                    .GetCookiesAsync(
                        CookieUri,
                        cookie => cookie.Name.StartsWith("CloudFront", StringComparison.OrdinalIgnoreCase));

                // That's the way to add cookies to be used by a map tile datasource
                foreach (HttpCookie cookie in cookies)
                {
                    filter.CookieManager.SetCookie(cookie);
                }

                return true;
            }
        }

        return false;

        async Task<bool> IsHeatmapAuthenticatedAsync()
        {
            HttpRequestMessage request = new(HttpMethod.Get, new Uri(BaseUri, "tiles-auth/ride/hot/15/5448/12688.png"));
            request.Headers.CacheControl.Add(new("no-cache"));
            
            return (await client.SendRequestAsync(request)).StatusCode == HttpStatusCode.Ok;
        }
    }

    private async Task<(bool Success, IEnumerable<HttpCookie> Cookies)> LoginAsync(PopupBrowser browser)
    {
        var success = false;
        
        browser.BrowserNavigated += OnBrowserNavigated;
        await browser.StartAsync(DashboardUri);
        browser.BrowserNavigated -= OnBrowserNavigated;

        IEnumerable<HttpCookie> cookies = !success ? null : await browser.GetCookiesAsync(CookieUri);

        return (success, cookies);

        void OnBrowserNavigated(Uri uri, bool isRedirect)
        {
            if (isRedirect && uri.Equals(LoginUri))
            {
                browser.Open();
            }
            else if (uri.Equals(DashboardUri))
            {
                success = true;
                browser.Close();
            }
        }
    }
}