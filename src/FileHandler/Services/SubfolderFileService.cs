using System.IO;
using System.Security;
using FileHandler.Models;

namespace FileHandler.Services;

public sealed class SubfolderFileService
{
    private readonly FileTreeScanner _scanner = new();
    private readonly string _logPath;

    public string LogPath => _logPath;

    public SubfolderFileService(string? logPath = null)
    {
        _logPath = logPath ?? ActivityLog.DefaultPath;
    }

    public SubfolderFileScanResult Scan(string source, IProgress<OperationProgress> progress)
    {
        var sourcePath = Path.GetFullPath(source);
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"No existe la carpeta de origen: {sourcePath}");
        }

        using var log = new ActivityLog(LogPath);
        log.Write($"Inicio del escaneo de archivos en subcarpetas. Origen: {sourcePath}");
        var scan = _scanner.Scan(sourcePath, excludedPath: null, progress, log.Write);
        var files = scan.Files
            .Where(file => !PathsEqual(Path.GetDirectoryName(file)!, sourcePath))
            .ToArray();

        log.Write(
            $"Escaneo finalizado. Archivos en subcarpetas: {files.Length} | " +
            $"Subcarpetas: {scan.SubdirectoryCount} | Errores: {scan.Errors}");
        progress.Report(new OperationProgress(
            files.Length,
            files.Length,
            $"Escaneo finalizado: {files.Length} archivos en subcarpetas."));

        return new SubfolderFileScanResult(sourcePath, files, scan.Errors);
    }

    public SubfolderFileSummary Move(
        SubfolderFileScanResult scan,
        string destination,
        IProgress<OperationProgress> progress)
    {
        if (scan.Files.Count == 0)
        {
            throw new InvalidOperationException("No hay archivos en subcarpetas para mover.");
        }

        var destinationPath = Path.GetFullPath(destination);
        using var log = new ActivityLog(LogPath);
        log.Write(
            $"Inicio del movimiento a la raíz del destino. Archivos: {scan.Files.Count} | " +
            $"Destino: {destinationPath}");

        try
        {
            Directory.CreateDirectory(destinationPath);
        }
        catch (IOException exception)
        {
            log.Write($"[ERROR] No se pudo preparar el destino: {exception.Message}");
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            log.Write($"[ERROR] Sin permiso para preparar el destino: {exception.Message}");
            throw;
        }
        catch (SecurityException exception)
        {
            log.Write($"[ERROR] Acceso denegado al preparar el destino: {exception.Message}");
            throw;
        }

        var moved = 0;
        var collisionRenames = 0;
        var alreadyAtDestination = 0;
        var errors = 0;
        var total = scan.Files.Count;
        var processed = 0;

        foreach (var sourceFile in scan.Files)
        {
            if (PathsEqual(Path.GetDirectoryName(sourceFile)!, destinationPath))
            {
                alreadyAtDestination++;
                var existingMessage = $"[OMITIDO] El archivo ya está en la raíz del destino: {sourceFile}";
                log.Write(existingMessage);
                progress.Report(new OperationProgress(++processed, total, existingMessage));
                continue;
            }

            try
            {
                var fileName = Path.GetFileName(sourceFile);
                var desiredPath = Path.Combine(destinationPath, fileName);
                var hasCollision = File.Exists(desiredPath) || Directory.Exists(desiredPath);
                var target = UniqueDestinationPath.GetAvailablePath(destinationPath, fileName);
                File.Move(sourceFile, target);
                moved++;
                if (hasCollision)
                {
                    collisionRenames++;
                    log.Write($"[COLISIÓN] Se conservó el elemento existente y se renombró el entrante: {target}");
                }

                var movedMessage = $"[MOVIDO] {sourceFile} -> {target}";
                log.Write(movedMessage);
                progress.Report(new OperationProgress(++processed, total, movedMessage));
            }
            catch (IOException exception)
            {
                errors++;
                var errorMessage = $"[ERROR] No se pudo mover {sourceFile}: {exception.Message}";
                log.Write(errorMessage);
                progress.Report(new OperationProgress(++processed, total, errorMessage));
            }
            catch (UnauthorizedAccessException exception)
            {
                errors++;
                var errorMessage = $"[ERROR] Sin permiso para mover {sourceFile}: {exception.Message}";
                log.Write(errorMessage);
                progress.Report(new OperationProgress(++processed, total, errorMessage));
            }
            catch (SecurityException exception)
            {
                errors++;
                var errorMessage = $"[ERROR] Acceso denegado al mover {sourceFile}: {exception.Message}";
                log.Write(errorMessage);
                progress.Report(new OperationProgress(++processed, total, errorMessage));
            }
        }

        var summary = new SubfolderFileSummary(
            total,
            moved,
            collisionRenames,
            alreadyAtDestination,
            errors,
            scan.ScanErrors);
        log.Write(
            $"Fin del movimiento. Encontrados: {summary.FilesFound} | Movidos: {summary.Moved} | " +
            $"Renombrados por colisión: {summary.CollisionRenames} | Ya en destino: {summary.AlreadyAtDestination} | " +
            $"Errores de escaneo: {summary.ScanErrors} | Errores de movimiento: {summary.Errors}");
        progress.Report(new OperationProgress(
            total,
            total,
            $"Movimiento finalizado: {summary.Moved} movidos, {summary.AlreadyAtDestination} ya en destino, " +
            $"{summary.Errors} errores."));
        return summary;
    }

    private static bool PathsEqual(string first, string second) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(first),
            Path.TrimEndingDirectorySeparator(second),
            StringComparison.OrdinalIgnoreCase);
}
