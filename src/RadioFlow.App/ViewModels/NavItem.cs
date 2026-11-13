namespace RadioFlow.App.ViewModels;

public sealed record NavItem(string Key, string Title, string Glyph);

public static class NavKeys
{
    public const string Home = "home";
    public const string All = "all";
    public const string Favorites = "favorites";
    public const string Recent = "recent";
    public const string Countries = "countries";
    public const string Genres = "genres";
    public const string Settings = "settings";
}
