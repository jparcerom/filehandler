using System.IO;
using System.Security;
using System.Security.Cryptography;
using FileHandler.Models;

namespace FileHandler.Services;

public sealed class MediaCleanupService
{
    private readonly string _logPath;

    public string LogPath => _logPath;

    public MediaCleanupService(string? logPath = null)
    {
        _logPath = logPath ?? ActivityLog.DefaultPath;
    }

    public MediaCleanupSummary DeleteCopiedFiles(
        MediaScanResult scan,
        IReadOnlyList<MediaCopyEntry> successfulCopies,
        bool deleteImages,
        bool deleteVideos,
        IProgress<OperationProgress> progress)
    {
        var filesToDelete = successfulCopies
            .Where(copy => (copy.IsImage && deleteImages) || (!copy.IsImage && deleteVideos))
            .ToArray();
        if (filesToDelete.Length == 0)
        {
            throw new InvalidOperationException("No hay originales copiados seleccionados para eliminar.");
        }

        var removeEmptyDirectories = ShouldRemoveEmptyDirectories(scan, deleteImages, deleteVideos);
        var total = filesToDelete.Length +
                    (removeEmptyDirectories ? scan.Directories.Count : 0);

        using var log = new ActivityLog(LogPath);
        log.Write(
            $"Inicio de limpieza del origen. Imágenes seleccionadas: {deleteImages} | " +
            $"Vídeos seleccionados: {deleteVideos} | Archivos: {filesToDelete.Length}");

        var imagesDeleted = 0;
        var videosDeleted = 0;
        var filesSkipped = 0;
        var errors = 0;
        var processed = 0;

        foreach (var copy in filesToDelete)
        {
            try
            {
                var sourceHash = ComputeSha256(copy.SourcePath);
                var destinationHash = ComputeSha256(copy.DestinationPath);
                if (!string.Equals(sourceHash, destinationHash, StringComparison.Ordinal))
                {
                    filesSkipped++;
                    var changedMessage = $"[CONSERVADO] El origen o la copia cambió; no se elimina: {copy.SourcePath}";
                    log.Write(changedMessage);
                    progress.Report(new OperationProgress(++processed, total, changedMessage));
                    continue;
                }

                File.Delete(copy.SourcePath);
                if (copy.IsImage)
                {
                    imagesDeleted++;
                }
                else
                {
                    videosDeleted++;
                }

                var deletedMessage = $"[ELIMINADO] {copy.SourcePath}";
                log.Write(deletedMessage);
                progress.Report(new OperationProgress(++processed, total, deletedMessage));
            }
            catch (FileNotFoundException exception)
            {
                filesSkipped++;
                var missingMessage = $"[CONSERVADO] Ya no existe un archivo que se iba a verificar: {exception.FileName}";
                log.Write(missingMessage);
                progress.Report(new OperationProgress(++processed, total, missingMessage));
            }
            catch (DirectoryNotFoundException exception)
            {
                filesSkipped++;
                var missingMessage = $"[CONSERVADO] Ya no existe una carpeta necesaria para verificar: {exception.Message}";
                log.Write(missingMessage);
                progress.Report(new OperationProgress(++processed, total, missingMessage));
            }
            catch (IOException exception)
            {
                errors++;
                var errorMessage = $"[ERROR] No se pudo eliminar {copy.SourcePath}: {exception.Message}";
                log.Write(errorMessage);
                progress.Report(new OperationProgress(++processed, total, errorMessage));
            }
            catch (UnauthorizedAccessException exception)
            {
                errors++;
                var errorMessage = $"[ERROR] Sin permiso para eliminar {copy.SourcePath}: {exception.Message}";
                log.Write(errorMessage);
                progress.Report(new OperationProgress(++processed, total, errorMessage));
            }
            catch (SecurityException exception)
            {
                errors++;
                var errorMessage = $"[ERROR] Acceso denegado al eliminar {copy.SourcePath}: {exception.Message}";
                log.Write(errorMessage);
                progress.Report(new OperationProgress(++processed, total, errorMessage));
            }
        }

        var directoriesDeleted = 0;
        if (removeEmptyDirectories)
        {
            foreach (var directory in scan.Directories.OrderByDescending(GetDepth))
            {
                try
                {
                    if (Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        var notEmptyMessage = $"[CONSERVADA] La carpeta no está vacía: {directory}";
                        log.Write(notEmptyMessage);
                        progress.Report(new OperationProgress(++processed, total, notEmptyMessage));
                        continue;
                    }

                    Directory.Delete(directory, recursive: false);
                    directoriesDeleted++;
                    var deletedMessage = $"[CARPETA ELIMINADA] {directory}";
                    log.Write(deletedMessage);
                    progress.Report(new OperationProgress(++processed, total, deletedMessage));
                }
                catch (DirectoryNotFoundException)
                {
                    progress.Report(new OperationProgress(
                        ++processed,
                        total,
                        $"[OMITIDA] La carpeta ya no existe: {directory}"));
                }
                catch (IOException exception)
                {
                    errors++;
                    var errorMessage = $"[ERROR] No se pudo eliminar la carpeta {directory}: {exception.Message}";
                    log.Write(errorMessage);
                    progress.Report(new OperationProgress(++processed, total, errorMessage));
                }
                catch (UnauthorizedAccessException exception)
                {
                    errors++;
                    var errorMessage = $"[ERROR] Sin permiso para eliminar la carpeta {directory}: {exception.Message}";
                    log.Write(errorMessage);
                    progress.Report(new OperationProgress(++processed, total, errorMessage));
                }
                catch (SecurityException exception)
                {
                    errors++;
                    var errorMessage = $"[ERROR] Acceso denegado al eliminar la carpeta {directory}: {exception.Message}";
                    log.Write(errorMessage);
                    progress.Report(new OperationProgress(++processed, total, errorMessage));
                }
            }
        }

        var summary = new MediaCleanupSummary(
            imagesDeleted,
            videosDeleted,
            filesSkipped,
            errors,
            directoriesDeleted,
            scan.Errors);
        log.Write(
            $"Fin de limpieza. Imágenes eliminadas: {summary.ImagesDeleted} | " +
            $"Vídeos eliminados: {summary.VideosDeleted} | Archivos conservados: {summary.FilesSkipped} | " +
            $"Carpetas vacías eliminadas: {summary.EmptyDirectoriesDeleted} | " +
            $"Errores: {summary.Errors} | Errores de escaneo previos: {summary.ScanErrors}");
        progress.Report(new OperationProgress(
            total,
            total,
            $"Limpieza finalizada: {summary.ImagesDeleted} imágenes, {summary.VideosDeleted} vídeos, " +
            $"{summary.EmptyDirectoriesDeleted} carpetas vacías eliminadas."));
        return summary;
    }

    public static bool ShouldRemoveEmptyDirectories(
        MediaScanResult scan,
        bool deleteImages,
        bool deleteVideos)
    {
        var hasImages = scan.ImageFiles.Count > 0;
        var hasVideos = scan.VideoFiles.Count > 0;
        if (hasImages && hasVideos)
        {
            return deleteImages && deleteVideos;
        }

        return (hasImages && deleteImages) || (hasVideos && deleteVideos);
    }

    private static int GetDepth(string directory) =>
        Path.GetRelativePath(Path.GetPathRoot(directory)!, directory)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries)
            .Length;

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
