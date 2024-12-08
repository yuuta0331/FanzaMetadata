using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;
using Playnite.SDK;

namespace FanzaMetadata
{
    public class FanzaScrapper
    {
        private const SupportedLanguages DefaultLanguage = SupportedLanguages.ja_JP;
        private readonly ILogger _logger;

        public FanzaScrapper(ILogger logger)
        {
            _logger = logger;
        }

        public async Task<List<FanzaSearchResult>> ScrapSearchPage(string searchCategory, string query, int maxSearchResults = 30, SupportedLanguages language = DefaultLanguage)
        {
            var baseUrl = FanzaMetadataSettings.GetSearchCategoryBaseUrl(searchCategory);
            var searchPath = FanzaMetadataSettings.GetSearchPath(searchCategory);
            var parameters = FanzaMetadataSettings.GetDefaultParameters(searchCategory);
            var searchUrl = $"{baseUrl}{searchPath}{Uri.EscapeDataString(query)}{parameters}";

            try
            {
                if (searchCategory == "ALL")
                {
                    var generalCategoryResults = await ScrapSearchPageForCategory("PC Games", query, maxSearchResults, language);
                    var doujinCategoryResults = await ScrapSearchPageForCategory("Doujin Games", query, maxSearchResults, language);

                    var combinedResults = InterleaveResults(generalCategoryResults, doujinCategoryResults, maxSearchResults);

                    return combinedResults;
                }

                return await ScrapSearchPageForCategory(searchCategory, query, maxSearchResults, language);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error scraping search page: {searchUrl}");
                return new List<FanzaSearchResult>();
            }
        }

        private async Task<List<FanzaSearchResult>> ScrapSearchPageForCategory(string searchCategory, string query, int maxSearchResults, SupportedLanguages language)
        {
            var baseUrl = FanzaMetadataSettings.GetSearchCategoryBaseUrl(searchCategory);
            var searchPath = FanzaMetadataSettings.GetSearchPath(searchCategory);
            var parameters = FanzaMetadataSettings.GetDefaultParameters(searchCategory);
            var searchUrl = $"{baseUrl}{searchPath}{Uri.EscapeDataString(query)}{parameters}";

            try
            {
                var document = await FetchDocumentAsync(searchUrl);
                if (document == null) return new List<FanzaSearchResult>();

                var isDoujinCategory = searchCategory == "Doujin Games";
                return ExtractSearchResults(baseUrl, document, isDoujinCategory, maxSearchResults);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error scraping search page: {searchUrl}");
                return new List<FanzaSearchResult>();
            }
        }

        private List<FanzaSearchResult> InterleaveResults(List<FanzaSearchResult> generalResults, List<FanzaSearchResult> doujinResults, int maxResults)
        {
            var combinedResults = new List<FanzaSearchResult>();

            int generalIndex = 0, doujinIndex = 0;
            while (combinedResults.Count < maxResults)
            {
                if (generalIndex < generalResults.Count)
                {
                    combinedResults.Add(generalResults[generalIndex]);
                    generalIndex++;
                }

                if (doujinIndex < doujinResults.Count)
                {
                    combinedResults.Add(doujinResults[doujinIndex]);
                    doujinIndex++;
                }

                if (generalIndex >= generalResults.Count && doujinIndex >= doujinResults.Count)
                {
                    break;
                }
            }

            return combinedResults;
        }

        private List<FanzaSearchResult> ExtractSearchResults(string baseUrl, IDocument document, bool isDoujinCategory,
            int maxResults)
        {
            var resultSelector = isDoujinCategory ? "li.productList__item" : "li.component-legacy-productTile__item";
            var rows = document.QuerySelectorAll(resultSelector);

            var results = new List<FanzaSearchResult>();
            foreach (var row in rows)
            {
                var titleElement = row.QuerySelector(isDoujinCategory
                    ? ".tileListTtl__txt a"
                    : "span.component-legacy-productTile__title");
                var linkElement = row.QuerySelector(isDoujinCategory
                    ? ".tileListTtl__txt a"
                    : "a.component-legacy-productTile__detailLink");

                var title = titleElement?.TextContent.Trim();
                var link = linkElement?.GetAttribute("href");

                if (!string.IsNullOrEmpty(link) && !link.StartsWith("http"))
                {
                    link = $"{baseUrl.TrimEnd('/')}/{link.TrimStart('/')}";
                }

                var authorElement = row.QuerySelector(isDoujinCategory
                    ? "div.tileListTtl__txt--author a"
                    : "div.component-legacy-productTile__relatedInfo a[href*='/list/article=maker']");

                var illustratorElement = row.QuerySelector(isDoujinCategory
                    ? ""
                    : "div.component-legacy-productTile__relatedInfo a[href*='/list/article=author']");

                var author = authorElement?.TextContent.Trim();
                var illustrator = illustratorElement?.TextContent.Trim();

                if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(link))
                {
                    _logger.Warn("Skipped an item due to missing title or link.");
                    continue;
                }

                results.Add(new FanzaSearchResult
                {
                    Title = title,
                    Link = link,
                    Excerpt =
                        $"{(string.IsNullOrEmpty(illustrator) ? "" : illustrator)}\n{(string.IsNullOrEmpty(author) ? "Unknown Brand" : author)}"
                });

                if (results.Count >= maxResults) break;
            }

