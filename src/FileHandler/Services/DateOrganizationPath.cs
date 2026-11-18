using System.Globalization;
using System.IO;
using FileHandler.Localization;

namespace FileHandler.Services;

internal static class DateOrganizationPath
{
    private static readonly string[] MonthNameKeys =
    [
        "JanuaryFolder", "FebruaryFolder", "MarchFolder", "AprilFolder",
        "MayFolder", "JuneFolder", "JulyFolder", "AugustFolder",
        "SeptemberFolder", "OctoberFolder", "NovemberFolder", "DecemberFolder"
    ];

    public static string GetMonthDirectory(
        string destination,
        DateTime date,
        CultureInfo? culture = null) =>
        Path.Combine(
            destination,
            date.Year.ToString(CultureInfo.InvariantCulture),
            Text.Get(MonthNameKeys[date.Month - 1], culture ?? AppLanguage.Culture));
}
