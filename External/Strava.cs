using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using cycloid.Controls;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.Web.Http;
using Windows.Web.Http.Filters;

namespace cycloid.External;

public class Strava
{
    private static readonly Uri LoginUri = new("https://www.strava.com/login");
    private static readonly Uri DashboardUri = new("https://www.strava.com/dashboard");
    private static readonly Uri CookieUri = new("https://strava.com");

    public Uri BaseUri { get; }

    public string HeatmapUri { get; }

    public Strava()
    {
        BaseUri = new Uri("https://heatmap-external-" + (char)('a' + new Random().Next(3)) + ".strava.com");
        HeatmapUri = new Uri(BaseUri, "/tiles-auth/ride/hot/{zoomlevel}/{x}/{y}.png").ToString();
    }

    public async Task<HttpCookie[]> LoginAsync()
    {
        using PopupBrowser browser = new();

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

        using PopupBrowser browser = new();

        if ((await LoginAsync(browser)).Success)
        {
            await browser.StartAsync(new Uri(BaseUri, "auth"));
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

        await browser.StartAsync(DashboardUri, (uri, isRedirect) =>
        {
            if (uri.Equals(DashboardUri))
            {
                success = true;
                browser.Close();
                return true;
            }

            if (isRedirect && uri.Equals(LoginUri))
            {
                browser.Open();
            }
            
            return false;
        });

        IEnumerable<HttpCookie> cookies = !success ? null : await browser.GetCookiesAsync(CookieUri);

        return (success, cookies);
    }
}