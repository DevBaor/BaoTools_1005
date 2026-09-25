using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using BaoToolsGui.Services;

namespace BaoToolsGui.ViewModels;

/// <summary>One installed lua file (a game added to Steam via config\stplug-in\&lt;appid&gt;.lua).</summary>
public partial class LuaTileViewModel : ObservableObject
{
    public long AppId { get; }
    public string FilePath { get; }
    public DateTime AddedAt { get; }
    // Invariant culture so the month is always the 3-letter abbreviation ("Jun", not "June" or a
    // localized long form), 2-digit year ("'26"). Keeps the combined "Added … • Released …" line
    // short enough to fit the card.
    public string AddedLabel
    {
        get
        {
            bool isVi = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
            string dateStr = isVi
                ? AddedAt.ToString("dd/MM/yyyy")
                : AddedAt.ToString(@"MMM d, \'yy", System.Globalization.CultureInfo.CurrentUICulture);
            return string.Format(Resources.Strings.Manage_AddedDate, dateStr);
        }
    }

    /// <summary>Steam release date for the card (e.g. "Released Feb 24, 2022"), or "" until details
    /// are cached. Set by <see cref="UpdateReleaseLabel"/> once the appdetails blob is available.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AddedReleaseLabel))]
    [NotifyPropertyChangedFor(nameof(HasReleaseLabel))]
    private string _releaseLabel = "";

    public bool HasReleaseLabel => !string.IsNullOrWhiteSpace(ReleaseLabel);

    /// <summary>Single-line combined label for the card: "Added … • Released …" (just the Added part
    /// until the release date is known). Keeps both dates on one row so they don't clip the card.</summary>
    public string AddedReleaseLabel =>
        string.IsNullOrEmpty(ReleaseLabel) ? AddedLabel : $"{AddedLabel}  •  {ReleaseLabel}";

    private int _resolving; // 0/1 in-progress guard; retries on later views while Cover is still null
    private bool _nameIsPlaceholder; // cleared once a real name is resolved, so we stop re-fetching

    [ObservableProperty] private string _name;
    [ObservableProperty] private ImageSource? _cover;
    [ObservableProperty] private bool _isSelected;

    /// <summary>True once this app's full details are cached (so filters/sort can use it).</summary>
    [ObservableProperty] private bool _detailsLoaded;

    /// <summary>Which stored lua this game is currently running ("Default", "Build 18234567", …), shown
    /// on the Builds page's game list. Null on the Manage page, which doesn't display it.</summary>
    [ObservableProperty] private string? _variantBadge;

    /// <summary>Flag for "NEW" badge on dashboard recent strip.</summary>
    [ObservableProperty] private bool _isNew;

    /// <summary>Genre or category line for recent card (e.g. "Action • Steam").</summary>
    [ObservableProperty] private string _categoryLabel = "Steam";

    /// <summary>Populate <see cref="ReleaseLabel"/> from cached app-details (no-op until they exist).
    /// Shows Steam's own date string ("Released 24 Feb, 2022"); blank for unreleased/unknown.</summary>
    public void UpdateReleaseLabel(SteamAppInfoCache appInfo)
    {
        var data = appInfo.GetFilterData(AppId);
        if (data is null) { ReleaseLabel = ""; return; }

        bool isVi = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
        if (data.ReleaseDate.HasValue)
        {
            string dateStr = isVi
                ? data.ReleaseDate.Value.ToString("dd/MM/yyyy")
                : data.ReleaseDate.Value.ToString(@"MMM d, \'yy", System.Globalization.CultureInfo.CurrentUICulture);
            ReleaseLabel = string.Format(Resources.Strings.Add_Released, dateStr);
            return;
        }

        string? text = data.ReleaseDateText;
        if (string.IsNullOrWhiteSpace(text)) { ReleaseLabel = ""; return; }

        // Shorten a 4-digit year to 2 digits ("May 26, 2025" → "May 26, '25") to keep the combined
        // line on one row. Leaves non-date wording ("Coming soon") untouched.
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\b(19|20)(\d{2})\b", "'$2");
        ReleaseLabel = string.Format(Resources.Strings.Add_Released, text);
    }

    /// <summary>Populate <see cref="CategoryLabel"/> from cached genres (e.g. "RPG • Steam").</summary>
    public void UpdateCategory(SteamAppInfoCache appInfo)
    {
        var data = appInfo.GetFilterData(AppId);
        string primaryGenre = SteamAppInfoCache.ResolvePrimaryGenre(data?.Genres);
        CategoryLabel = primaryGenre != "Steam" ? $"{primaryGenre} • Steam" : "Steam";
        OnPropertyChanged(nameof(GenreText));
        OnPropertyChanged(nameof(LocalizedGenreText));
    }

    [ObservableProperty] private bool _isInstalled = true;
    [ObservableProperty] private bool _isMissingFiles;

