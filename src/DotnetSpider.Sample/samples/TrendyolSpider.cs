using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using DotnetSpider.DataFlow;
using DotnetSpider.DataFlow.Parser;
using DotnetSpider.Http;
using DotnetSpider.Selector;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;
using Serilog;

namespace DotnetSpider.Sample.samples;

[DisplayName("Trendyol Flaş Ürünler Taraması")]
public class TrendyolSpider(
    IOptions<SpiderOptions> options,
    DependenceServices services,
    ILogger<Spider> logger)
    : Spider(options, services, logger)
{
    public static async Task RunAsync()
    {
        var builder = Builder.CreateDefaultBuilder<TrendyolSpider>(x =>
        {
            x.Speed = 1;
            x.EmptySleepTime = 5;
        });
        builder.ConfigureServices(services =>
        {
            services.AddKeyedSingleton<IDownloader, PuppeteerDownloader>(nameof(HttpClientDownloader));
        });
        builder.UseSerilog();
        await builder.Build().RunAsync();
    }

    protected override async Task InitializeAsync(CancellationToken stoppingToken = default)
    {
        // Trendyol Arama / Tag URL'si
        var targetUrl = "https://www.trendyol.com/sr?tag=fs_4_8_2026_21_24%2Cfs_4_8_2026_0_24";

        var request = new Request(targetUrl);

        // Anti-bot taramalarında gerçek bir tarayıcı (Chrome) Header'ları simüle edilir
        request.Headers.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36";
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
        request.Headers.Add("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");
        request.Headers.Add("Sec-Ch-Ua", "\"Chromium\";v=\"122\", \"Not(A:Brand\";v=\"24\", \"Google Chrome\";v=\"122\"");
        request.Headers.Add("Sec-Ch-Ua-Mobile", "?0");
        request.Headers.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
        request.Headers.Add("Sec-Fetch-Dest", "document");
        request.Headers.Add("Sec-Fetch-Mode", "navigate");
        request.Headers.Add("Sec-Fetch-Site", "none");
        request.Headers.Add("Sec-Fetch-User", "?1");
        request.Headers.Add("Upgrade-Insecure-Requests", "1");

        await AddRequestsAsync(request);

        // Veri Parse Adımı ve Konsol Çıktısı (ConsoleStorage)
        AddDataFlow<TrendyolParser>();
        AddDataFlow<ConsoleStorage>();
    }

    protected class TrendyolParser : DataParser
    {
        public override Task InitializeAsync()
        {
            AddRequiredValidator("trendyol\\.com");
            return Task.CompletedTask;
        }

        protected override Task ParseAsync(DataFlowContext context)
        {
            var htmlContent = context.Response.ReadAsString();

            // Cloudflare engeli kontrolü
            if (htmlContent.Contains("Just a moment...") || htmlContent.Contains("cf-challenge"))
            {
                Logger.LogWarning("Trendyol Cloudflare Anti-Bot koruması tespit edildi (Just a moment...).");
                context.AddData("HATA", "Cloudflare anti-bot engeline takıldı. Headless Browser (Puppeteer/Playwright) veya Proxy kullanılması gerekebilir.");
                return Task.CompletedTask;
            }

            // HTML Ürün Kartları Listesi (XPath ile seçilir)
            var productCards = context.Selectable.SelectList(Selectors.XPath("//div[contains(@class, 'p-card-chldrn') or contains(@class, 'p-card-wrppr')]"));

            if (productCards != null && productCards.Count() > 0)
            {
                int index = 1;
                foreach (var card in productCards)
                {
                    var brand = card.Select(Selectors.XPath(".//span[contains(@class, 'prdct-desc-cntnr-ttl')]"))?.Value?.Trim();
                    var name = card.Select(Selectors.XPath(".//span[contains(@class, 'prdct-desc-cntnr-name')]"))?.Value?.Trim();
                    var price = card.Select(Selectors.XPath(".//div[contains(@class, 'prc-box-dscntd') or contains(@class, 'prc-box-sllng')]"))?.Value?.Trim();
                    var link = card.Select(Selectors.XPath(".//a/@href"))?.Value?.Trim();

                    if (!string.IsNullOrEmpty(link) && !link.StartsWith("http"))
                    {
                        link = "https://www.trendyol.com" + link;
                    }

                    context.AddData($"Ürün #{index++}", new
                    {
                        Marka = brand,
                        ÜrünAdı = name,
                        Fiyat = price,
                        Link = link
                    });
                }
            }
            else
            {
                // Alternatif olarak sayfa başlığını ve ham yanıt bilgisini ekle
                var pageTitle = context.Selectable.XPath("//title")?.Value?.Trim();
                context.AddData("SayfaBaşlığı", pageTitle);
                context.AddData("Durum", "Ürün kartları bulunamadı (Sayfa dinamik JavaScript ile yükleniyor olabilir).");
            }

            return Task.CompletedTask;
        }
    }
}
