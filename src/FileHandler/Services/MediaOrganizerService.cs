using System.IO;
using System.Security;
using System.Security.Cryptography;
using FileHandler.Models;

namespace FileHandler.Services;

public sealed class MediaOrganizerService
{
    private readonly MediaCopyService _mediaScanner;
    private readonly string _logPath;

    public string LogPath => _logPath;

    public MediaOrganizerService(string? logPath = null)
    {
        _logPath = logPath ?? ActivityLog.DefaultPath;
        _mediaScanner = new MediaCopyService(_logPath);
    }

    public MediaScanResult Scan(string source, IProgress<OperationProgress> progress) =>
        _mediaScanner.Scan(source, progress);

    public MediaOrganizationSummary Organize(
        MediaScanResult scan,
        bool organizeImages,
        string? imageDestination,
        bool organizeVideos,
        string? videoDestination,
        IProgress<OperationProgress> progress)
    {
        IReadOnlyList<string> images = organizeImages ? scan.ImageFiles : Array.Empty<string>();
        IReadOnlyList<string> videos = organizeVideos ? scan.VideoFiles : Array.Empty<string>();
        var total = images.Count + videos.Count;
        if (total == 0)
        {
            throw new InvalidOperationException("No hay archivos multimedia seleccionados para organizar.");
        }

        var normalizedImageDestination = images.Count > 0
            ? ValidateDestination(scan.SourcePath, imageDestination, "imágenes")
            : null;
        var normalizedVideoDestination = videos.Count > 0
            ? ValidateDestination(scan.SourcePath, videoDestination, "vídeos")
            : null;

        using var log = new ActivityLog(LogPath);
        log.Write(
            $"Inicio de organización multimedia. Imágenes: {images.Count} | Vídeos: {videos.Count} | " +
            $"Destino de imágenes: {normalizedImageDestination ?? "(no seleccionado)"} | " +
            $"Destino de vídeos: {normalizedVideoDestination ?? "(no seleccionado)"}");

        try
        {
            if (normalizedImageDestination is not null)
            {
                Directory.CreateDirectory(normalizedImageDestination);
            }
            if (normalizedVideoDestination is not null &&
                !PathsEqual(normalizedVideoDestination, normalizedImageDestination))
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

        var files = new List<OrganizationFile>(total);
        files.AddRange(images.Select(file => new OrganizationFile(file, true, normalizedImageDestination!)));
        files.AddRange(videos.Select(file => new OrganizationFile(file, false, normalizedVideoDestination!)));

        var imagesMoved = 0;
        var videosMoved = 0;
        var collisionRenames = 0;
        var alreadyOrganized = 0;
        var errors = 0;

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            try
            {
                if (!File.Exists(file.SourcePath))
                {
                    throw new FileNotFoundException("El archivo de origen ya no existe.", file.SourcePath);
                }

                var creationDate = File.GetCreationTime(file.SourcePath);
                var modifiedDate = File.GetLastWriteTime(file.SourcePath);
                var date = creationDate <= modifiedDate ? creationDate : modifiedDate;
                var targetDirectory = DateOrganizationPath.GetMonthDirectory(file.Destination, date);
                Directory.CreateDirectory(targetDirectory);

                var fileName = Path.GetFileName(file.SourcePath);
                var desiredPath = Path.Combine(targetDirectory, fileName);
                if (PathsEqual(file.SourcePath, desiredPath))
                {
                    alreadyOrganized++;
                    var existingMessage = $"[YA ORGANIZADO] {file.SourcePath}";
                    log.Write(existingMessage);
                    progress.Report(new OperationProgress(index + 1, total, existingMessage));
                    continue;
                }

                var targetPath = UniqueDestinationPath.GetAvailablePath(targetDirectory, fileName);
                var finalPath = MoveFileSafely(file.SourcePath, targetPath, targetDirectory, fileName, log.Write);
                if (!PathsEqual(finalPath, desiredPath))
                {
                    collisionRenames++;
                    log.Write($"[COLISIÓN] Se conservó el elemento existente y se renombró el entrante: {finalPath}");
                }

                if (file.IsImage)
                {
                    imagesMoved++;
                }
                else
                {
                    videosMoved++;
                }

                var movedMessage = $"[MOVIDO] {file.SourcePath} -> {finalPath}";
                log.Write(movedMessage);
                progress.Report(new OperationProgress(index + 1, total, movedMessage));
            }
            catch (IOException exception)
            {
                errors++;
                var errorMessage = $"[ERROR] No se pudo organizar {file.SourcePath}: {exception.Message}";
                log.Write(errorMessage);
                progress.Report(new OperationProgress(index + 1, total, errorMessage));
            }
            catch (UnauthorizedAccessException exception)
            {
                errors++;
                var errorMessage = $"[ERROR] Sin permiso para organizar {file.SourcePath}: {exception.Message}";
                log.Write(errorMessage);
                progress.Report(new OperationProgress(index + 1, total, errorMessage));
            }
            catch (SecurityException exception)
            {
                errors++;
                var errorMessage = $"[ERROR] Acceso denegado al organizar {file.SourcePath}: {exception.Message}";
                log.Write(errorMessage);
                progress.Report(new OperationProgress(index + 1, total, errorMessage));
            }
        }

        var summary = new MediaOrganizationSummary(
            images.Count,
            videos.Count,
            imagesMoved,
            videosMoved,
            collisionRenames,
            alreadyOrganized,
            errors,
            scan.Errors);
        log.Write(
            $"Fin de organización multimedia. Imágenes movidas: {summary.ImagesMoved}/{summary.ImagesToOrganize} | " +
            $"Vídeos movidos: {summary.VideosMoved}/{summary.VideosToOrganize} | " +
            $"Renombrados por colisión: {summary.CollisionRenames} | Ya organizados: {summary.AlreadyOrganized} | " +
            $"Errores de escaneo: {summary.ScanErrors} | Errores de movimiento: {summary.Errors}");
        progress.Report(new OperationProgress(
            total,
            total,
            $"Organización finalizada: {summary.ImagesMoved} imágenes y {summary.VideosMoved} vídeos movidos; " +
            $"{summary.CollisionRenames} colisiones y {summary.Errors} errores."));
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
            throw new ArgumentException($"El destino de {fileType} no puede ser el origen ni estar dentro de él.");
        }