    /// <summary>Clean single metadata row for card: "App ID: 123456 • Added Sep 18, '26"</summary>
    public string MetadataLine => $"App ID: {AppId}  •  {AddedLabel}";
    public string AppIdText => $"App ID: {AppId}";
    public string AddedDateText
    {
        get
        {
            bool isVi = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase);
            string dateStr = isVi
                ? AddedAt.ToString("dd/MM/yyyy")
                : AddedAt.ToString("MMM d, yyyy", System.Globalization.CultureInfo.CurrentUICulture);
            return string.Format(Resources.Strings.Manage_AddedDate, dateStr);
        }
    }
    public string GenreText
    {
        get
        {
            if (CategoryLabel.Contains(" • ")) return CategoryLabel.Split(" • ")[0];
            if (CategoryLabel != "Steam" && !string.IsNullOrWhiteSpace(CategoryLabel)) return CategoryLabel;
            return "Steam";
        }
    }
    public string LocalizedGenreText => GenreLocalizationHelper.GetLocalized(GenreText);

    public void RefreshLabels()
    {
        OnPropertyChanged(nameof(AddedLabel));
        OnPropertyChanged(nameof(AddedReleaseLabel));
        OnPropertyChanged(nameof(AddedDateText));
        OnPropertyChanged(nameof(MetadataLine));
        OnPropertyChanged(nameof(LocalizedGenreText));
        OnPropertyChanged(nameof(ReleaseLabel));
        OnPropertyChanged(nameof(HasReleaseLabel));
    }

    /// <summary>Raised when IsSelected changes so the page can update its selection count/bar.</summary>
    public Action? SelectionChanged { get; set; }
    partial void OnIsSelectedChanged(bool value) => SelectionChanged?.Invoke();

    public LuaTileViewModel(long appId, string filePath, DateTime addedAt, string name, bool nameIsPlaceholder)
    {
        AppId = appId;
        FilePath = filePath;
        AddedAt = addedAt;
        _name = name;
        _nameIsPlaceholder = nameIsPlaceholder;
        _isNew = (DateTime.Now - addedAt).TotalDays <= 7;
        // Cover stays blank until resolved: avoids flashing Steam's "Header Capsule" placeholder.
    }

    /// <summary>
    /// For a VISIBLE tile: ensure the cover file (warming if needed), decode off-UI, show it.
    /// Retries on later views while Cover is still null (so transient failures self-heal).
    /// </summary>
    public async Task EnsureResolvedAsync(SteamAppInfoCache appInfo, CoverCache covers)
    {
        // Refresh the release label whenever a card is (re)shown. Its details may have backfilled
        // since the last ApplyFilter pass. Cheap (reads the memoized filter cache).
        if (string.IsNullOrEmpty(ReleaseLabel)) OnUi(() => UpdateReleaseLabel(appInfo));
        if (CategoryLabel == "Steam") OnUi(() => UpdateCategory(appInfo));

        if (Cover is not null) return;
        if (Interlocked.Exchange(ref _resolving, 1) == 1) return;
        try
        {
            string? local = await ResolveCoverFileAsync(AppId, appInfo, covers,
                name => { if (_nameIsPlaceholder) SetName(name); });

            // If the cover came from the CDN guess (fast path), the name was never resolved.
            // For a still-placeholder name, fetch appdetails once (throttled, saved to /details).
            if (_nameIsPlaceholder)
            {
                var info = appInfo.GetCached(AppId) ?? await appInfo.ResolveAsync(AppId);
                if (!string.IsNullOrWhiteSpace(info?.Name)) SetName(info!.Name);
            }

            if (local is null) return;
            var image = await Task.Run(() => LoadFrozen(local));
            if (image is not null) OnUi(() => Cover = image);
        }
        finally
        {
            Interlocked.Exchange(ref _resolving, 0);
        }
    }

    /// <summary>
    /// Ensure the cover image FILE exists on disk (no decode, no UI). Used by the background prefetch
    /// so the visible page can decode instantly. Order: disk → CDN guess → appdetails header_image.
    /// Only flags "no cover" when Steam definitively has none, never on a transient rate-limit.
    /// </summary>
    public static async Task<string?> ResolveCoverFileAsync(
        long appId, SteamAppInfoCache appInfo, CoverCache covers, Action<string>? onName = null)
    {
        string? local = covers.GetLocalPath(appId);
        if (local is not null) return local;
        if (covers.IsKnownMissing(appId)) return null;

        // Fast path 1: Try high-resolution capsule (616x353) first for sharpest display.
        local = await covers.EnsureAsync(appId, SteamAppInfoCache.GuessCapsuleImageUrl(appId));
        if (local is not null) return local;

        // Fast path 2: Standard header.jpg (460x215).
        local = await covers.EnsureAsync(appId, SteamAppInfoCache.GuessHeaderImageUrl(appId));
        if (local is not null) return local;

        // Slow path: appdetails header_image (throttled) + optional name backfill.
        var info = appInfo.GetCached(appId) ?? await appInfo.ResolveAsync(appId);
        if (onName is not null && !string.IsNullOrWhiteSpace(info?.Name)) onName(info!.Name);
        if (!string.IsNullOrWhiteSpace(info?.HeaderImage))
            local = await covers.EnsureAsync(appId, info!.HeaderImage!);

        // info != null means appdetails answered (so an empty header = genuinely no cover).
        // info == null means we couldn't reach it (rate-limited/offline). Don't give up, retry later.
        if (local is null && info is not null) covers.MarkMissing(appId);
        return local;
    }

    /// <summary>Decode a local image file into a frozen bitmap at full native resolution (off-UI, lock-free).</summary>
    private static ImageSource? LoadFrozen(string path)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad; // load fully, release the file handle
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null; // unreadable file → leave blank
        }
    }

    private void SetName(string name)
    {
        _nameIsPlaceholder = false; // got a real name. Don't re-resolve
        OnUi(() => Name = name);
    }

    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }

    public bool Matches(string q) =>
        Name.Contains(q, StringComparison.OrdinalIgnoreCase) || AppId.ToString().Contains(q);
}

public class QuickPill(string key, string label)
{
    public string Key { get; } = key;
    public string Label { get; } = label;
}

public partial class ManageViewModel : PagedListViewModel<LuaTileViewModel>
{
    private readonly SteamService _steam;
    private readonly SteamAppListCache _appList;
    private readonly SteamAppInfoCache _appInfo;
    private readonly CoverCache _covers;
    private readonly ToastService _toast;
    private readonly SettingsService _settings;
    private readonly SteamlessService _steamless;
    private readonly SteamTicketService _tickets;
    private readonly SteamLibraryService _library;

    private List<LuaTileViewModel> _all = [];
    private CancellationTokenSource? _prefetchCts;

    /// <summary>Set by App so "Update" can navigate to the Add page pre-seeded with the appid.</summary>
    public Action<long>? NavigateToAdd { get; set; }

    /// <summary>Set by App so "Manage Build" can open this game on the Builds page.</summary>
    public Action<long>? NavigateToBuilds { get; set; }

    /// <summary>Set by App so "+ Add Game" navigates to the Add page.</summary>
    public Action? RequestAddGame { get; set; }
    [RelayCommand] private void AddGame() => RequestAddGame?.Invoke();

    public int TotalGamesCount => _all.Count;
    public string TotalGamesLabel => string.Format(Resources.Strings.Manage_TotalGames, _all.Count);

