using System.IO;
using System.Security;
using System.Security.Cryptography;
using FileHandler.Models;
using Microsoft.VisualBasic.FileIO;

namespace FileHandler.Services;

public sealed class MediaDuplicateService
{
    private readonly MediaCopyService _mediaScanner;
    private readonly string _logPath;
    private readonly Action<string> _sendToRecycleBin;

    public string LogPath => _logPath;

    public MediaDuplicateService(
        string? logPath = null,
        Action<string>? sendToRecycleBin = null)
    {
        _logPath = logPath ?? ActivityLog.DefaultPath;
        _mediaScanner = new MediaCopyService(_logPath);
        _sendToRecycleBin = sendToRecycleBin ?? SendToRecycleBin;
    }

    public MediaDuplicateScanResult Scan(
        string source,
        IProgress<OperationProgress> progress)
    {
        var mediaScan = _mediaScanner.Scan(source, progress);
        using var log = new ActivityLog(LogPath);

        var files = new List<MediaDuplicateFile>(mediaScan.ImageFiles.Count + mediaScan.VideoFiles.Count);
        files.AddRange(mediaScan.ImageFiles.Select(path => new MediaDuplicateFile(path, null, true, null)));
        files.AddRange(mediaScan.VideoFiles.Select(path => new MediaDuplicateFile(path, null, false, null)));

        var verificationErrors = 0;
        for (var index = 0; index < files.Count; index++)
        {
            if (!TryGetFileSize(files[index].Path, out var size, out var error))
            {
                verificationErrors++;
                log.Write($"[ERROR] No se pudo leer el tamaño de {files[index].Path}: {error}");
                continue;
            }

            files[index] = files[index] with { Size = size };
        }

        var sameSizeGroups = files
            .Select((file, index) => (File: file, Index: index))
            .Where(entry => entry.File.Size.HasValue)
            .GroupBy(entry => entry.File.Size!.Value)
            .Where(group => group.Count() > 1)
            .ToArray();
        var hashCount = sameSizeGroups.Sum(group => group.Count());
        var processed = 0;

        progress.Report(new OperationProgress(
            0,
            hashCount,
            $"Comparando el contenido de {hashCount} archivos del mismo tamaño."));

        foreach (var sameSizeGroup in sameSizeGroups)
        {
            foreach (var entry in sameSizeGroup)
            {
                var file = entry.File;
                if (!TryComputeSha256(file.Path, out var hash, out var currentSize, out var error))
                {
                    verificationErrors++;
                    log.Write($"[ERROR] No se pudo comparar {file.Path}: {error}");
                    progress.Report(new OperationProgress(
                        ++processed,
                        hashCount,
                        $"No se pudo verificar {file.Path}."));
                    continue;
                }

                if (currentSize != file.Size)
                {
                    verificationErrors++;
                    log.Write($"[CAMBIÓ] El tamaño cambió durante el escaneo: {file.Path}");
                    progress.Report(new OperationProgress(
                        ++processed,
                        hashCount,
                        $"El archivo cambió durante el escaneo: {file.Path}"));
                    continue;
                }

                files[entry.Index] = file with { Sha256 = hash };
                log.Write($"[HASH] {file.Path} · {hash}");
                progress.Report(new OperationProgress(
                    ++processed,
                    hashCount,
                    $"Comparando contenido: {file.Path}"));
            }
        }

        var duplicateGroups = files
            .Where(file => file.Size.HasValue && file.Sha256 is not null)
            .GroupBy(
                file => (Size: file.Size!.Value, Sha256: file.Sha256!),
                DuplicateContentComparer.Instance)
            .Where(group => group.Count() > 1)
            .Select(group => new MediaDuplicateGroup(
                group.Key.Sha256,
                group.Key.Size,
                group.OrderBy(file => file.Path, StringComparer.OrdinalIgnoreCase).ToArray()))
            .OrderByDescending(group => (decimal)group.Size * (group.Files.Count - 1L))
            .ThenBy(group => group.Files[0].Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        log.Write(
            $"Búsqueda finalizada. Imágenes: {mediaScan.ImageFiles.Count} | Vídeos: {mediaScan.VideoFiles.Count} | " +
            $"Grupos idénticos: {duplicateGroups.Length} | " +
            $"Errores de escaneo: {mediaScan.Errors} | Errores de verificación: {verificationErrors}");
        progress.Report(new OperationProgress(
            hashCount,
            hashCount,
            $"Búsqueda finalizada: {duplicateGroups.Length} grupos de contenido idéntico."));

        return new MediaDuplicateScanResult(
            mediaScan.SourcePath,
            mediaScan.ImageFiles.Count,
            mediaScan.VideoFiles.Count,
            mediaScan.Errors,
            verificationErrors,
            duplicateGroups);
    }

    public MediaDuplicateRemovalSummary SendSelectedToRecycleBin(
        MediaDuplicateScanResult scan,
        IReadOnlyCollection<string> selectedPaths,
        IProgress<OperationProgress> progress)
    {
        if (selectedPaths.Count == 0)
        {
            throw new InvalidOperationException("Selecciona al menos un archivo duplicado.");
        }

        var selected = selectedPaths
            .Select(System.IO.Path.GetFullPath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var groups = scan.DuplicateGroups;
        var filesByPath = groups
            .SelectMany(group => group.Files)
            .ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
        if (selected.Any(path => !filesByPath.ContainsKey(path)))
        {
            throw new ArgumentException("La selección contiene archivos que no pertenecen a los duplicados verificados.");
        }

        var selectedByGroup = groups
            .Select(group => new
            {
                Group = group,
                Selected = group.Files.Where(file => selected.Contains(file.Path)).ToArray(),
                Retained = group.Files.Where(file => !selected.Contains(file.Path)).ToArray()
            })
            .Where(entry => entry.Selected.Length > 0)
            .ToArray();
        if (selectedByGroup.Any(entry => entry.Retained.Length == 0))
        {
            throw new InvalidOperationException("Debe conservarse al menos un archivo de cada grupo duplicado.");
        }

        using var log = new ActivityLog(LogPath);
        log.Write($"Inicio del envío a la Papelera de reciclaje. Archivos seleccionados: {selected.Count}");

        var sentToRecycleBin = new List<string>();
        var changedSinceScan = 0;
        var errors = 0;
        var processed = 0;

        foreach (var entry in selectedByGroup)
        {
            if (!TryOpenVerifiedFile(
                    entry.Retained[0],
                    out var retainedStream,
                    out var retainedError))
            {
                changedSinceScan += entry.Selected.Length;
                var message =
                    $"[CONSERVADO] No se pudo volver a verificar una copia que debía conservarse: " +
                    $"{entry.Retained[0].Path}. {retainedError}";
                log.Write(message);
                foreach (var file in entry.Selected)
                {
                    progress.Report(new OperationProgress(++processed, selected.Count, message));
                }
                continue;
            }

            using (retainedStream!)
            {
                foreach (var file in entry.Selected)
                {
                    if (!TryOpenVerifiedFile(file, out var duplicateStream, out var duplicateError))
                    {
                        changedSinceScan++;
                        var message = $"[CONSERVADO] El archivo ya no coincide con el escaneo: {file.Path}. {duplicateError}";
                        log.Write(message);
                        progress.Report(new OperationProgress(++processed, selected.Count, message));
                        continue;
                    }

                    duplicateStream!.Dispose();
                    try
                    {
                        _sendToRecycleBin(file.Path);
                        if (File.Exists(file.Path))
                        {
                            throw new IOException("El archivo sigue presente después de enviarlo a la Papelera.");
                        }

                        sentToRecycleBin.Add(file.Path);
                        var message = $"[PAPELERA] {file.Path}";
                        log.Write(message);
                        progress.Report(new OperationProgress(++processed, selected.Count, message));
                    }
                    catch (IOException exception)
                    {
                        errors++;
                        var message = $"[ERROR] No se pudo enviar a la Papelera {file.Path}: {exception.Message}";
                        log.Write(message);
                        progress.Report(new OperationProgress(++processed, selected.Count, message));
                    }
                    catch (UnauthorizedAccessException exception)
                    {
                        errors++;
                        var message = $"[ERROR] Sin permiso para enviar a la Papelera {file.Path}: {exception.Message}";
                        log.Write(message);
                        progress.Report(new OperationProgress(++processed, selected.Count, message));
                    }
                    catch (SecurityException exception)
                    {
                        errors++;
                        var message = $"[ERROR] Acceso denegado al enviar a la Papelera {file.Path}: {exception.Message}";
                        log.Write(message);
                        progress.Report(new OperationProgress(++processed, selected.Count, message));
                    }
                    catch (InvalidOperationException exception)
                    {
                        errors++;
                        var message = $"[ERROR] No se pudo enviar a la Papelera {file.Path}: {exception.Message}";
                        log.Write(message);
                        progress.Report(new OperationProgress(++processed, selected.Count, message));
                    }
                    catch (NotSupportedException exception)
                    {
                        errors++;
                        var message = $"[ERROR] No se pudo enviar a la Papelera {file.Path}: {exception.Message}";
                        log.Write(message);
                        progress.Report(new OperationProgress(++processed, selected.Count, message));
                    }
                }
            }
        }

        log.Write(
            $"Fin del envío a la Papelera. Enviados: {sentToRecycleBin.Count} | " +
            $"Cambiados desde el escaneo: {changedSinceScan} | Errores: {errors}");
        progress.Report(new OperationProgress(
            selected.Count,
            selected.Count,
            $"Proceso finalizado: {sentToRecycleBin.Count} enviados a la Papelera, " +
            $"{changedSinceScan} modificados y {errors} errores."));

        return new MediaDuplicateRemovalSummary(sentToRecycleBin, changedSinceScan, errors);
    }

    private static bool TryOpenVerifiedFile(
        MediaDuplicateFile file,
        out FileStream? stream,
        out string? error)
    {
        try
        {
            stream = new FileStream(file.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = Convert.ToHexString(SHA256.HashData(stream));
            if (stream.Length != file.Size || !string.Equals(hash, file.Sha256, StringComparison.Ordinal))
            {
                stream.Dispose();
                stream = null;
                error = "El contenido cambió desde el escaneo.";
                return false;
            }

            stream.Position = 0;
            error = null;
            return true;
        }
        catch (IOException exception)
        {
            stream = null;
            error = exception.Message;
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            stream = null;
            error = exception.Message;
            return false;
        }
        catch (SecurityException exception)
        {
            stream = null;
            error = exception.Message;
            return false;
        }
    }

    private static bool TryGetFileSize(string path, out long size, out string? error)
    {
        try
        {
            size = new FileInfo(path).Length;
            error = null;
            return true;
        }
        catch (IOException exception)
        {
            size = 0;
            error = exception.Message;
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            size = 0;
            error = exception.Message;
            return false;
        }
        catch (SecurityException exception)
        {
            size = 0;
            error = exception.Message;
            return false;
        }
    }

    private static bool TryComputeSha256(
        string path,
        out string hash,
        out long size,
        out string? error)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            size = stream.Length;
            hash = Convert.ToHexString(SHA256.HashData(stream));
            error = null;
            return true;
        }
        catch (IOException exception)
        {
            hash = string.Empty;
            size = 0;
            error = exception.Message;
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            hash = string.Empty;
            size = 0;
            error = exception.Message;
            return false;
        }
        catch (SecurityException exception)
        {
            hash = string.Empty;
            size = 0;
            error = exception.Message;
            return false;
        }
    }

    private static void SendToRecycleBin(string path) =>
        FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);

    private sealed class DuplicateContentComparer : IEqualityComparer<(long Size, string Sha256)>
    {
        public static DuplicateContentComparer Instance { get; } = new();

        public bool Equals((long Size, string Sha256) x, (long Size, string Sha256) y) =>
            x.Size == y.Size && string.Equals(x.Sha256, y.Sha256, StringComparison.Ordinal);

        public int GetHashCode((long Size, string Sha256) value) =>
            HashCode.Combine(value.Size, StringComparer.Ordinal.GetHashCode(value.Sha256));
    }
}