            return results;
        }

        public async Task<FanzaScrapperResult> ScrapGamePage(string url, FanzaMetadataSettings settings)
        {
            string searchCategory = FanzaMetadataSettings.DetermineCategoryFromUrl(url);
            SupportedLanguages language = settings.GetSupportedLanguage();

            if (!IsValidUrl(url))
            {
                _logger.Error($"Invalid URL: {url}");
                return null;
            }

            url = EnsureLocaleInUrl(url, language);
            var result = InitializeResult(url);

            try
            {
                var document = await FetchDocumentAsync(url);
                if (document == null) return null;

                var isDoujinCategory = searchCategory == "Doujin Games";

                result.Title = ExtractTitle(document, isDoujinCategory);
                result.Author = ExtractAuthor(document, isDoujinCategory);
                result.Rating = ExtractRating(document, isDoujinCategory);
                result.ProductImages = ParseImages(document, isDoujinCategory);
                result.MainImage = result.ProductImages.FirstOrDefault();
                result.Icon = result.MainImage?.Replace("pl.jpg", "ps.jpg");
                // result.Description = ExtractDescription(_logger, document, isDoujinCategory);
                result.Description = await ExtractDescription(_logger, settings.GetCurrentBaseUrl(), document, isDoujinCategory, FetchDocumentAsync);


                ExtractAdditionalInformation(document, result, isDoujinCategory, language);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error scraping game page: {url}");
                return null;
            }

            return result;
        }

        private static string EnsureLocaleInUrl(string url, SupportedLanguages language)
        {
            return url.Contains("?locale=") ? url : $"{url}?locale={language}";
        }

        private FanzaScrapperResult InitializeResult(string url)
        {
            return new FanzaScrapperResult
            {
                Links = new Dictionary<string, string> { { "Fanza", url } },
                Age = FanzaScrapperResult.AgeRating.Adult
            };
        }

        private async Task<IDocument> FetchDocumentAsync(string url)
        {
            try
            {
                var handler = CreateHttpClientHandler(url);
                using var client = new HttpClient(handler);
                var responseBody = await client.GetStringAsync(url);
                var context = BrowsingContext.New(Configuration.Default);
                return await context.OpenAsync(req => req.Content(responseBody));
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to fetch URL: {url} - {ex.Message}");
                return null;
            }
        }

        private HttpClientHandler CreateHttpClientHandler(string url)
        {
            var cookieContainer = new CookieContainer();
            AddCookiesForDomain(url, cookieContainer);
            return new HttpClientHandler { CookieContainer = cookieContainer };
        }

        private void AddCookiesForDomain(string url, CookieContainer cookieContainer)
        {
            if (url.Contains("dlsoft.dmm.co.jp"))
            {
                cookieContainer.Add(new Uri("https://dlsoft.dmm.co.jp"), new Cookie("age_check_done", "1"));
            }
            else if (url.Contains("www.dmm.co.jp"))
            {
                cookieContainer.Add(new Uri("https://www.dmm.co.jp"), new Cookie("age_check_done", "1"));
                cookieContainer.Add(new Uri("https://www.dmm.co.jp"), new Cookie("dc_doujin_age_check_done", "1"));
            }
        }

        private static string ExtractTitle(IDocument document, bool isDoujinCategory)
        {
            var selector = isDoujinCategory ? "h1.productTitle__txt" : "h1.productTitle__headline";
            var titleElement = document.QuerySelector(selector);

            if (titleElement == null) return null;

            foreach (var span in titleElement.QuerySelectorAll("span"))
            {
                span.Remove();
            }

            return titleElement.TextContent.Trim();
        }

        private static string ExtractAuthor(IDocument document, bool isDoujinCategory)
        {
            var selector = isDoujinCategory ? ".circleName__txt" : ".component-textLink[title*='ブランド']";
            return document.QuerySelector(selector)?.TextContent.Trim();
        }

