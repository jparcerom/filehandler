using System.IO;
using System.Text;
using FileHandler.Models;

namespace FileHandler.Services;

public sealed class DateOrganizer
{
    private static readonly string[] MonthNames =
    [
        "01-Enero", "02-Febrero", "03-Marzo", "04-Abril",
        "05-Mayo", "06-Junio", "07-Julio", "08-Agosto",
        "09-Septiembre", "10-Octubre", "11-Noviembre", "12-Diciembre"
    ];

    public string LogPath { get; }

    public DateOrganizer(string? logPath = null)
    {
        LogPath = logPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FileHandler",
            "logs",
            "filehandler.log");
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
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

        using var log = new StreamWriter(LogPath, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        WriteLog(log, $"Inicio de organización por fecha. Origen: {sourcePath} | Destino: {destinationPath}");

        var files = new List<string>();
        var errors = 0;
        var directories = new Stack<string>();
        directories.Push(sourcePath);

        while (directories.Count > 0)
        {
            var current = directories.Pop();
            progress.Report(new OperationProgress(
                files.Count,
                0,
                $"Escaneando {current} · {files.Count} archivos encontrados.",
                IsScanning: true));
            try
            {
                foreach (var file in Directory.EnumerateFiles(current))
                {
                    files.Add(file);
                }

                foreach (var subdirectory in Directory.EnumerateDirectories(current))
                {
                    if (IsSameOrDescendant(subdirectory, destinationPath))
                    {
                        continue;
                    }

                    try
                    {
                        if ((File.GetAttributes(subdirectory) & FileAttributes.ReparsePoint) == 0)
                        {
                            directories.Push(subdirectory);
                        }
                    }
                    catch (IOException exception)
                    {
                        errors++;
                        WriteLog(log, $"[ERROR] No se pudo leer la carpeta {subdirectory}: {exception.Message}");
                    }
                    catch (UnauthorizedAccessException exception)
                    {
                        errors++;
                        WriteLog(log, $"[ERROR] Sin permiso para leer la carpeta {subdirectory}: {exception.Message}");
                    }
                }
            }
            catch (IOException exception)
            {
                errors++;
                WriteLog(log, $"[ERROR] No se pudo recorrer la carpeta {current}: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                errors++;
                WriteLog(log, $"[ERROR] Sin permiso para recorrer la carpeta {current}: {exception.Message}");
            }
        }

        WriteLog(log, $"Archivos encontrados: {files.Count}");
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
                    WriteLog(log, message);
                    progress.Report(new OperationProgress(index + 1, files.Count, message));
                    continue;
                }

                Directory.CreateDirectory(targetDirectory);
                File.Move(sourceFile, targetFile);
                moved++;
                var successMessage = $"[MOVIDO] {sourceFile} -> {targetFile}";
                WriteLog(log, successMessage);
                progress.Report(new OperationProgress(index + 1, files.Count, successMessage));
            }
            catch (IOException exception)
            {
                errors++;
                var message = $"[ERROR] {sourceFile}: {exception.Message}";
                WriteLog(log, message);
                progress.Report(new OperationProgress(index + 1, files.Count, message));
            }
            catch (UnauthorizedAccessException exception)
            {
                errors++;
                var message = $"[ERROR] Sin permiso para mover {sourceFile}: {exception.Message}";
                WriteLog(log, message);
                progress.Report(new OperationProgress(index + 1, files.Count, message));
            }
        }

        var summary = new OperationSummary(files.Count, moved, skipped, errors);
        WriteLog(log, $"Fin. Encontrados: {summary.Total} | Movidos: {summary.Moved} | Omitidos: {summary.Skipped} | Errores: {summary.Errors}");
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

    private static bool IsSameOrDescendant(string path, string parent)
    {
        var relativePath = Path.GetRelativePath(parent, path);
        return relativePath == "." ||
               (!Path.IsPathRooted(relativePath) &&
                relativePath != ".." &&
                !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }

    private static void WriteLog(StreamWriter log, string message)
    {
        log.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
        log.Flush();
    }
}
