using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml;
using System;
using Windows.Web.Http.Filters;
using Windows.Web.Http;
using System.Linq;
using Microsoft.VisualStudio.Threading;
using System.Collections.Generic;

namespace cycloid;

public class Strava
{
    private static readonly Uri LoginUri = new("https://www.strava.com/login");
    private static readonly Uri DashboardUri = new("https://www.strava.com/dashboard");
    private static readonly Uri CookieUri = new("https://strava.com");

    private Page Page => (Page)((Frame)Window.Current.Content).Content;

    public async Task<HttpCookie[]> LoginAsync()
    {
        using PopupBrowser browser = PopupBrowser.Create((Panel)Page.Content);

        return (await LoginAsync(browser)).Cookies.ToArray();
    }

    public async Task<(bool Success, string UriFormat, HttpCookie[] Cookies)> InitializeHeatmapAsync()
    {
        Uri baseUrl = new("https://heatmap-external-" + (char)('a' + new Random().Next(3)) + ".strava.com");

        using PopupBrowser browser = PopupBrowser.Create((Panel)Page.Content);

        await browser.StartAsync(new Uri("https://heatmap-external-a.strava.com/auth"));
        int status = await browser.GetHttpStatusAsync();
        if (status >= 400)
        {
            await LoginAsync(browser);
            await browser.StartAsync(new Uri("https://heatmap-external-a.strava.com/auth"));
            status = await browser.GetHttpStatusAsync();
        }

        if (status == 200)
        {
            HttpCookie[] cookies = (await browser.GetCookiesAsync(
                CookieUri,
                cookie => cookie.Name.StartsWith("CloudFront", StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            //HttpBaseProtocolFilter filter = new();
            //foreach (HttpCookie cookie in cookies)
            //{
            //    filter.CookieManager.SetCookie(cookie);
            //}
            //HttpClient client = new(filter);
            //HttpRequestMessage request = new(HttpMethod.Get, new Uri(baseUrl, "/tiles-auth/ride/hot/15/5448/12688.png"));
            //HttpResponseMessage response = await client.SendRequestAsync(request);

            return (true, new Uri(baseUrl, "/tiles-auth/ride/hot/{zoomlevel}/{x}/{y}.png").ToString(), cookies);
        }

        return default;
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