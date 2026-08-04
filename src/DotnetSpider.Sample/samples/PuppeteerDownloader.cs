using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotnetSpider.Downloader;
using DotnetSpider.Http;
using Microsoft.Extensions.Logging;
using PuppeteerSharp;

namespace DotnetSpider.Sample.samples;

public class PuppeteerDownloader(ILogger<PuppeteerDownloader> logger) : IDownloader
{
    private static bool _isBrowserDownloaded;
    private static readonly SemaphoreSlim Semaphore = new(1, 1);

    public async Task<Response> DownloadAsync(Request request)
    {
        try
        {
            if (!_isBrowserDownloaded)
            {
                await Semaphore.WaitAsync();
                try
                {
                    if (!_isBrowserDownloaded)
                    {
                        logger.LogInformation("Puppeteer Chrome taranıyor / indiriliyor...");
                        using var browserFetcher = new BrowserFetcher();
                        await browserFetcher.DownloadAsync();
                        _isBrowserDownloaded = true;
                        logger.LogInformation("Puppeteer Chrome hazır.");
                    }
                }
                finally
                {
                    Semaphore.Release();
                }
            }

            var launchOptions = new LaunchOptions
            {
                Headless = true,
                Args =
                [
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-blink-features=AutomationControlled",
                    "--window-size=1920,1080"
                ]
            };

            await using var browser = await Puppeteer.LaunchAsync(launchOptions);
            await using var page = await browser.NewPageAsync();

            await page.SetUserAgentAsync(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            await page.SetViewportAsync(new ViewPortOptions { Width = 1920, Height = 1080 });

            logger.LogInformation("Puppeteer Sharp ile sayfa yükleniyor: {Url}", request.RequestUri);
            var response = await page.GoToAsync(request.RequestUri.ToString(), new NavigationOptions
            {
                WaitUntil = [WaitUntilNavigation.Networkidle2],
                Timeout = 60000
            });

            // Cloudflare / JS rendering beklemesi
            await Task.Delay(3000);

            var html = await page.GetContentAsync();

            return new Response
            {
                RequestHash = request.Hash,
                StatusCode = response?.Status ?? HttpStatusCode.OK,
                Content = new ByteArrayContent(Encoding.UTF8.GetBytes(html)),
                Version = HttpVersion.Version11
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Puppeteer indirme hatası: {Url}", request.RequestUri);
            return new Response
            {
                RequestHash = request.Hash,
                StatusCode = HttpStatusCode.InternalServerError,
                ReasonPhrase = ex.Message,
                Version = HttpVersion.Version11
            };
        }
    }
}