        private static async Task<string> ExtractDescription(ILogger logger, string baseUrl, IDocument document, bool isDoujinCategory, Func<string, Task<IDocument>> fetchDocumentAsync)
        {
            var selector = isDoujinCategory ? "p.summary__txt" : "p.text-overflow";
            var baseDescription = document.QuerySelector(selector)?.InnerHtml.Trim() ?? string.Empty;

            if (!isDoujinCategory)
            {
                var sections = document.QuerySelectorAll("section.universalSection");
                if (sections == null || !sections.Any())
                {
                    return baseDescription;
                }

                foreach (var section in sections)
                {
                    var headline = section.QuerySelector("h2.universalSection__headline");
                    var headlineText = headline?.TextContent?.Trim() ?? string.Empty;

                    if (headlineText.Contains("作品情報"))
                    {
                        var iframe = section.QuerySelector("iframe#if_view");
                        if (iframe == null)
                        {
                            continue;
                        }

                        var iframeSrc = iframe.GetAttribute("src");
                        if (string.IsNullOrEmpty(iframeSrc))
                        {
                            continue;
                        }

                        var absoluteIframeUrl = new Uri(new Uri(baseUrl), iframeSrc).ToString();

                        try
                        {
                            var iframeDocument = await fetchDocumentAsync(absoluteIframeUrl);
                            if (iframeDocument != null)
                            {
                                var iframeContent = iframeDocument.Body?.InnerHtml.Trim() ?? string.Empty;
                                var fixIframeContent = ConvertBlackTextToWhite(iframeContent);

                                baseDescription += $"\n\n<br><br><br>{fixIframeContent}";
                            }
                            else
                            {
                                logger.Error("The content of the iframe could not be retrieved.");
                            }
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex, "An error occurred while retrieving the content of the iframe.");
                        }

                        break;
                    }
                }
            }

            return baseDescription;
        }

        private static string ConvertBlackTextToWhite(string htmlContent)
        {
            htmlContent = Regex.Replace(
                htmlContent,
                @"(?<=color:\s*)(#000|#333|rgb\(0,\s*0,\s*0\))(;?)",
                "#fff$2",
                RegexOptions.IgnoreCase
            );

            return htmlContent;
        }



        private static int ExtractRating(IDocument document, bool isDoujinCategory)
        {
            if (isDoujinCategory)
            {
                var ratingElement = document.QuerySelector("div.userReview__item a span");
                var ratingClass = ratingElement?.ClassName.Replace("u-common__ico--review", "").Trim();

                if (!string.IsNullOrEmpty(ratingClass) && Regex.Match(ratingClass, @"\d+(\.\d+)?") is Match match && double.TryParse(match.Value, out double ratingValue))
                {
                    return Math.Min((int)(ratingValue / 10), 100);
                }
            }
            else
            {
                var ratingStars = document.QuerySelectorAll("div.reviewStars span.reviewStars__star svg path[fill='#FFAA47']");
                return Math.Min(ratingStars.Length, 100);
            }

            return 0;
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
                if (imagesContainer == null) return images;

                var imageElements = imagesContainer.QuerySelectorAll("img[src*='pics.dmm.co.jp']")
                    .Select(img => img.GetAttribute("src")?.Trim())
                    .Where(src => !string.IsNullOrEmpty(src))
                    .Distinct()
                    .ToList();

                images.AddRange(imageElements);
            }

            return images;
        }

        private void ExtractAdditionalInformation(IDocument document, FanzaScrapperResult result, bool isDoujinCategory, SupportedLanguages language)
        {
            var selector = isDoujinCategory ? "div.m-productInformation div.productInformation__item" : ".contentsDetailBottom__tableRow";
            var informationItems = document.QuerySelectorAll(selector);

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
                    result.ReleaseDate = ParseReleaseDate(releaseDateText, language);
                }
                else if (header.Contains(TranslationDictionary.Series[language]))
                {
                    var seriesElement = dataElement.QuerySelector("a");
                    if (seriesElement != null)
                    {
                        result.Series = seriesElement.TextContent.Trim();
                    }
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
        }

        private static DateTime? ParseReleaseDate(string releaseDateText, SupportedLanguages language)
        {
            releaseDateText = releaseDateText.Split(' ')[0];
            if (string.IsNullOrEmpty(releaseDateText)) return null;

            return language switch
            {
                SupportedLanguages.ja_JP => DateTime.TryParseExact(releaseDateText, "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null,
                SupportedLanguages.en_US => DateTime.TryParseExact(releaseDateText, "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null,
                _ => DateTime.TryParse(releaseDateText, out var date) ? date : null,
            };
        }

        public static bool IsValidUrl(string url)
        {
            return Regex.IsMatch(url, @"https://(dlsoft\.dmm\.co\.jp|www\.dmm\.co\.jp)/.*");
        }
    }
}
