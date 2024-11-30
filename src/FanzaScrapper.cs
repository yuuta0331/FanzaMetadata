using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Extensions;
using Playnite.SDK;

namespace FanzaMetadata;

public class FanzaScrapper(ILogger logger)
{
    private const SupportedLanguages DefaultLanguage = SupportedLanguages.ja_JP;

    public async Task<FanzaScrapperResult> ScrapGamePage(string url, string SearchCategory, SupportedLanguages language = DefaultLanguage)
    {
        if (!IsValidUrl(url))
        {
            logger.Error($"Invalid URL: {url}");
            return null;
        }

        if (!url.Contains("?locale="))
        {
            url += $"?locale={language.ToString()}";
        }

        var result = new FanzaScrapperResult
        {
            Links = new Dictionary<string, string>
            {
                { "Fanza", url }
            }
        };

        result.Age = FanzaScrapperResult.AgeRating.Adult;

        var cookieContainer = new CookieContainer();
        cookieContainer.Add(new Uri("https://dlsoft.dmm.co.jp"), new Cookie("age_check_done", "1"));
        AddCookiesForDomain(url, cookieContainer);
        var handler = new HttpClientHandler { CookieContainer = cookieContainer };
        var client = new HttpClient(handler);

        string responseBody;
        try
        {
            responseBody = await client.GetStringAsync(url);
        }
        catch (Exception e)
        {
            Console.WriteLine($"ERROR: Failed to fetch URL: {url} - {e.Message}");
            return null;
        }

        var context = BrowsingContext.New(Configuration.Default);
        var document = await context.OpenAsync(req => req.Content(responseBody));

        var isDoujinCategory = SearchCategory == "Doujin Games";
        var titleElement = document.QuerySelector(isDoujinCategory
            ? "h1.productTitle__txt"
            : "h1.productTitle__headline");

        if (titleElement != null)
        {
            foreach (var span in titleElement.QuerySelectorAll("span"))
            {
                span.Remove();
            }

            result.Title = titleElement.TextContent.Trim();
        }
        var brandElement = document.QuerySelector(isDoujinCategory
            ? ".circleName__txt"
            : ".component-textLink[title*='ブランド']");
        result.Author = brandElement?.TextContent.Trim();

        if (isDoujinCategory)
        {
            var ratingElement = document.QuerySelector("div.userReview__item a span");
            var ratingClass = ratingElement?.ClassName.Replace("u-common__ico--review", "").Trim();
            if (!string.IsNullOrEmpty(ratingClass))
            {
                var match = Regex.Match(ratingClass, @"\d+(\.\d+)?");
                if (match.Success && double.TryParse(match.Value, out double ratingValue))
                {
                    result.Rating = Math.Min((int)(ratingValue / 10), 100);
                }
            }
        }
        else
        {
            var ratingElements = document.QuerySelectorAll("div.reviewStars span.reviewStars__star svg path[fill='#FFAA47']");
            result.Rating = Math.Min(ratingElements.Length, 100);
        }

        var images = ParseImages(document, isDoujinCategory);

        result.MainImage = images.FirstOrDefault();
        result.Icon = result.MainImage?.Replace("pl.jpg", "ps.jpg");
        result.ProductImages = images;
        var descriptionElement = document.QuerySelector(isDoujinCategory
            ? "p.summary__txt"
            : "p.text-overflow");
        result.Description = descriptionElement?.InnerHtml.Trim();

        var informationItems = document.QuerySelectorAll(isDoujinCategory
            ? "div.m-productInformation div.productInformation__item"
            : ".contentsDetailBottom__tableRow");

        foreach (var item in informationItems)
        {
            string header = isDoujinCategory
                ? item.QuerySelector("dt.informationList__ttl")?.TextContent.Trim()
                : item.QuerySelector(".contentsDetailBottom__tableDataLeft")?.TextContent.Trim();

            var dataElement = isDoujinCategory
                ? item.QuerySelector("dd.informationList__txt, dd.informationList__item")
                : item.QuerySelector(".contentsDetailBottom__tableDataRight");

            if (header == null || dataElement == null) continue;

            if (header.Contains(TranslationDictionary.ReleaseDate[language]))
            {
                var releaseDateText = dataElement.TextContent.Trim();
                if (!string.IsNullOrEmpty(releaseDateText))
                {
                    releaseDateText = releaseDateText.Split(' ')[0];

                    DateTime releaseDate;
                    switch (language)
                    {
                        case SupportedLanguages.ja_JP:
                            DateTime.TryParseExact(releaseDateText, "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out releaseDate);
                            break;
                        case SupportedLanguages.en_US:
                            DateTime.TryParseExact(releaseDateText, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out releaseDate);
                            break;
                        default:
                            DateTime.TryParse(releaseDateText, out releaseDate);
                            break;
                    }
                    result.ReleaseDate = releaseDate;
                }
            }
            else if (header.Contains(TranslationDictionary.Series[language]))
            {
                var seriesElement = dataElement.QuerySelector("a");
                if (seriesElement != null)
                {
                    result.Series = seriesElement.TextContent.Trim();
                }
            }
            else if (header.Contains(TranslationDictionary.Illustration[language]))
            {
                result.Illustrators = dataElement.QuerySelectorAll("a")
                    .Select(a => a.TextContent.Trim())
                    .ToList();
            }
            else if (header.Contains(TranslationDictionary.Scenario[language]))
            {
                result.ScenarioWriters = dataElement.QuerySelectorAll("a")
                    .Select(a => a.TextContent.Trim())
                    .ToList();
            }
            else if (header.Contains(TranslationDictionary.VoiceActor[language]))
            {
                result.VoiceActors = dataElement.QuerySelectorAll("a")
                    .Select(a => a.TextContent.Trim())
                    .ToList();
            }
            else if (header.Contains(TranslationDictionary.Genre[language]))
            {
                var excludeKeywords = new List<string>
                {
                    "キャンペーン", "ブラウザ対応", "Windows", "%OFF", "ポイント還元", "アワード", "デモ・体験版", "セール", "販売", "商品", "新作", "成人向け"
                };

                result.Genres = dataElement.QuerySelectorAll("a")
                    .Select(a => a.TextContent.Trim())
                    .Where(genre => !excludeKeywords.Any(keyword => genre.Contains(keyword)))
                    .ToList();
            }
        }


        return result;
    }

    public async Task<List<FanzaSearchResult>> ScrapSearchPage(
        string searchCategory,
        string query,
        int maxSearchResults = 30,
        SupportedLanguages language = DefaultLanguage
    )
    {
        var baseUrl = FanzaMetadataSettings.GetSearchCategoryBaseUrl(searchCategory);
        var searchPath = FanzaMetadataSettings.GetSearchPath(searchCategory);
        var parameters = FanzaMetadataSettings.GetDefaultParameters(searchCategory);
        var searchUrl = $"{baseUrl}{searchPath}{Uri.EscapeDataString(query)}{parameters}";

        var cookieContainer = new CookieContainer();

        AddCookiesForDomain(baseUrl, cookieContainer);

        var handler = new HttpClientHandler { CookieContainer = cookieContainer };
        var client = new HttpClient(handler);
        string responseBody;

        try
        {
            responseBody = await client.GetStringAsync(searchUrl);
        }
        catch (HttpRequestException e)
        {
            logger.Error(e, $"Failed to fetch URL: {searchUrl}");
            throw;
        }

        var context = BrowsingContext.New(Configuration.Default);
        var document = await context.OpenAsync(req => req.Content(responseBody));

        var isDoujinCategory = searchCategory == "Doujin Games";
        var resultSelector = isDoujinCategory ? "li.productList__item" : "li.component-legacy-productTile__item";
        var searchResultsRows = document.QuerySelectorAll(resultSelector);
        if (!searchResultsRows.Any())
        {
            var debugHtml = document.QuerySelector("ul#doujin_list")?.OuterHtml ?? document.Body.OuterHtml;
            logger.Warn("No search results found. Debugging HTML:");
            logger.Warn(debugHtml);
        }

        var searchResults = new List<FanzaSearchResult>();

        foreach (var row in searchResultsRows)
        {
            var titleElement = isDoujinCategory
                ? row.QuerySelector(".tileListTtl__txt a")
                : row.QuerySelector("span.component-legacy-productTile__title");

            var linkElement = isDoujinCategory
                ? row.QuerySelector(".tileListTtl__txt a")
                : row.QuerySelector("a.component-legacy-productTile__detailLink");

            var imageElement = isDoujinCategory
                ? row.QuerySelector(".tileListImg__tmb img")
                : row.QuerySelector("img");

            var title = titleElement?.Text().Trim();
            var link = linkElement?.GetAttribute("href");
            if (string.IsNullOrEmpty(link))
            {
                logger.Warn($"Skipped an item due to missing link. Debugging HTML: {row.OuterHtml}");
                continue;
            }

            if (!link.StartsWith("http"))
            {
                link = $"{baseUrl.TrimEnd('/')}/{link.TrimStart('/')}";
            }

            var image = imageElement?.GetAttribute("src");

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(link))
            {
                logger.Warn("Skipped an item due to missing title or link.");
                continue;
            }

            var AuthorOrDescriptionElement = row.QuerySelector(isDoujinCategory
                ? "div.tileListTtl__txt a"
                : "div.component-legacy-productTile__relatedInfo a[href*='/list/article=author']");
            var brandElement = row.QuerySelector(isDoujinCategory
                ? "div.tileListTtl__txt--author a"
                : "div.component-legacy-productTile__relatedInfo a[href*='/list/article=maker']");
            var AuthorOrDescription = AuthorOrDescriptionElement?.Text().Trim();
            var brand = brandElement?.Text().Trim();
            var excerpt = !string.IsNullOrEmpty(AuthorOrDescription) || !string.IsNullOrEmpty(brand)
                ? $"{AuthorOrDescription ?? "Unknown"}\n{brand ?? "Unknown"}"
                : null;


            searchResults.Add(new FanzaSearchResult
            {
                Title = title,
                Link = link,
                Excerpt = excerpt
            });
        }

        if (!searchResults.Any())
        {
            logger.Error("No valid search results found.");
        }

        return searchResults.Take(maxSearchResults).ToList();
    }

