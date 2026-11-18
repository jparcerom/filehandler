using System.Globalization;
using System.IO;

namespace FileHandler.Services;

internal static class DateOrganizationPath
{
    private static readonly string[] MonthNames =
    [
        "01-Enero", "02-Febrero", "03-Marzo", "04-Abril",
        "05-Mayo", "06-Junio", "07-Julio", "08-Agosto",
        "09-Septiembre", "10-Octubre", "11-Noviembre", "12-Diciembre"
    ];

    public static string GetMonthDirectory(string destination, DateTime date) =>
        Path.Combine(
            destination,
            date.Year.ToString(CultureInfo.InvariantCulture),
            MonthNames[date.Month - 1]);
}
