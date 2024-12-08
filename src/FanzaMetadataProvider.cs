using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Threading.Tasks;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace FanzaMetadata;

public class FanzaMetadataProvider(
    IPlayniteAPI playniteApi,
    MetadataRequestOptions options,
    FanzaMetadataSettings settings)
    : OnDemandMetadataProvider
{
    private static readonly ILogger Logger = LogManager.GetLogger();

    private List<MetadataField> _availableFields;

    private FanzaScrapperResult _gameData;
    private bool IsBackgroundDownload => options.IsBackgroundDownload;
    public override List<MetadataField> AvailableFields => _availableFields ??= GetAvailableFields();

    public override string GetDescription(GetMetadataFieldArgs args)
    {
        if (!AvailableFields.Contains(MetadataField.Description)) return base.GetDescription(args);

        return _gameData.Description;
    }

    public override string GetName(GetMetadataFieldArgs args)
    {
        if (!AvailableFields.Contains(MetadataField.Name)) return base.GetName(args);

        return _gameData.Title;
    }

    public override IEnumerable<MetadataProperty> GetAgeRatings(GetMetadataFieldArgs args)
    {
        if (!AvailableFields.Contains(MetadataField.AgeRating)) return base.GetAgeRatings(args);

        var age = _gameData.Age switch
        {
            FanzaScrapperResult.AgeRating.AllAges => "All ages",
            FanzaScrapperResult.AgeRating.RRated => "R-Rated",
            FanzaScrapperResult.AgeRating.Adult => "Adult",
            _ => null
        };

        if (age is null) return base.GetAgeRatings(args);

        var ageRating = playniteApi.Database.AgeRatings
            .Where(x => x.Name is not null)
            .FirstOrDefault(rating => rating.Name.Equals(age, StringComparison.OrdinalIgnoreCase));

        var property = ageRating is null
            ? (MetadataProperty)new MetadataNameProperty(age)
            : new MetadataIdProperty(ageRating.Id);

        return new[] { property };
    }

    public override IEnumerable<MetadataProperty> GetGenres(GetMetadataFieldArgs args)
    {
        if (!AvailableFields.Contains(MetadataField.Genres)) return base.GetGenres(args);

        if (_gameData.Genres == null) return new List<MetadataProperty>();

        var genres = _gameData.Genres
            .Select(genre => (genre,
                playniteApi.Database.Genres.Where(x => x.Name is not null)
                    .FirstOrDefault(x => x.Name.Equals(genre, StringComparison.OrdinalIgnoreCase))))
            .Select(tuple =>
            {
                var (genre, property) = tuple;
                if (property is not null) return (MetadataProperty)new MetadataIdProperty(property.Id);
                return new MetadataNameProperty(genre);
            })
            .ToList();

        return genres;
    }

    public override MetadataFile GetIcon(GetMetadataFieldArgs args)
    {
        if (!AvailableFields.Contains(MetadataField.Icon)) return base.GetIcon(args);

        return new MetadataFile(_gameData.Icon);
    }

    public override IEnumerable<MetadataProperty> GetDevelopers(GetMetadataFieldArgs args)
    {
        if (!AvailableFields.Contains(MetadataField.Developers)) return base.GetDevelopers(args);

        var staff = new List<string>();

        if (_gameData.Author != null) staff.Add(_gameData.Author);

        if (_gameData.Circle != null && _gameData.Author != _gameData.Circle) staff.Add(_gameData.Circle);

        var developers = staff
            .Select(name => (name,
                playniteApi.Database.Companies.Where(x => x.Name is not null).FirstOrDefault(company =>
                    company.Name.Equals(name, StringComparison.OrdinalIgnoreCase))))
            .Select(tuple =>
            {
                var (name, company) = tuple;
                if (company is not null) return (MetadataProperty)new MetadataIdProperty(company.Id);
                return new MetadataNameProperty(name);
            })
            .ToList();

        return developers;
    }

    public override IEnumerable<Link> GetLinks(GetMetadataFieldArgs args)
    {
        if (!AvailableFields.Contains(MetadataField.Links)) return base.GetLinks(args);

        var links = new List<Link>();

        if (_gameData.Links != null) links.AddRange(_gameData.Links.Select(link => new Link(link.Key, link.Value)));

        return links;
    }

    public override int? GetCommunityScore(GetMetadataFieldArgs args)
    {
        if (!AvailableFields.Contains(MetadataField.CommunityScore)) return base.GetCommunityScore(args);

        if (_gameData.Rating == null) return base.GetCommunityScore(args);

        return (int)(_gameData.Rating * 20);
    }

    public override IEnumerable<MetadataProperty> GetPublishers(GetMetadataFieldArgs args)
    {
        var publisher = playniteApi.Database.Companies
            .Where(x => x.Name is not null)
            .FirstOrDefault(company => company.Name.Equals("Fanza", StringComparison.OrdinalIgnoreCase));

        var property = publisher is null
            ? (MetadataProperty)new MetadataNameProperty("Fanza")
            : new MetadataIdProperty(publisher.Id);

        return new[] { property };
    }

    public override ReleaseDate? GetReleaseDate(GetMetadataFieldArgs args)
    {
        if (!AvailableFields.Contains(MetadataField.ReleaseDate)) return base.GetReleaseDate(args);

        if (_gameData.ReleaseDate == null) return base.GetReleaseDate(args);

        return new ReleaseDate(_gameData.ReleaseDate.Value);
    }

    public override IEnumerable<MetadataProperty> GetSeries(GetMetadataFieldArgs args)
    {
        if (!AvailableFields.Contains(MetadataField.Series)) return base.GetSeries(args);

        var series = playniteApi.Database.Series
            .Where(x => x.Name is not null)
            .FirstOrDefault(series => series.Name.Equals(_gameData.Series, StringComparison.OrdinalIgnoreCase));

        var property = series is null
            ? (MetadataProperty)new MetadataNameProperty(_gameData.Series)
            : new MetadataIdProperty(series.Id);

        return new[] { property };
    }

    public override MetadataFile GetCoverImage(GetMetadataFieldArgs args)
    {
        if (!AvailableFields.Contains(MetadataField.CoverImage)) return base.GetCoverImage(args);

        return new MetadataFile(_gameData.MainImage);
    }

    public override MetadataFile GetBackgroundImage(GetMetadataFieldArgs args)
    {
        if (!AvailableFields.Contains(MetadataField.BackgroundImage)) return base.GetBackgroundImage(args);

        if (IsBackgroundDownload)
        {
            var image = _gameData.ProductImages?.FirstOrDefault();
            return image is null ? base.GetBackgroundImage(args) : new MetadataFile(image);
        }

        var imageFileOption = playniteApi.Dialogs.ChooseImageFile(
            _gameData.ProductImages?.Select(image => new ImageFileOption(image)).ToList(),
            "Select Background Image");

        return imageFileOption is null ? base.GetBackgroundImage(args) : new MetadataFile(imageFileOption.Path);
    }

    private void GetMetadata()
    {
        if (_gameData != null) return;

        var fanzaLink = options.GameData.Links?.FirstOrDefault(
            link => link.Name.Equals("Fanza", StringComparison.OrdinalIgnoreCase)
        )?.Url;
        var gameName = options.GameData?.Name;

        var isValidUrl = fanzaLink != null && FanzaScrapper.IsValidUrl(fanzaLink);

        var scrapper = new FanzaScrapper(Logger);

        if (!isValidUrl)
        {
            var selectedGame = (FanzaItemOption)playniteApi.Dialogs.ChooseItemWithSearch(
                null,
                query =>
                {
                    var searchResult = new List<FanzaSearchResult>();
                    var searchTasks = settings.GetAvailableSearchCategory()
                        .Select(category => scrapper.ScrapSearchPage(
                            settings.SearchCategory,
                            query,
                            settings.MaxSearchResults,
                            settings.GetSupportedLanguage()))
                        .ToList();

                    var searchResults = Task.WhenAll(searchTasks);

                    searchResults.Wait();

                    searchResults.Result
                        .SelectMany(x => x)
                        .ToList()
                        .ForEach(game =>
                        {
                            var baseUrl = FanzaMetadataSettings.GetSearchCategoryBaseUrl(settings.SearchCategory);
                            var source = game.Link.Replace(FanzaMetadataSettings.GetSearchCategoryBaseUrl(baseUrl), "").Split('/');
                            var addExcerpt = source.Length > 0 ? source[0] : "Unknown source";
                            searchResult.Add(game);
                        });

                    if (searchResult.Count == 0)
                    {
                        return null;
                    }

                    return
                    [
                        ..searchResult.Select(
                            game => new FanzaItemOption(game.Title, game.Excerpt, game.Link)
                        ).ToList()
                    ];
                }, gameName, "Search Fanza");

            if (selectedGame == null) return;

            fanzaLink = selectedGame.Link;
        }

        if (fanzaLink == null)
        {
            Logger.Warn($"No Fanza link found for {gameName}");
            return;
        }

        try
        {
            var gameTask = scrapper.ScrapGamePage(fanzaLink, settings);
            gameTask.Wait();

            _gameData = gameTask.Result;

            if (_gameData == null) Logger.Warn($"Failed to get metadata for {gameName ?? "the selected game"}");
        }
        catch (Exception)
        {
            playniteApi.Dialogs.ShowErrorMessage(
                $"Failed to fetch metadata from Fanza for {gameName ?? "the selected game"}",
                "FanzaMetadata Error"
            );
        }
    }

    private List<MetadataField> GetAvailableFields()
    {
        if (_gameData == null) GetMetadata();

        var fields = new List<MetadataField>();

        if (_gameData == null) return fields;

        if (_gameData.Title != null) fields.Add(MetadataField.Name);

        if (_gameData.Age != null) fields.Add(MetadataField.AgeRating);

        if (_gameData.Description != null) fields.Add(MetadataField.Description);

        if (_gameData.Genres != null) fields.Add(MetadataField.Genres);

        var addDevelopers = _gameData.Author != null ||
                            _gameData.Circle != null;

        if (addDevelopers) fields.Add(MetadataField.Developers);

        if (_gameData.Icon != null) fields.Add(MetadataField.Icon);

        if (_gameData.Links != null) fields.Add(MetadataField.Links);

        if (_gameData.Rating != null) fields.Add(MetadataField.CommunityScore);

        if (_gameData.ReleaseDate != null) fields.Add(MetadataField.ReleaseDate);

        if (_gameData.Series != null) fields.Add(MetadataField.Series);

        if (_gameData.MainImage != null) fields.Add(MetadataField.CoverImage);

        if (_gameData.ProductImages != null) fields.Add(MetadataField.BackgroundImage);

        return fields;
    }
}