    [ObservableProperty]
    private string _selectedQuickPill = AnyOption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowGridView))]
    [NotifyPropertyChangedFor(nameof(ShowListView))]
    private bool _isGridView = true;

    public bool ShowGridView => ShowItems && IsGridView;
    public bool ShowListView => ShowItems && !IsGridView;

    [RelayCommand]
    private void SetViewMode(string mode)
    {
        IsGridView = mode == "Grid";
    }

    [ObservableProperty]
    private ObservableCollection<QuickPill> _quickPillItems =
    [
        new(AnyOption, "All"),
        new("Steam", "Steam"),
        new("RPG", "RPG"),
        new("Action", "Action"),
        new("Strategy", "Strategy"),
        new("Survival", "Survival")
    ];

    public void UpdateQuickPills()
    {
        int total = _all.Count;
        int action = _all.Count(t => TileMatchesGenre(t, "Action"));
        int rpg = _all.Count(t => TileMatchesGenre(t, "RPG"));
        int strategy = _all.Count(t => TileMatchesGenre(t, "Strategy"));
        int survival = _all.Count(t => TileMatchesGenre(t, "Survival"));

        QuickPillItems =
        [
            new(AnyOption, $"{Resources.Strings.Manage_PageSize_All} ({total})"),
            new("Steam", $"Steam ({total})"),
            new("RPG", $"RPG ({rpg})"),
            new("Action", $"Action ({action})"),
            new("Strategy", $"Strategy ({strategy})"),
            new("Survival", $"Survival ({survival})")
        ];
    }

    [RelayCommand]
    private void QuickFilterGenre(string genre)
    {
        if (string.IsNullOrEmpty(genre) || genre == AnyOption)
        {
            SelectedQuickPill = AnyOption;
            SelectedGenre = AnyOption;
        }
        else if (SelectedQuickPill == genre)
        {
            // Toggle off
            SelectedQuickPill = AnyOption;
            SelectedGenre = AnyOption;
        }
        else
        {
            SelectedQuickPill = genre;
            SelectedGenre = genre;
        }
        ApplyFilter();
    }

    [RelayCommand]
    private void SetSort(string sort)
    {
        if (!string.IsNullOrEmpty(sort))
        {
            SelectedSort = sort;
        }
    }

    // Paging (Items/PageSize/CurrentPage/…), the filtered slice, refresh cooldown, IsLoading/EmptyMessage
    // and the empty-state gating all live in PagedListViewModel<LuaTileViewModel>.

    [ObservableProperty] private string _searchText = "";

    // ── Filters & sort (Manage page) ─────────────────────────────────
    [ObservableProperty] private bool _isFilterPanelOpen = true;

    // "Any" sentinel = no filter for that category.
    public const string AnyOption = "Any";

    // Dropdown option lists. Seeded with "Any" so the default shows before library details load
    // (PopulateFilterOptions re-adds the real values, keeping "Any" first).
    public ObservableCollection<string> TypeOptions { get; } = [AnyOption];
    public ObservableCollection<string> GenreOptions { get; } = [AnyOption];
    public ObservableCollection<string> QuickGenreOptions { get; } =
        [AnyOption, "Action", "RPG", "Strategy", "Survival", "Adventure", "Simulation"];
    public ObservableCollection<string> YearOptions { get; } = [AnyOption];
    public ObservableCollection<string> PriceOptions { get; } = [AnyOption, "Free", "Paid"];
    // Static like Price: stored VALUES stay English (localized for display via FilterOptionDisplayConverter).
    public ObservableCollection<string> ContentOptions { get; } = [AnyOption, "Hide adult", "Adult only"];
    public ObservableCollection<string> SortOptions { get; } =
        ["Recently added", "Name (A–Z)", "Release date (newest)", "Metacritic", "Most reviewed"];

    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasActiveFilters))] private string _selectedType = AnyOption;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasActiveFilters))] private string _selectedGenre = AnyOption;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasActiveFilters))] private string _selectedYear = AnyOption;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasActiveFilters))] private string _selectedPrice = AnyOption;
    [ObservableProperty][NotifyPropertyChangedFor(nameof(HasActiveFilters))] private string _selectedContent = AnyOption;
    [ObservableProperty] private string _selectedSort = "Recently added";

    // Pagination (PageSizeOptions/SelectedPageSize/CurrentPage/PageNumbers/Prev-Next-GoToPage/…) is
    // inherited from PagedListViewModel<LuaTileViewModel>; page size persists via SavePageSizeSetting below.
    protected override void SavePageSizeSetting(int size) => _settings.ManagePageSize = size;

    // ── Sidebar Checkbox Filters matching mockup ───────────────────
    [ObservableProperty] private bool _filterPlatformSteam = true;
    [ObservableProperty] private bool _filterPlatformEpic;
    [ObservableProperty] private bool _filterPlatformOther;

    [ObservableProperty] private bool _filterStatusInstalled;
    [ObservableProperty] private bool _filterStatusNotInstalled;
    [ObservableProperty] private bool _filterStatusMissingFiles;

    [ObservableProperty] private bool _filterGenreAction;
    [ObservableProperty] private bool _filterGenreRpg;
    [ObservableProperty] private bool _filterGenreStrategy;
    [ObservableProperty] private bool _filterGenreSurvival;
    [ObservableProperty] private bool _filterGenreRacing;
    [ObservableProperty] private bool _filterGenreSimulation;
    [ObservableProperty] private bool _filterGenreSports;
    [ObservableProperty] private bool _filterGenreOther;

    partial void OnFilterPlatformSteamChanged(bool value) => ApplyFilter();
    partial void OnFilterPlatformEpicChanged(bool value) => ApplyFilter();
    partial void OnFilterPlatformOtherChanged(bool value) => ApplyFilter();
    partial void OnFilterStatusInstalledChanged(bool value) => ApplyFilter();
    partial void OnFilterStatusNotInstalledChanged(bool value) => ApplyFilter();
    partial void OnFilterStatusMissingFilesChanged(bool value) => ApplyFilter();
    partial void OnFilterGenreActionChanged(bool value) => ApplyFilter();
    partial void OnFilterGenreRpgChanged(bool value) => ApplyFilter();
    partial void OnFilterGenreStrategyChanged(bool value) => ApplyFilter();
    partial void OnFilterGenreSurvivalChanged(bool value) => ApplyFilter();
    partial void OnFilterGenreRacingChanged(bool value) => ApplyFilter();
    partial void OnFilterGenreSimulationChanged(bool value) => ApplyFilter();
    partial void OnFilterGenreSportsChanged(bool value) => ApplyFilter();
    partial void OnFilterGenreOtherChanged(bool value) => ApplyFilter();

    public bool HasActiveFilters =>
        !FilterPlatformSteam || FilterPlatformEpic || FilterPlatformOther ||
        FilterStatusInstalled || FilterStatusNotInstalled || FilterStatusMissingFiles ||
        FilterGenreAction || FilterGenreRpg || FilterGenreStrategy || FilterGenreSurvival ||
        FilterGenreRacing || FilterGenreSimulation || FilterGenreSports || FilterGenreOther ||
        (SelectedQuickPill != AnyOption) ||
        SelectedType != AnyOption || (SelectedGenre != AnyOption && SelectedGenre != "Steam") ||
        SelectedYear != AnyOption || SelectedPrice != AnyOption || SelectedContent != AnyOption;

    // How many library apps still lack cached details while a filter is active (can't be filtered yet).
    // The text updates as the count ticks down. HasPendingDetails (which drives the spinner's
    // Visibility) is a SEPARATE bool that only flips at the 0/non-0 boundary, so a count change
    // doesn't re-fire the visibility binding and restart the ProgressRing animation.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilterPendingText))]
    private int _pendingDetailsCount;

    [ObservableProperty] private bool _hasPendingDetails;
    [ObservableProperty] private bool _isBackfilling;

    partial void OnPendingDetailsCountChanged(int value) => UpdatePendingState();

    private void UpdatePendingState()
    {
        bool detailFiltersActive = SelectedType != AnyOption || SelectedYear != AnyOption ||
                                   SelectedPrice != AnyOption || SelectedContent != AnyOption;
        HasPendingDetails = IsBackfilling && PendingDetailsCount > 0 && detailFiltersActive;
    }

    public string FilterPendingText =>
        string.Format(Resources.Strings.Manage_FetchingDetails, PendingDetailsCount);

    partial void OnSelectedTypeChanged(string value) => ApplyFilter();
    partial void OnSelectedGenreChanged(string value) => ApplyFilter();
    partial void OnSelectedYearChanged(string value) => ApplyFilter();
    partial void OnSelectedPriceChanged(string value) => ApplyFilter();
    partial void OnSelectedContentChanged(string value) => ApplyFilter();
    partial void OnSelectedSortChanged(string value) => ApplyFilter();

    // IsLoading, EmptyMessage and the HasItems/ShowItems/IsEmpty gating are inherited from the base.

    // ── Detail flyout ───────────────────────────────────────────────
    // The depot/DLC breakdown that used to live here now belongs to the Builds page (BuildsViewModel),
    // where it can also show manifest pins per build. This flyout is cover + title + actions only.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDetailOpen))]
    private LuaTileViewModel? _selectedTile;

    partial void OnSelectedTileChanged(LuaTileViewModel? value)
    {
        if (value is not null)
        {
            AiChatViewModel.SetGlobalInspectedGame((uint)value.AppId, value.Name);
        }
    }

    public bool IsDetailOpen => SelectedTile is not null;

    // ── Multi-select ────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelecting))]
    [NotifyPropertyChangedFor(nameof(SelectionLabel))]
    private int _selectedCount;

    public bool IsSelecting => SelectedCount > 0;
    public string SelectionLabel => string.Format(Resources.Strings.Manage_SelectionLabel, SelectedCount);

    public ManageViewModel(SteamService steam, SteamAppListCache appList, SteamAppInfoCache appInfo,
        CoverCache covers, ToastService toast, SettingsService settings,
        SteamlessService steamless, SteamTicketService tickets, SteamLibraryService library)
    {
        _steam = steam;
        _appList = appList;
        _appInfo = appInfo;
        _covers = covers;
        _toast = toast;
        _settings = settings;
        _steamless = steamless;
        _tickets = tickets;
        _library = library;
        InitPageSize(settings.ManagePageSize);
        SettingsViewModel.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        OnUi(() =>
        {
            foreach (var tile in _all)
            {
                tile.UpdateCategory(_appInfo);
                tile.UpdateReleaseLabel(_appInfo);
                tile.RefreshLabels();
            }

            if (SelectedTile is not null)
            {
                SetupDescription(Overview?.ShortDescription, SelectedTile.AppId);
            }

            OnPropertyChanged(nameof(SelectionLabel));
            ApplyFilter();
        });
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    /// <summary>Called by the view as a tile scrolls into view. Resolves its cover (cached after).</summary>
    public void ResolveTile(LuaTileViewModel tile) => _ = tile.EnsureResolvedAsync(_appInfo, _covers);

    /// <summary>
    /// Open a game's detail flyout by appid (Home "recently added" / Add page "Reveal"). Re-scans if
    /// the tile isn't found. The library may be stale (e.g. the game was removed then re-added, or
    /// this VM hasn't loaded yet). If it's genuinely gone, tell the user instead of failing silently.
    /// </summary>
    public async Task OpenDetailForAppIdAsync(long appId)
    {
        var tile = _all.FirstOrDefault(t => t.AppId == appId);
        if (tile is null)
        {
            await LoadAsync();
            tile = _all.FirstOrDefault(t => t.AppId == appId);
        }

        if (tile is not null) await OpenDetailAsync(tile);
        else _toast.Show(Resources.Strings.Manage_Toast_NotFound_Title, Resources.Strings.Manage_Toast_NotFound_Body, error: true);
    }

    // ── Tile actions ────────────────────────────────────────────────

    /// <summary>Blurb + studio + genres for the open flyout, read from the cached appdetails blob.
    /// Null when this game has no details cached (the section collapses).</summary>
    [ObservableProperty] private AppOverview? _overview;
    [ObservableProperty] private string? _effectiveDescription;
    [ObservableProperty] private bool _isShowingTranslation;
    [ObservableProperty] private bool _canToggleDescriptionLanguage;
    [ObservableProperty] private string _descriptionLanguageToggleText = "";

    private void SetupDescription(string? desc, long appId)
    {
        if (string.IsNullOrWhiteSpace(desc))
        {
            EffectiveDescription = null;
            CanToggleDescriptionLanguage = false;
            DescriptionLanguageToggleText = "";
            return;
        }

        string targetLang = GameDescriptionTranslator.GetTargetLanguageCode();
        if (targetLang == "en")
        {
            EffectiveDescription = desc;
            CanToggleDescriptionLanguage = false;
            DescriptionLanguageToggleText = "";
            return;
        }

        if (GameDescriptionTranslator.TryGetCached(desc, targetLang, out var cached) && !string.IsNullOrWhiteSpace(cached))
        {
            EffectiveDescription = cached;
            IsShowingTranslation = true;
            CanToggleDescriptionLanguage = true;
            DescriptionLanguageToggleText = GameDescriptionTranslator.GetOriginalLabel(targetLang);
        }
        else
        {
            EffectiveDescription = desc;
            IsShowingTranslation = false;
            CanToggleDescriptionLanguage = true;
            DescriptionLanguageToggleText = GameDescriptionTranslator.GetTranslateLabel(targetLang);

            _ = Task.Run(async () =>
            {
                string translated = await GameDescriptionTranslator.TranslateAsync(desc, targetLang);
                if (!string.IsNullOrWhiteSpace(translated) && translated != desc && SelectedTile?.AppId == appId)
                {
                    OnUi(() =>
                    {
                        EffectiveDescription = translated;
                        IsShowingTranslation = true;
                        DescriptionLanguageToggleText = GameDescriptionTranslator.GetOriginalLabel(targetLang);
                    });
                }
            });
        }
    }

    [RelayCommand]
    private async Task ToggleDescriptionLanguageAsync()
    {
        string? original = Overview?.ShortDescription;
        if (string.IsNullOrWhiteSpace(original)) return;

        string targetLang = GameDescriptionTranslator.GetTargetLanguageCode();
        if (IsShowingTranslation)
        {
            EffectiveDescription = original;
            IsShowingTranslation = false;
            DescriptionLanguageToggleText = GameDescriptionTranslator.GetTranslateLabel(targetLang);
        }
        else
        {
            if (GameDescriptionTranslator.TryGetCached(original, targetLang, out var cached) && !string.IsNullOrWhiteSpace(cached))
            {
                EffectiveDescription = cached;
                IsShowingTranslation = true;
                DescriptionLanguageToggleText = GameDescriptionTranslator.GetOriginalLabel(targetLang);
            }
            else
            {
                DescriptionLanguageToggleText = GameDescriptionTranslator.GetTranslatingLabel(targetLang);
                string translated = await GameDescriptionTranslator.TranslateAsync(original, targetLang);
                if (!string.IsNullOrWhiteSpace(translated))
                {
                    EffectiveDescription = translated;
                    IsShowingTranslation = true;
                    DescriptionLanguageToggleText = GameDescriptionTranslator.GetOriginalLabel(targetLang);
                }
                else
                {
                    DescriptionLanguageToggleText = GameDescriptionTranslator.GetTranslateLabel(targetLang);
                }
            }
        }
    }

    /// <summary>Open the detail flyout for a tile (cover, title, game info, actions).</summary>
    [RelayCommand]
    private async Task OpenDetailAsync(LuaTileViewModel tile)
    {
        SelectedTile = tile;
        Overview = _appInfo.GetOverview(tile.AppId); // instant when the blob is already on disk
        SetupDescription(Overview?.ShortDescription, tile.AppId);

        // The flyout binds its cover to SelectedTile.Cover. When opened from outside Manage
        // (Home → game detail) the tile was never rendered/scrolled into view, so its cover
        // was never resolved and the pullout image is blank. Ensure it here (idempotent).
        await tile.EnsureResolvedAsync(_appInfo, _covers);

        // No cached blob yet (the background backfill hasn't reached this game). Fetch it at
        // interactive priority, then fill the section in. Re-checked afterwards because the user can
        // close the flyout or switch games while that request is in flight.
        if (Overview is null && await _appInfo.EnsureFullDetailsAsync(tile.AppId) && SelectedTile == tile)
        {
            Overview = _appInfo.GetOverview(tile.AppId);
            SetupDescription(Overview?.ShortDescription, tile.AppId);
        }
    }

    [RelayCommand]
    private void CloseDetail()
    {
        SelectedTile = null;
        Overview = null;
        EffectiveDescription = null;
        CanToggleDescriptionLanguage = false;
    }

    /// <summary>Open this game on the Builds page (switch build, inspect depots/manifests, edit).</summary>
    [RelayCommand]
    private void ManageBuild(LuaTileViewModel tile) => NavigateToBuilds?.Invoke(tile.AppId);

    /// <summary>Set by App. Opens the launch-option editor for a game (appid, name).</summary>
    public Action<long, string>? OpenLaunchOptions { get; set; }

    /// <summary>Edit this game's Steam launch options (the entries behind the Play button).</summary>
    [RelayCommand]
    private void EditLaunchOptions(LuaTileViewModel tile) => OpenLaunchOptions?.Invoke(tile.AppId, tile.Name);

    [RelayCommand]
    private static void OpenStorePage(LuaTileViewModel tile) =>
        SteamService.OpenUrl($"steam://store/{tile.AppId}");

    [RelayCommand]
    private static void OpenInSteam(LuaTileViewModel tile) =>
        SteamService.OpenUrl($"steam://nav/games/details/{tile.AppId}");

    [RelayCommand]
    private static void RevealFile(LuaTileViewModel tile) =>
        SteamService.RevealInExplorer(tile.FilePath);

    [RelayCommand]
    private void CopyAppId(LuaTileViewModel tile)
    {
        if (!SteamService.CopyToClipboard(tile.AppId.ToString()))
            _toast.Show(Resources.Strings.Common_CopyAppId, Resources.Strings.Err_ClipboardBusy, error: true);
    }

    [RelayCommand]
    private void Update(LuaTileViewModel tile) => NavigateToAdd?.Invoke(tile.AppId);

    // ── Steamless: remove SteamStub DRM ──────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NotBusy))]
    private bool _isBusy;
    public bool NotBusy => !IsBusy;

    [ObservableProperty] private double _progress;
    [ObservableProperty] private bool _isProgressIndeterminate;

    /// <summary>Extract Denuvo and AppOwnership tickets for this game.</summary>
    [RelayCommand]
    private async Task ExtractTickets(LuaTileViewModel? tile)
    {
        if (tile is null || IsBusy) return;

        if (!_tickets.IsSteamRunning)
        {
            _toast.Show(Resources.Strings.Manage_Action_ExtractTickets, Resources.Strings.Tickets_SteamNotRunning, error: true);
            return;
        }

        IsBusy = true;
        IsProgressIndeterminate = true;

        try
        {
            var result = await _tickets.ExtractTicketsAsync((uint)tile.AppId, updateLua: true, saveFiles: true);
            if (result.Success)
            {
                _toast.Show(
                    Resources.Strings.Manage_Action_ExtractTickets,
                    string.Format(Resources.Strings.Tickets_ExtractSuccess, tile.AppId));
            }
            else
            {
                _toast.Show(
                    Resources.Strings.Manage_Action_ExtractTickets,
                    result.Error ?? Resources.Strings.Tickets_ExtractFailed,
                    error: true);
            }
        }
        catch (Exception ex)
        {
            _toast.Show(
                Resources.Strings.Manage_Action_ExtractTickets,
                ex.Message,
                error: true);
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
        }
    }

    /// <summary>Download Steamless (once) and strip SteamStub DRM from this game's executable(s).</summary>
    [RelayCommand]
    private async Task RemoveDrm(LuaTileViewModel? tile)
    {
        if (tile is null || IsBusy) return;

        var confirm = ModernMessageBox.Show(
            Resources.Strings.Manage_Steamless_Confirm_Body,
            Resources.Strings.Manage_Steamless_Confirm_Title,
            MessageBoxButton.OKCancel, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.OK) return;

        IsBusy = true;
        IsProgressIndeterminate = true;
        Progress = 0;
        var prog = new Progress<double?>(p =>
        {
            IsProgressIndeterminate = p is null;
            if (p is not null) Progress = p.Value * 100;
        });

        try
        {
            var result = await _steamless.PatchGameAsync(tile.AppId, prog);
            if (result.Failed)
            {
                string msg = result.Error switch
                {
                    "no-install" => Resources.Strings.Manage_Steamless_NoInstall,
                    "no-exe" => Resources.Strings.Manage_Steamless_NoInstall,
                    _ => string.Format(Resources.Strings.Manage_Steamless_Failed, ""),
                };
                _toast.Show(Resources.Strings.Manage_Action_RemoveDrm, msg, error: true);
            }
            else
            {
                _toast.Show(Resources.Strings.Manage_Action_RemoveDrm,
                    string.Format(Resources.Strings.Manage_Toast_Steamless_Done, result.Patched, result.Unchanged));
            }
        }
        catch (Exception ex)
        {
            _toast.Show(Resources.Strings.Manage_Action_RemoveDrm,
                string.Format(Resources.Strings.Manage_Steamless_Failed, ex.Message), error: true);
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
        }
    }

    /// <summary>Confirm, then delete the &lt;appid&gt;.lua file and remove the tile from the grid.</summary>
    [RelayCommand]
    private void Delete(LuaTileViewModel tile)
    {
        var result = ModernMessageBox.Show(
            string.Format(Resources.Strings.Manage_Delete_Body, tile.Name, tile.AppId),
            Resources.Strings.Manage_Delete_Title,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.OK) return;

        try
        {
            if (File.Exists(tile.FilePath)) File.Delete(tile.FilePath);
        }
        catch (Exception ex)
        {
            ModernMessageBox.Show(string.Format(Resources.Strings.Manage_RemoveFailed_File, ex.Message), Resources.Strings.Manage_RemoveFailed_Title,
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (!TryDeleteFile(tile.FilePath, tile.Name)) return;
        RemoveTile(tile);
    }

    private void RecountSelection() => SelectedCount = _all.Count(t => t.IsSelected);

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var t in _all.Where(t => t.IsSelected)) t.IsSelected = false;
        SelectedCount = 0;
    }

    /// <summary>Delete all selected lua files after one confirm; offer a Steam restart afterwards.</summary>
    [RelayCommand]
    private void DeleteSelected()
    {
        var targets = _all.Where(t => t.IsSelected).ToList();
        if (targets.Count == 0) return;

        var result = ModernMessageBox.Show(
            string.Format(Resources.Strings.Manage_DeleteMany_Body, targets.Count),
            Resources.Strings.Manage_DeleteMany_Title,
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.OK) return;

        int failed = 0;
        foreach (var t in targets)
        {
            if (TryDeleteFile(t.FilePath, t.Name, silent: true))
            {
                t.SelectionChanged = null;
                _all.Remove(t);
                if (SelectedTile == t) CloseDetail();
            }
            else failed++;
        }
        // Rebuild the (filtered) grid in one pass: reliable refresh vs. per-item Tiles.Remove on the
        // virtualized panel, which didn't always visually update when filters were active.
        ApplyFilter();
        SelectedCount = _all.Count(t => t.IsSelected);

        if (failed > 0)
            ModernMessageBox.Show(string.Format(Resources.Strings.Manage_RemoveFailed_Count, failed),
                Resources.Strings.Manage_RemoveFailed_Title, MessageBoxButton.OK, MessageBoxImage.Warning);

        // No restart prompt: OST/BST watch config/stplug-in, so deleting a lua un-applies it live.
    }

    /// <summary>Delete one lua file; returns false (and warns, unless silent) on failure.</summary>
    private static bool TryDeleteFile(string path, string name, bool silent = false)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            if (!silent)
                ModernMessageBox.Show(string.Format(Resources.Strings.Manage_RemoveFailed_Named, name, ex.Message), Resources.Strings.Manage_RemoveFailed_Title,
                    MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    /// <summary>Remove a tile from the lists after its file is gone; update empty/detail state.</summary>
    private void RemoveTile(LuaTileViewModel tile)
    {
        tile.SelectionChanged = null;
        _all.Remove(tile);
        if (SelectedTile == tile) CloseDetail();
        ApplyFilter(); // rebuild the (filtered) grid in one pass. Reliable virtualized refresh
    }

    /// <summary>Scan config\stplug-in for &lt;appid&gt;.lua files. Called when the page is shown.</summary>
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            string? steamPath = _steam.EffectivePath;
            if (steamPath is null)
            {
                _all = [];
                ApplyFilter();
                SetEmpty(Resources.Strings.Manage_Empty_NoSteam);
                return;
            }

            string dir = Path.Combine(steamPath, "config", "stplug-in");
            if (!Directory.Exists(dir))
            {
                _all = [];
                ApplyFilter();
                SetEmpty(Resources.Strings.Manage_Empty_NoLuas);
                return;
            }

            // Bulk game-name list (downloaded once, cached). Gives every game a name with no rate limit.
            await _appList.EnsureLoadedAsync();

            HashSet<long> installedAppIds = [];
            HashSet<long> missingAppIds = [];
            try
            {
                installedAppIds = new HashSet<long>(_library.EnumerateInstalled().Select(g => g.AppId));
                var allManifests = _library.GetAllInstalledApps().ToList();
                foreach (var m in allManifests)
                {
                    if (!installedAppIds.Contains(m.AppId))
                        missingAppIds.Add(m.AppId);
                }
            }
            catch { /* best-effort */ }

            var tiles = await Task.Run(() =>
                LuaInstaller.EnumerateInstalled(dir) // shared scan rule (skips Steamtools.lua etc.)
                    .Select(f =>
                    {
                        var info = new FileInfo(f.Path);
                        string? name = _appList.GetName(f.AppId) ?? ParseLuaName(f.Path) ?? _appInfo.GetCached(f.AppId)?.Name;
                        bool placeholder = name is null;
                        // Base = when added to the folder; if edited since (LastWrite later), use that. Newer is more relevant.
                        var added = info.LastWriteTime > info.CreationTime ? info.LastWriteTime : info.CreationTime;
                        var t = new LuaTileViewModel(f.AppId, f.Path, added, name ?? string.Format(Resources.Strings.Common_AppFallback, f.AppId), placeholder);
                        t.IsInstalled = installedAppIds.Contains(f.AppId);
                        t.IsMissingFiles = missingAppIds.Contains(f.AppId);
                        return t;
                    })
                    .OrderByDescending(t => t.AddedAt)
                    .ToList());

            foreach (var t in tiles) t.SelectionChanged = RecountSelection;
            _all = tiles;
            SelectedCount = 0; // fresh scan clears any prior selection
            PopulateFilterOptions();
            UpdateQuickPills();
            ApplyFilter();
            if (_all.Count == 0) SetEmpty(Resources.Strings.Manage_Empty_NoLuas);

            StartCoverPrefetch(_all);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Warm cover FILES for the whole library in the background (download only, no decode, no UI),
    /// so when you jump to any page it decodes instantly from disk.
    /// </summary>
    private void StartCoverPrefetch(IReadOnlyList<LuaTileViewModel> tiles)
    {
        _prefetchCts?.Cancel();
        var cts = _prefetchCts = new CancellationTokenSource();
        var appids = tiles.Select(t => t.AppId).ToList();
        _ = Task.Run(async () =>
        {
            try
            {
                OnUi(() =>
                {
                    IsBackfilling = true;
                    UpdatePendingState();
                });

                // 1. Priority: warm cover images + names for the whole library (CDN-first, fast).
                await Parallel.ForEachAsync(
                    appids,
                    new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = cts.Token },
                    async (appid, _) =>
                    {
                        try { await LuaTileViewModel.ResolveCoverFileAsync(appid, _appInfo, _covers); }
                        catch { /* one cover failing shouldn't stop the rest */ }
                    });

                // 2. Then: gently backfill full app-details (for filters) for any not yet cached.
                //    Trickles under the rate cap and yields to interactive lookups. As details arrive,
                //    refresh the dropdowns + pending count live (throttled, so we don't re-render per app).
                var lastUi = DateTime.MinValue;
                await _appInfo.BackfillFullDetailsAsync(appids, onProgress: () =>
                {
                    if (cts.Token.IsCancellationRequested) return;
                    if (DateTime.UtcNow - lastUi < TimeSpan.FromSeconds(2)) return;
                    lastUi = DateTime.UtcNow;
                    OnUi(() => { PopulateFilterOptions(); RefreshPendingCount(); UpdateQuickPills(); });
                }, cts.Token);

                // Final refresh once backfill completes (dropdowns + counts + re-apply current filters).
                // resetPage:false so a user who's paged away isn't yanked back to page 1.
                if (!cts.Token.IsCancellationRequested)
                    OnUi(() => { PopulateFilterOptions(); UpdateQuickPills(); ApplyFilter(resetPage: false); });
            }
            catch (OperationCanceledException) { /* superseded by a newer load */ }
            finally
            {
                OnUi(() =>
                {
                    IsBackfilling = false;
                    HasPendingDetails = false;
                });
            }
        });
    }

    private static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }

    [RelayCommand]
    private Task Refresh() => RefreshWithCooldownAsync(async () =>
    {
        if (!string.IsNullOrEmpty(SearchText)) SearchText = ""; // reset filter → full library visible
        await LoadAsync();
        _toast.Show(Resources.Strings.Manage_Toast_Refreshed_Title, string.Format(Resources.Strings.Manage_Toast_Refreshed_Body, _all.Count));
    });

    // IsEmpty is computed (in the base) from Items + IsLoading; this just sets the message for that state.
    private void SetEmpty(string message) => EmptyMessage = message;

    /// <summary>Recount library apps still missing details (drives the "fetching details" notice).</summary>
    private void RefreshPendingCount()
    {
        PendingDetailsCount = _all.Count(t => _appInfo.GetFilterData(t.AppId) is null);
        UpdatePendingState();
    }

    /// <param name="resetPage">True (default) for a user-initiated filter/search/sort/page-size change.
    /// Jump back to page 1. False for passive re-renders (backfill completing) so the user stays on
    /// their current page; the page is still clamped into range below.</param>
    private void ApplyFilter(bool resetPage = true)
    {
        string q = SearchText.Trim();

        IEnumerable<LuaTileViewModel> result = _all;

        // 1. Text search (name / appid).
        if (!string.IsNullOrEmpty(q)) result = result.Where(t => t.Matches(q));

        // 2. Platform filter (Steam, Epic, Other)
        // All installed .lua files in stplug-in belong to Steam.
        if (!FilterPlatformSteam)
        {
            result = Enumerable.Empty<LuaTileViewModel>();
        }

        // 3. Status filter (Installed, Not Installed, Missing Files)
        bool hasStatus = FilterStatusInstalled || FilterStatusNotInstalled || FilterStatusMissingFiles;
        if (hasStatus)
        {
            result = result.Where(t =>
                (FilterStatusInstalled && t.IsInstalled && !t.IsMissingFiles) ||
                (FilterStatusNotInstalled && !t.IsInstalled && !t.IsMissingFiles) ||
                (FilterStatusMissingFiles && t.IsMissingFiles));
        }

        // 4. Quick Pills filter (All, Steam, RPG, Action, Strategy, Survival)
        if (!string.IsNullOrEmpty(SelectedQuickPill) && SelectedQuickPill != AnyOption)
        {
            if (SelectedQuickPill != "Steam")
            {
                result = result.Where(t => TileMatchesGenre(t, SelectedQuickPill));
            }
        }
        else if (SelectedGenre != AnyOption && SelectedGenre != "Steam")
        {
            result = result.Where(t => TileMatchesGenre(t, SelectedGenre));
        }

        // 5. Sidebar Genre checkboxes filter
        List<string> checkedGenres = [];
        if (FilterGenreAction) checkedGenres.Add("Action");
        if (FilterGenreRpg) checkedGenres.Add("RPG");
        if (FilterGenreStrategy) checkedGenres.Add("Strategy");
        if (FilterGenreSurvival) checkedGenres.Add("Survival");
        if (FilterGenreRacing) checkedGenres.Add("Racing");
        if (FilterGenreSimulation) checkedGenres.Add("Simulation");
        if (FilterGenreSports) checkedGenres.Add("Sports");
        if (FilterGenreOther) checkedGenres.Add("Other");

        if (checkedGenres.Count > 0)
        {
            result = result.Where(t => checkedGenres.Any(cg => TileMatchesGenre(t, cg)));
        }

        // 6. Secondary detail filters (Type, Year, Price, Content) - ONLY when changed from Any
        bool detailFiltersActive = SelectedType != AnyOption || SelectedYear != AnyOption ||
                                   SelectedPrice != AnyOption || SelectedContent != AnyOption;
        if (detailFiltersActive)
        {
            result = result.Where(t =>
            {
                var data = _appInfo.GetFilterData(t.AppId);
                if (data is null) return false;
                return MatchesDetailFilters(data);
            });
        }

        // Update tile metadata
        foreach (var t in _all)
        {
            var data = _appInfo.GetFilterData(t.AppId);
            t.DetailsLoaded = data is not null;
            if (data is not null)
            {
                t.UpdateReleaseLabel(_appInfo);
                t.UpdateCategory(_appInfo);
            }
        }

        var list = result.ToList();
        list = SortTiles(list);

        PendingDetailsCount = _all.Count(t => _appInfo.GetFilterData(t.AppId) is null);
        UpdatePendingState();

        EmptyMessage = _all.Count > 0
            ? Resources.Strings.Manage_Empty_NoMatch
            : Resources.Strings.Manage_Empty_NoLuas;

        SetFiltered(list, resetPage);
        OnPropertyChanged(nameof(TotalGamesCount));
        OnPropertyChanged(nameof(TotalGamesLabel));
        OnPropertyChanged(nameof(HasActiveFilters));
        OnPropertyChanged(nameof(ShowGridView));
        OnPropertyChanged(nameof(ShowListView));
    }

    private bool TileMatchesGenre(LuaTileViewModel t, string genre)
    {
        var data = _appInfo.GetFilterData(t.AppId);
        if (data?.Genres is { } genres && genres.Count > 0)
        {
            if (genre.Equals("RPG", StringComparison.OrdinalIgnoreCase))
                return genres.Any(g => g.Contains("RPG", StringComparison.OrdinalIgnoreCase) || g.Contains("Role-Playing", StringComparison.OrdinalIgnoreCase));
            if (genre.Equals("Action", StringComparison.OrdinalIgnoreCase))
                return genres.Any(g => g.Contains("Action", StringComparison.OrdinalIgnoreCase));
            if (genre.Equals("Strategy", StringComparison.OrdinalIgnoreCase))
                return genres.Any(g => g.Contains("Strategy", StringComparison.OrdinalIgnoreCase));
            if (genre.Equals("Survival", StringComparison.OrdinalIgnoreCase))
                return genres.Any(g => g.Contains("Survival", StringComparison.OrdinalIgnoreCase));
            if (genre.Equals("Racing", StringComparison.OrdinalIgnoreCase))
                return genres.Any(g => g.Contains("Racing", StringComparison.OrdinalIgnoreCase));
            if (genre.Equals("Simulation", StringComparison.OrdinalIgnoreCase))
                return genres.Any(g => g.Contains("Simulation", StringComparison.OrdinalIgnoreCase));
            if (genre.Equals("Sports", StringComparison.OrdinalIgnoreCase))
                return genres.Any(g => g.Contains("Sports", StringComparison.OrdinalIgnoreCase));
            if (genre.Equals("Other", StringComparison.OrdinalIgnoreCase))
            {
                string[] known = ["Action", "RPG", "Role-Playing", "Strategy", "Survival", "Racing", "Simulation", "Sports"];
                return genres.Any(g => !known.Any(k => g.Contains(k, StringComparison.OrdinalIgnoreCase)));
            }
            return genres.Any(g => g.Contains(genre, StringComparison.OrdinalIgnoreCase));
        }

        // Fallback before full details are loaded: check CategoryLabel
        return t.CategoryLabel.Contains(genre, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesDetailFilters(AppFilterData d)
    {
        if (SelectedType != AnyOption &&
            !string.Equals(d.Type, SelectedType, StringComparison.OrdinalIgnoreCase)) return false;

        if (SelectedYear != AnyOption &&
            (d.ReleaseYear is null || d.ReleaseYear.Value.ToString() != SelectedYear)) return false;

        if (SelectedPrice != AnyOption)
        {
            if (SelectedPrice == "Free" && !d.IsFree) return false;
            if (SelectedPrice == "Paid" && d.IsFree) return false;
        }

        if (SelectedContent == "Hide adult" && d.IsAdult) return false;
        if (SelectedContent == "Adult only" && !d.IsAdult) return false;

        return true;
    }

    private List<LuaTileViewModel> SortTiles(List<LuaTileViewModel> tiles) => SelectedSort switch
    {
        "Name (A–Z)" => tiles.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList(),
        "Release date (newest)" => tiles
            .OrderByDescending(t => _appInfo.GetFilterData(t.AppId)?.ReleaseDate ?? DateTime.MinValue).ToList(),
        "Metacritic" => tiles
            .OrderByDescending(t => _appInfo.GetFilterData(t.AppId)?.Metacritic ?? int.MinValue).ToList(),
        "Most reviewed" => tiles
            .OrderByDescending(t => _appInfo.GetFilterData(t.AppId)?.Reviews ?? long.MinValue).ToList(),
        _ => tiles.OrderByDescending(t => t.AddedAt).ToList(), // "Recently added" (default)
    };

    /// <summary>Reset every filter (keeps the search text and sort).</summary>
    [RelayCommand]
    private void ClearFilters()
    {
        FilterPlatformSteam = true;
        FilterPlatformEpic = false;
        FilterPlatformOther = false;
        FilterStatusInstalled = false;
        FilterStatusNotInstalled = false;
        FilterStatusMissingFiles = false;
        FilterGenreAction = false;
        FilterGenreRpg = false;
        FilterGenreStrategy = false;
        FilterGenreSurvival = false;
        FilterGenreRacing = false;
        FilterGenreSimulation = false;
        FilterGenreSports = false;
        FilterGenreOther = false;

        SelectedQuickPill = AnyOption;
        SelectedType = AnyOption;
        SelectedGenre = AnyOption;
        SelectedYear = AnyOption;
        SelectedPrice = AnyOption;
        SelectedContent = AnyOption;
        OnPropertyChanged(nameof(HasActiveFilters));
        ApplyFilter();
    }

    [RelayCommand]
    private void ToggleFilterPanel() => IsFilterPanelOpen = !IsFilterPanelOpen;

    /// <summary>Build the Type/Genre/Year dropdown options from whatever library details are cached.</summary>
    private void PopulateFilterOptions()
    {
        var types = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var genres = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var years = new SortedSet<int>();

        foreach (var t in _all)
        {
            var d = _appInfo.GetFilterData(t.AppId);
            if (d is null) continue;
            if (!string.IsNullOrWhiteSpace(d.Type)) types.Add(d.Type!);
            foreach (var g in d.Genres) genres.Add(g);
            if (d.ReleaseYear is { } y) years.Add(y);
        }

        RebuildOptionList(TypeOptions, types);
        RebuildOptionList(GenreOptions, genres);
        RebuildOptionList(YearOptions, years.OrderByDescending(y => y).Select(y => y.ToString()));
    }

    /// <summary>Sync a dropdown's options to <paramref name="values"/> WITHOUT a full Clear(). Clearing
    /// drops the ComboBox's bound SelectedItem (which made Type/Genre/Year render blank). "Any" stays
    /// at index 0; we add new values and remove stale ones in place.</summary>
    private static void RebuildOptionList(ObservableCollection<string> target, IEnumerable<string> values)
    {
        var wanted = values.ToList();
        if (target.Count == 0 || target[0] != AnyOption) target.Insert(0, AnyOption);

        // Remove options no longer present (skip index 0 = "Any").
        for (int i = target.Count - 1; i >= 1; i--)
            if (!wanted.Contains(target[i]))
                target.RemoveAt(i);

        // Add any new ones (append; order is best-effort).
        foreach (var v in wanted)
            if (!target.Contains(v))
                target.Add(v);
    }

    /// <summary>Best-effort name from the lua header comment (Morrenus/Hubcap). Fallback when the app list misses.</summary>
    private static string? ParseLuaName(string path)
    {
        try
        {
            using var reader = new StreamReader(path);
            string? line0 = reader.ReadLine();
            string? line1 = reader.ReadLine();
            if (line0 is not null && line1 is not null &&
                line0.Contains("Created by", StringComparison.OrdinalIgnoreCase) &&
                line1.StartsWith("--"))
            {
                string name = line1.TrimStart('-', ' ').Trim();
                return string.IsNullOrWhiteSpace(name) ? null : name;
            }
        }
        catch { /* unreadable. Fall back to appid */ }
        return null;
    }
}
