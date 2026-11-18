using System.IO;
using FileHandler.Models;

namespace FileHandler.Services;

public sealed class DateOrganizer
{
    private readonly FileTreeScanner _scanner = new();
    private readonly string _logPath;

    private static readonly string[] MonthNames =
    [
        "01-Enero", "02-Febrero", "03-Marzo", "04-Abril",
        "05-Mayo", "06-Junio", "07-Julio", "08-Agosto",
        "09-Septiembre", "10-Octubre", "11-Noviembre", "12-Diciembre"
    ];

    public string LogPath => _logPath;

    public DateOrganizer(string? logPath = null)
    {
        _logPath = logPath ?? ActivityLog.DefaultPath;
    }

    public OperationSummary Organize(
        string source,
        string destination,
        IProgress<OperationProgress> progress)
    {
        var sourcePath = Path.GetFullPath(source);
        var destinationPath = Path.GetFullPath(destination);

        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"No existe la carpeta de origen: {sourcePath}");
        }

        if (PathsEqual(sourcePath, destinationPath))
        {
            throw new ArgumentException("El origen y el destino deben ser carpetas distintas.");
        }

        Directory.CreateDirectory(destinationPath);
        using var log = new ActivityLog(LogPath);
        log.Write($"Inicio de organización por fecha. Origen: {sourcePath} | Destino: {destinationPath}");
        var scan = _scanner.Scan(sourcePath, destinationPath, progress, log.Write);
        var files = scan.Files;
        var errors = scan.Errors;

        log.Write($"Archivos encontrados: {files.Count}");
        var moved = 0;
        var skipped = 0;
        progress.Report(new OperationProgress(0, files.Count, $"Se encontraron {files.Count} archivos."));

        for (var index = 0; index < files.Count; index++)
        {
            var sourceFile = files[index];
            var fileName = Path.GetFileName(sourceFile);
            try
            {
                var creationDate = File.GetCreationTime(sourceFile);
                var modifiedDate = File.GetLastWriteTime(sourceFile);
                var date = creationDate <= modifiedDate ? creationDate : modifiedDate;
                var targetDirectory = Path.Combine(
                    destinationPath,
                    date.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    MonthNames[date.Month - 1]);
                var targetFile = Path.Combine(targetDirectory, fileName);

                if (PathsEqual(sourceFile, targetFile) ||
                    File.Exists(targetFile) ||
                    Directory.Exists(targetFile))
                {
                    skipped++;
                    var message = $"[OMITIDO] Ya existe un elemento con ese nombre: {targetFile}";
                    log.Write(message);
                    progress.Report(new OperationProgress(index + 1, files.Count, message));
                    continue;
                }

                Directory.CreateDirectory(targetDirectory);
                File.Move(sourceFile, targetFile);
                moved++;
                var successMessage = $"[MOVIDO] {sourceFile} -> {targetFile}";
                log.Write(successMessage);
                progress.Report(new OperationProgress(index + 1, files.Count, successMessage));
            }
            catch (IOException exception)
            {
                errors++;
                var message = $"[ERROR] {sourceFile}: {exception.Message}";
                log.Write(message);
                progress.Report(new OperationProgress(index + 1, files.Count, message));
            }
            catch (UnauthorizedAccessException exception)
            {
                errors++;
                var message = $"[ERROR] Sin permiso para mover {sourceFile}: {exception.Message}";
                log.Write(message);
                progress.Report(new OperationProgress(index + 1, files.Count, message));
            }
        }

        var summary = new OperationSummary(files.Count, moved, skipped, errors);
        log.Write($"Fin. Encontrados: {summary.Total} | Movidos: {summary.Moved} | Omitidos: {summary.Skipped} | Errores: {summary.Errors}");
        progress.Report(new OperationProgress(
            files.Count,
            files.Count,
            $"Finalizado: {summary.Moved} movidos, {summary.Skipped} omitidos, {summary.Errors} errores."));
        return summary;
    }

    private static bool PathsEqual(string first, string second) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(first),
            Path.TrimEndingDirectorySeparator(second),
            StringComparison.OrdinalIgnoreCase);

}
