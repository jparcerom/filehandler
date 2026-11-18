using System.Globalization;
using System.IO;

namespace FileHandler.Localization;

internal static class AppLanguage
{
    private const string InstallLanguageFile = "app-language.txt";

    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("es-ES");

    public static string Code { get; private set; } = "es";

    public static void Initialize()
    {
        var languageFile = Path.Combine(AppContext.BaseDirectory, InstallLanguageFile);
        var installedLanguage = File.Exists(languageFile)
            ? File.ReadAllText(languageFile).Trim()
            : null;
        Code = ResolveCode(installedLanguage, CultureInfo.CurrentUICulture.Name);
        Culture = CultureInfo.GetCultureInfo(Code == "en" ? "en-US" : "es-ES");

        CultureInfo.DefaultThreadCurrentCulture = Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Culture;
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = Culture;
    }

    internal static string ResolveCode(string? installedLanguage, string? systemLanguage)
    {
        var selectedLanguage = ResolveSupportedCode(installedLanguage);
        return selectedLanguage
            ?? (systemLanguage?.StartsWith("es", StringComparison.OrdinalIgnoreCase) == true
                ? "es"
                : "en");
    }

    private static string? ResolveSupportedCode(string? language) =>
        language?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true
            ? "en"
            : language?.StartsWith("es", StringComparison.OrdinalIgnoreCase) == true
                ? "es"
                : null;
}
