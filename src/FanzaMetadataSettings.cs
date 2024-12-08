using System;
using System.Collections.Generic;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace FanzaMetadata;

public class FanzaMetadataSettings : ObservableObject
{
    private int _maxSearchResults = 30;
    private string _pageLanguage = "Japanese";
    private string _searchCategory = "ALL";

    [DontSerialize]
    public List<string> AvailableSearchCategory { get; } =
    [
        "ALL",
        "PC Games",
        "Doujin Games"
    ];

    [DontSerialize]
    public List<int> MaxSearchResultsSteps { get; } =
    [
        30,
        50,
        100
    ];

    [DontSerialize]
    public List<string> AvailableLanguages { get; } =
    [
        "Japanese",
        "English",
    ];

    public string SearchCategory
    {
        get => _searchCategory;
        set => SetValue(ref _searchCategory, value);
    }

    public string PageLanguage
    {
        get => _pageLanguage;
        set => SetValue(ref _pageLanguage, value);
    }

    public int MaxSearchResults
    {
        get => _maxSearchResults;
        set => SetValue(ref _maxSearchResults, value);
    }

    public SupportedLanguages GetSupportedLanguage()
    {
        return _pageLanguage switch
        {
            "Japanese" => SupportedLanguages.ja_JP,
            "English" => SupportedLanguages.en_US,
            _ => SupportedLanguages.en_US
        };
    }

    public List<string> GetAvailableSearchCategory()
    {
        return AvailableSearchCategory;
    }

    public static string GetSearchCategoryBaseUrl(string searchCategory)
    {
        return searchCategory switch
        {
            "PC Games" => "https://dlsoft.dmm.co.jp/",
            "Doujin Games" => "https://www.dmm.co.jp/",
            _ => "https://dlsoft.dmm.co.jp/"
        };
    }

    public static string GetSearchPath(string searchCategory)
    {
        return searchCategory switch
        {
            "PC Games" => "search/?service=pcgame&floor=digital_pcgame&searchstr=",
            "Doujin Games" => "dc/doujin/-/search/=/searchstr=",
            _ => "search/?service=pcgame&floor=digital_pcgame&searchstr="
        };
    }

    private static readonly Dictionary<string, string> DefaultParameters = new()
    {
        { "PC Games", "" },
        { "Doujin Games", "/n1=AgReSwMKX1VZCFQCloTHi8SF/" }
    };

    public string GetCurrentBaseUrl()
    {
        return GetSearchCategoryBaseUrl(SearchCategory);
    }

    public static string DetermineCategoryFromUrl(string url)
    {
        if (url.Contains("dlsoft.dmm.co.jp"))
        {
            return "PC Games";
        }

        if (url.Contains("www.dmm.co.jp"))
        {
            return "Doujin Games";
        }

        return "Unknown";
    }

    private Dictionary<string, string> _customParameters = new();

    public static string GetDefaultParameters(string searchCategory)
    {
        return DefaultParameters.ContainsKey(searchCategory) ? DefaultParameters[searchCategory] : "";
    }

    public string GetSearchParameters(string searchCategory)
    {
        return _customParameters.ContainsKey(searchCategory)
            ? _customParameters[searchCategory]
            : GetDefaultParameters(searchCategory);
    }

    public void SetCustomParameter(string searchCategory, string parameter)
    {
        _customParameters[searchCategory] = parameter;
    }
}

public class FanzaMetadataSettingsViewModel : ObservableObject, ISettings
{
    private readonly FanzaMetadataPlugin _plugin;

    private FanzaMetadataSettings _settings;

    public FanzaMetadataSettingsViewModel(FanzaMetadataPlugin plugin)
    {
        // Injecting your plugin instance is required for Save/Load method because Playnite saves data to a location based on what plugin requested the operation.
        _plugin = plugin;

        // Load saved settings.
        var savedSettings = plugin.LoadPluginSettings<FanzaMetadataSettings>();

        // LoadPluginSettings returns null if no saved data is available.
        Settings = savedSettings ?? new FanzaMetadataSettings();
    }

    private FanzaMetadataSettings editingClone { get; set; }

    public FanzaMetadataSettings Settings
    {
        get => _settings;
        set
        {
            _settings = value;
            OnPropertyChanged();
        }
    }

    public void BeginEdit()
    {
        // Code executed when settings view is opened and user starts editing values.
        editingClone = Serialization.GetClone(Settings);
    }

    public void CancelEdit()
    {
        // Code executed when user decides to cancel any changes made since BeginEdit was called.
        // This method should revert any changes made to Option1 and Option2.
        Settings = editingClone;
    }

    public void EndEdit()
    {
        // Code executed when user decides to confirm changes made since BeginEdit was called.
        // This method should save settings made to Option1 and Option2.
        _plugin.SavePluginSettings(Settings);
    }

    public bool VerifySettings(out List<string> errors)
    {
        // Code execute when user decides to confirm changes made since BeginEdit was called.
        // Executed before EndEdit is called and EndEdit is not called if false is returned.
        // List of errors is presented to user if verification fails.
        errors = new List<string>();

        if (!Settings.AvailableSearchCategory.Contains(Settings.SearchCategory))
            errors.Add("Selected category is not supported.");

        if (!Settings.AvailableLanguages.Contains(Settings.PageLanguage))
            errors.Add("Selected language is not supported.");

        if (!Settings.MaxSearchResultsSteps.Contains(Settings.MaxSearchResults))
            errors.Add("Selected search results is not in the list of steps.");

        return true;
    }
}