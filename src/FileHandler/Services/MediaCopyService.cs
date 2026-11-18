using System.IO;
using System.Security;
using System.Security.Cryptography;
using FileHandler.Localization;
using FileHandler.Models;

namespace FileHandler.Services;

public sealed class MediaCopyService
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif", ".tiff",
        ".webp", ".heic", ".heif", ".raw"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".avi", ".wmv", ".flv", ".webm",
        ".m4v", ".mpg", ".mpeg", ".3gp"
    };

    private readonly FileTreeScanner _scanner = new();
    private readonly string _logPath;

    public string LogPath => _logPath;

    public MediaCopyService(string? logPath = null)
    {
        _logPath = logPath ?? ActivityLog.DefaultPath;
    }

    public MediaScanResult Scan(string source, IProgress<OperationProgress> progress)
    {
        var sourcePath = Path.GetFullPath(source);
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException(Text.Format("SourceFolderNotFound", sourcePath));
        }

        using var log = new ActivityLog(LogPath);
        log.Write(Text.Format("StartingMediaScan", sourcePath));
        var scan = _scanner.Scan(sourcePath, excludedPath: null, progress, log.Write);
        var images = scan.Files
            .Where(file => ImageExtensions.Contains(Path.GetExtension(file)))
            .ToArray();
        var videos = scan.Files
            .Where(file => VideoExtensions.Contains(Path.GetExtension(file)))
            .ToArray();

        log.Write(
            Text.Format(
                "MediaScanCompletedLog",
                images.Length,
                videos.Length,
                scan.SubdirectoryCount,
                scan.Errors));
        progress.Report(new OperationProgress(
            images.Length + videos.Length,
            images.Length + videos.Length,
            Text.Format(
                "MediaScanCompleted",
                images.Length,
                Text.Get("Images"),
                videos.Length,
                Text.Get("Videos"),
                scan.SubdirectoryCount)));

        return new MediaScanResult(sourcePath, images, videos, scan.Directories, scan.Errors);
    }

    public MediaCopySummary Copy(
        MediaScanResult scan,
        bool copyImages,
        string? imageDestination,
        bool copyVideos,
        string? videoDestination,
        IProgress<OperationProgress> progress)
    {
        var images = copyImages ? scan.ImageFiles : Array.Empty<string>();
        var videos = copyVideos ? scan.VideoFiles : Array.Empty<string>();
        var total = images.Count + videos.Count;
        if (total == 0)
        {
            throw new InvalidOperationException("No hay archivos multimedia seleccionados para copiar.");
        }

        var normalizedImageDestination = images.Count > 0
            ? ValidateDestination(scan.SourcePath, imageDestination, "imágenes")
            : null;
        var normalizedVideoDestination = videos.Count > 0
            ? ValidateDestination(scan.SourcePath, videoDestination, "vídeos")
            : null;

        using var log = new ActivityLog(LogPath);
        log.Write(
            $"Inicio de copia multimedia. Imágenes: {images.Count} | Vídeos: {videos.Count}");

        try
        {
            if (normalizedImageDestination is not null)
            {
                Directory.CreateDirectory(normalizedImageDestination);
            }
            if (normalizedVideoDestination is not null)
            {
                Directory.CreateDirectory(normalizedVideoDestination);
            }
        }
        catch (IOException exception)
        {
            log.Write($"[ERROR] No se pudieron preparar los destinos: {exception.Message}");
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            log.Write($"[ERROR] Sin permiso para preparar los destinos: {exception.Message}");
            throw;
        }
        catch (SecurityException exception)
        {
            log.Write($"[ERROR] Acceso denegado al preparar los destinos: {exception.Message}");
            throw;
        }

        var imagesCopied = 0;
        var videosCopied = 0;
        var errors = 0;
        var processed = 0;
        var successfulCopies = new List<MediaCopyEntry>();
        foreach (var file in images)
        {
            if (CopyFile(file, normalizedImageDestination!, log, out var destinationPath, out var message))
            {
                imagesCopied++;
                successfulCopies.Add(new MediaCopyEntry(file, destinationPath!, IsImage: true));
            }
            else
            {
                errors++;
            }
            processed++;
            progress.Report(new OperationProgress(processed, total, message));
        }

        foreach (var file in videos)
        {
            if (CopyFile(file, normalizedVideoDestination!, log, out var destinationPath, out var message))
            {
                videosCopied++;
                successfulCopies.Add(new MediaCopyEntry(file, destinationPath!, IsImage: false));
            }
            else
            {
                errors++;
            }
            processed++;
            progress.Report(new OperationProgress(processed, total, message));
        }

        var summary = new MediaCopySummary(
            images.Count,
            videos.Count,
            imagesCopied,
            videosCopied,
            scan.Errors,
            errors,
            successfulCopies);
        log.Write(
            $"Fin de copia multimedia. Imágenes copiadas: {summary.ImagesCopied}/{summary.ImagesToCopy} | " +
            $"Vídeos copiados: {summary.VideosCopied}/{summary.VideosToCopy} | " +
            $"Errores de escaneo: {summary.ScanErrors} | Errores de copia: {summary.CopyErrors}");
        progress.Report(new OperationProgress(
            total,
            total,
            $"Copia finalizada: {summary.ImagesCopied} imágenes y {summary.VideosCopied} vídeos; " +
            $"{summary.CopyErrors} errores."));
        return summary;
    }

    private static string ValidateDestination(string source, string? destination, string fileType)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            throw new ArgumentException($"Selecciona un destino para los archivos de {fileType}.");
        }

        var destinationPath = Path.GetFullPath(destination);
        if (FileTreeScanner.IsSameOrDescendant(destinationPath, source))
        {
            throw new ArgumentException(
                $"El destino de {fileType} no puede ser el origen ni estar dentro de él.");
        }

        return destinationPath;
    }

    private static bool CopyFile(
        string sourceFile,
        string destination,
        ActivityLog log,
        out string? destinationPath,
        out string message)
    {
        try
        {
            var target = GetAvailableDestination(sourceFile, destination, log);
            File.Copy(sourceFile, target, overwrite: false);
            destinationPath = target;
            message = $"[COPIADO] {sourceFile} -> {target}";
            log.Write(message);
            return true;
        }
        catch (IOException exception)
        {
            destinationPath = null;
            message = $"[ERROR] No se pudo copiar {sourceFile}: {exception.Message}";
            log.Write(message);
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            destinationPath = null;
            message = $"[ERROR] Sin permiso para copiar {sourceFile}: {exception.Message}";
            log.Write(message);
            return false;
        }
        catch (SecurityException exception)
        {
            destinationPath = null;
            message = $"[ERROR] Acceso denegado al copiar {sourceFile}: {exception.Message}";
            log.Write(message);
            return false;
        }
    }

    private static string GetAvailableDestination(string sourceFile, string destination, ActivityLog log)
    {
        var fileName = Path.GetFileName(sourceFile);
        var desiredPath = Path.Combine(destination, fileName);
        if (!File.Exists(desiredPath) && !Directory.Exists(desiredPath))
        {
            return desiredPath;
        }

        if (File.Exists(desiredPath))
        {
            var sourceHash = ComputeSha256(sourceFile);
            var destinationHash = ComputeSha256(desiredPath);
            var match = string.Equals(sourceHash, destinationHash, StringComparison.Ordinal);
            log.Write(match
                ? $"[DUPLICADO IDÉNTICO] Se conservarán ambos archivos: {desiredPath}"
                : $"[COLISIÓN] Contenido distinto; se conservará el existente y se renombrará el entrante: {desiredPath}");
        }
        else
        {
            log.Write($"[COLISIÓN] El destino es una carpeta; se renombrará el entrante: {desiredPath}");
        }

        return UniqueDestinationPath.GetAvailablePath(destination, fileName);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