        return destinationPath;
    }

    private static string MoveFileSafely(
        string source,
        string destination,
        string destinationDirectory,
        string fileName,
        Action<string> log)
    {
        if (string.Equals(
                Path.GetPathRoot(source),
                Path.GetPathRoot(destination),
                StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                File.Move(source, destination);
                return destination;
            }
            catch (IOException exception)
            {
                log($"[REINTENTO SEGURO] No se pudo mover directamente {source}: {exception.Message}");
            }
        }
        else
        {
            log($"[MOVIMIENTO ENTRE VOLÚMENES] Se verificará la copia antes de borrar {source}.");
        }

        var fallbackDestination = UniqueDestinationPath.GetAvailablePath(destinationDirectory, fileName);
        try
        {
            File.Copy(source, fallbackDestination, overwrite: false);
        }
        catch (IOException exception)
        {
            throw new IOException(
                $"No se pudo crear la copia en {fallbackDestination}; se conservó el origen. {exception.Message}",
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new UnauthorizedAccessException(
                $"No se pudo crear la copia en {fallbackDestination}; se conservó el origen. {exception.Message}",
                exception);
        }
        catch (SecurityException exception)
        {
            throw new SecurityException(
                $"No se pudo crear la copia en {fallbackDestination}; se conservó el origen. {exception.Message}",
                exception);
        }

        string sourceHash;
        string destinationHash;
        try
        {
            sourceHash = ComputeSha256(source);
            destinationHash = ComputeSha256(fallbackDestination);
        }
        catch (IOException exception)
        {
            throw new IOException(
                $"No se pudo verificar por SHA-256 la copia en {fallbackDestination}; se conservó el origen. " +
                exception.Message,
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new UnauthorizedAccessException(
                $"No se pudo verificar por SHA-256 la copia en {fallbackDestination}; se conservó el origen. " +
                exception.Message,
                exception);
        }
        catch (SecurityException exception)
        {
            throw new SecurityException(
                $"No se pudo verificar por SHA-256 la copia en {fallbackDestination}; se conservó el origen. " +
                exception.Message,
                exception);
        }

        if (!string.Equals(sourceHash, destinationHash, StringComparison.Ordinal))
        {
            throw new IOException(
                $"La copia de seguridad de {source} no coincide por SHA-256; se conservaron el origen y la copia en " +
                $"{fallbackDestination}.");
        }

        try
        {
            File.Delete(source);
        }
        catch (IOException exception)
        {
            throw new IOException(
                $"La copia verificada se conservó en {fallbackDestination}, pero no se pudo eliminar el origen " +
                $"{source}. {exception.Message}",
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new UnauthorizedAccessException(
                $"La copia verificada se conservó en {fallbackDestination}, pero no se pudo eliminar el origen " +
                $"{source}. {exception.Message}",
                exception);
        }
        catch (SecurityException exception)
        {
            throw new SecurityException(
                $"La copia verificada se conservó en {fallbackDestination}, pero no se pudo eliminar el origen " +
                $"{source}. {exception.Message}",
                exception);
        }

        log($"[COPIA VERIFICADA] Se eliminó el origen solo tras verificar SHA-256: {fallbackDestination}");
        return fallbackDestination;
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool PathsEqual(string first, string? second) =>
        second is not null &&
        string.Equals(
            Path.TrimEndingDirectorySeparator(first),
            Path.TrimEndingDirectorySeparator(second),
            StringComparison.OrdinalIgnoreCase);

    private sealed record OrganizationFile(string SourcePath, bool IsImage, string Destination);
}