    private void AddCookiesForDomain(string baseUrl, CookieContainer cookieContainer)
    {
        if (baseUrl.Contains("dlsoft.dmm.co.jp"))
        {
            cookieContainer.Add(new Uri("https://dlsoft.dmm.co.jp"), new Cookie("age_check_done", "1"));
        }
        else if (baseUrl.Contains("www.dmm.co.jp"))
        {
            cookieContainer.Add(new Uri("https://www.dmm.co.jp"), new Cookie("age_check_done", "1"));
            cookieContainer.Add(new Uri("https://www.dmm.co.jp"), new Cookie("dc_doujin_age_check_done", "1"));
        }
        else
        {
            throw new ArgumentException($"Unknown base URL: {baseUrl}");
        }
    }

    private static List<string> ParseImages(IDocument document, bool isDoujinCategory)
    {
        var images = new List<string>();

        if (isDoujinCategory)
        {
            var mainImageElement = document.QuerySelector("img[src*='doujin-assets.dmm.co.jp'][src*='pr.jpg']");
            if (mainImageElement != null)
            {
                var mainImageSrc = mainImageElement.GetAttribute("src")?.Trim();
                if (!string.IsNullOrEmpty(mainImageSrc))
                {
                    images.Add(mainImageSrc);
                }
            }

            var subImageElements = document.QuerySelectorAll("a.fn-colorbox img[src*='doujin-assets.dmm.co.jp']");
            foreach (var element in subImageElements)
            {
                var subImageSrc = element.GetAttribute("src")?.Trim();
                if (!string.IsNullOrEmpty(subImageSrc) && !images.Contains(subImageSrc))
                {
                    images.Add(subImageSrc);
                }
            }
        }
        else
        {
            var imagesContainer = document.QuerySelector(".slider-area");

            if (imagesContainer is null) return images;

            var imageElements = imagesContainer.QuerySelectorAll("img[src*='pics.dmm.co.jp']")
                .Select(x => x.GetAttribute("src"))
                .Where(src => src is not null)
                .Select(src => src!.Trim())
                .Distinct()
                .ToList();

            images.AddRange(imageElements);
        }

        return images;
    }


    public static bool IsValidUrl(string url)
    {
        var match = Regex.Match(url,
            @"https://(dlsoft\.dmm\.co\.jp/detail/[a-z0-9_]+/|www\.dmm\.co\.jp/dc/doujin/-/detail/=/cid=[a-z0-9_]+)/?.*");
        return match.Success;
    }
}