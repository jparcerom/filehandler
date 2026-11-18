using System.Globalization;
using System.Resources;

namespace FileHandler.Localization;

internal static class Text
{
    private static readonly ResourceManager Resources =
        new("FileHandler.Localization.Strings", typeof(Text).Assembly);

    public static string Get(string key) => Get(key, AppLanguage.Culture);

    internal static string Get(string key, CultureInfo culture) =>
        Resources.GetString(key, culture)
        ?? throw new MissingManifestResourceException($"Missing localization resource: {key}");

    public static string Format(string key, params object?[] arguments) =>
        Format(key, AppLanguage.Culture, arguments);

    internal static string Format(string key, CultureInfo culture, params object?[] arguments) =>
        string.Format(culture, Get(key, culture), arguments);

    internal static string[] GetMissingKeys(CultureInfo culture)
    {
        var defaultResources = Resources.GetResourceSet(
            CultureInfo.GetCultureInfo("es-ES"),
            createIfNotExists: true,
            tryParents: true)!;
        var localizedResources = Resources.GetResourceSet(
            culture,
            createIfNotExists: true,
            tryParents: false);

        return defaultResources
            .Cast<System.Collections.DictionaryEntry>()
            .Select(entry => (string)entry.Key)
            .Where(key => localizedResources?.GetString(key) is null)
            .ToArray();
    }
}
