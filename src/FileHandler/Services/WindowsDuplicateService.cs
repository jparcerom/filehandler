using System.Globalization;
using System.IO;
using System.Security;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FileHandler.Models;

namespace FileHandler.Services;

public sealed class WindowsDuplicateService
{
    private static readonly Regex NumberedNamePattern = new(
        @"^(?<stem>.+) \((?<number>\d+)\)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly FileTreeScanner _scanner = new();
    private readonly string _logPath;

    public string LogPath => _logPath;

    public WindowsDuplicateService(string? logPath = null)
    {
        _logPath = logPath ?? ActivityLog.DefaultPath;
    }

    public WindowsDuplicateScanResult Scan(string source, IProgress<OperationProgress> progress)
    {
        var sourcePath = Path.GetFullPath(source);
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"No existe la carpeta de origen: {sourcePath}");
        }

        using var log = new ActivityLog(LogPath);
        log.Write($"Inicio de búsqueda de duplicados de Windows. Origen: {sourcePath}");
        var scan = _scanner.Scan(sourcePath, excludedPath: null, progress, log.Write);
        var candidates = scan.Files
            .Select(TryParseNumberedName)
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .ToArray();
        var groups = candidates.GroupBy(
            candidate => Path.Combine(candidate.Directory, candidate.Stem + candidate.Extension),
            StringComparer.OrdinalIgnoreCase);
        var verified = new List<VerifiedWindowsDuplicate>();
        var unverified = 0;
        var errors = 0;
        var fallbackOriginals = 0;
        var processed = 0;

        progress.Report(new OperationProgress(
            0,
            candidates.Length,
            $"Verificando {candidates.Length} archivos con sufijo numérico."));

        foreach (var group in groups)
        {
            var numberedFiles = group
                .OrderBy(candidate => candidate.Number)
                .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var first = numberedFiles[0];
            var originalPath = Path.Combine(first.Directory, first.Stem + first.Extension);
            var hasOriginal = TryFileExists(originalPath, out var originalError);
            if (originalError is not null)
            {
                errors++;
                unverified += numberedFiles.Length;
                log.Write($"[ERROR] No se pudo comprobar el original {originalPath}: {originalError}");
                foreach (var candidate in numberedFiles)
                {
                    progress.Report(new OperationProgress(
                        ++processed,
                        candidates.Length,
                        $"No se pudo verificar {candidate.Path}."));
                }
                continue;
            }

            var canonicalPath = hasOriginal ? originalPath : first.Path;
            var filesToCompare = hasOriginal
                ? numberedFiles
                : numberedFiles.Skip(1).ToArray();
            if (!hasOriginal)
            {
                fallbackOriginals++;
                processed++;
                log.Write($"[ORIGINAL AUSENTE] Se conserva como referencia el sufijo más bajo: {canonicalPath}");
                progress.Report(new OperationProgress(
                    processed,
                    candidates.Length,
                    $"Se conserva como referencia {canonicalPath}."));
            }

            if (filesToCompare.Length == 0)
            {
                continue;
            }

            if (!TryComputeSha256(canonicalPath, out var canonicalHash, out var canonicalError))
            {
                errors++;
                unverified += filesToCompare.Length;
                log.Write($"[ERROR] No se pudo verificar el archivo de referencia {canonicalPath}: {canonicalError}");
                foreach (var candidate in filesToCompare)
                {
                    progress.Report(new OperationProgress(
                        ++processed,
                        candidates.Length,
                        $"No se pudo verificar {candidate.Path}."));
                }
                continue;
            }

            foreach (var candidate in filesToCompare)
            {
                if (!TryComputeSha256(candidate.Path, out var candidateHash, out var candidateError))
                {
                    errors++;
                    unverified++;
                    log.Write($"[ERROR] No se pudo verificar {candidate.Path}: {candidateError}");
                    progress.Report(new OperationProgress(
                        ++processed,
                        candidates.Length,
                        $"No se pudo verificar {candidate.Path}."));
                    continue;
                }

                if (string.Equals(canonicalHash, candidateHash, StringComparison.Ordinal))
                {
                    verified.Add(new VerifiedWindowsDuplicate(candidate.Path, canonicalPath, candidate.Number));
                    log.Write($"[VERIFICADO] {candidate.Path} coincide con {canonicalPath}");
                    progress.Report(new OperationProgress(
                        ++processed,
                        candidates.Length,
                        $"Duplicado verificado: {candidate.Path}"));
                }
                else
                {
                    unverified++;
                    log.Write($"[NO COINCIDE] Se conserva en origen: {candidate.Path}");
                    progress.Report(new OperationProgress(
                        ++processed,
                        candidates.Length,
                        $"Se conserva porque el contenido difiere: {candidate.Path}"));
                }
            }
        }

        if (candidates.Length == 0)
        {
            progress.Report(new OperationProgress(0, 0, "No se encontraron archivos con sufijo numérico."));
        }

        log.Write(
            $"Búsqueda finalizada. Candidatos: {candidates.Length} | Verificados: {verified.Count} | " +
            $"No verificados: {unverified} | Referencias alternativas: {fallbackOriginals} | " +
            $"Errores de escaneo: {scan.Errors} | Errores de verificación: {errors}");
        progress.Report(new OperationProgress(
            candidates.Length,
            candidates.Length,
            $"Búsqueda finalizada: {verified.Count} duplicados verificados de {candidates.Length} candidatos."));

        return new WindowsDuplicateScanResult(
            sourcePath,
            verified,
            candidates.Length,
            unverified,
            fallbackOriginals,
            scan.Errors,
            errors);
    }

    public WindowsDuplicateSummary Move(
        WindowsDuplicateScanResult scan,
        string destination,
        IProgress<OperationProgress> progress)
    {
        if (scan.VerifiedDuplicates.Count == 0)
        {
            throw new InvalidOperationException("No hay duplicados verificados para mover.");
        }

        var destinationPath = Path.GetFullPath(destination);
        if (FileTreeScanner.IsSameOrDescendant(destinationPath, scan.SourcePath))
        {
            throw new ArgumentException("El destino no puede ser el origen ni estar dentro de él.");
        }

        using var log = new ActivityLog(LogPath);
        log.Write(
            $"Inicio del movimiento de duplicados verificados. Archivos: {scan.VerifiedDuplicates.Count} | " +
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
        var changedSinceScan = 0;
        var errors = 0;
        var processed = 0;
        var total = scan.VerifiedDuplicates.Count;

        foreach (var duplicate in scan.VerifiedDuplicates)
        {
            var sourceHash = string.Empty;
            var canonicalHash = string.Empty;
            string? sourceError = null;
            string? canonicalError = null;
            var sourceHasHash = TryComputeSha256(duplicate.SourcePath, out sourceHash, out sourceError);
            var canonicalHasHash = sourceHasHash &&
                                   TryComputeSha256(duplicate.CanonicalPath, out canonicalHash, out canonicalError);
            if (!sourceHasHash || !canonicalHasHash)
            {
                errors++;
                var errorMessage =
                    $"[ERROR] No se pudo volver a verificar {duplicate.SourcePath}: " +
                    $"{sourceError ?? canonicalError}";
                log.Write(errorMessage);
                progress.Report(new OperationProgress(++processed, total, errorMessage));
                continue;
            }

            if (!string.Equals(sourceHash, canonicalHash, StringComparison.Ordinal))
            {
                changedSinceScan++;
                var changedMessage = $"[CONSERVADO] El contenido cambió desde el escaneo: {duplicate.SourcePath}";
                log.Write(changedMessage);
                progress.Report(new OperationProgress(++processed, total, changedMessage));
                continue;
            }

            try
            {
                var fileName = Path.GetFileName(duplicate.SourcePath);
                var desiredPath = Path.Combine(destinationPath, fileName);
                var hasCollision = File.Exists(desiredPath) || Directory.Exists(desiredPath);
                var target = UniqueDestinationPath.GetAvailablePath(destinationPath, fileName);
                File.Move(duplicate.SourcePath, target);
                moved++;
                if (hasCollision)
                {
                    collisionRenames++;
                    log.Write($"[COLISIÓN] Se conservó el destino existente y se renombró el entrante: {target}");
                }

                var movedMessage = $"[MOVIDO] {duplicate.SourcePath} -> {target}";
                log.Write(movedMessage);
                progress.Report(new OperationProgress(++processed, total, movedMessage));
            }
            catch (IOException exception)
            {
                errors++;
                var errorMessage = $"[ERROR] No se pudo mover {duplicate.SourcePath}: {exception.Message}";
                log.Write(errorMessage);
                progress.Report(new OperationProgress(++processed, total, errorMessage));
            }
            catch (UnauthorizedAccessException exception)
            {
                errors++;
                var errorMessage = $"[ERROR] Sin permiso para mover {duplicate.SourcePath}: {exception.Message}";
                log.Write(errorMessage);
                progress.Report(new OperationProgress(++processed, total, errorMessage));
            }
            catch (SecurityException exception)
            {
                errors++;
                var errorMessage = $"[ERROR] Acceso denegado al mover {duplicate.SourcePath}: {exception.Message}";
                log.Write(errorMessage);
                progress.Report(new OperationProgress(++processed, total, errorMessage));
            }
        }

        var summary = new WindowsDuplicateSummary(
            scan.CandidateCount,
            moved,
            collisionRenames,
            scan.UnverifiedCount,
            changedSinceScan,
            errors,
            scan.ScanErrors,
            scan.VerificationErrors,
            scan.FallbackOriginalCount);
        log.Write(
            $"Fin del movimiento. Movidos: {summary.Moved} | Renombrados por colisión: {summary.CollisionRenames} | " +
            $"No verificados: {summary.UnverifiedCount} | Cambiados desde el escaneo: {summary.ChangedSinceScan} | " +
            $"Errores de movimiento: {summary.Errors} | Errores de escaneo: {summary.ScanErrors} | " +
            $"Errores de verificación: {summary.VerificationErrors}");
        progress.Report(new OperationProgress(
            total,
            total,
            $"Movimiento finalizado: {summary.Moved} archivos; revisa manualmente el destino."));
        return summary;
    }

    private static NumberedFile? TryParseNumberedName(string path)
    {
        var extension = Path.GetExtension(path);
        var name = Path.GetFileNameWithoutExtension(path);
        var match = NumberedNamePattern.Match(name);
        if (!match.Success ||
            !long.TryParse(
                match.Groups["number"].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var number))
        {
            return null;
        }

        return new NumberedFile(
            path,
            Path.GetDirectoryName(path)!,
            match.Groups["stem"].Value,
            extension,
            number);
    }

    private static bool TryFileExists(string path, out string? error)
    {
        error = null;
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.Directory) != 0)
            {
                error = "La ruta del original corresponde a una carpeta.";
                return false;
            }

            return true;
        }
        catch (FileNotFoundException)
        {
            error = null;
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            error = null;
            return false;
        }
        catch (IOException exception)
        {
            error = exception.Message;
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            error = exception.Message;
            return false;
        }
        catch (SecurityException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool TryComputeSha256(string path, out string hash, out string? error)
    {
        try
        {
            using var stream = File.OpenRead(path);
            hash = Convert.ToHexString(SHA256.HashData(stream));
            error = null;
            return true;
        }
        catch (IOException exception)
        {
            hash = string.Empty;
            error = exception.Message;
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            hash = string.Empty;
            error = exception.Message;
            return false;
        }
        catch (SecurityException exception)
        {
            hash = string.Empty;
            error = exception.Message;
            return false;
        }
    }

    private sealed record NumberedFile(
        string Path,
        string Directory,
        string Stem,
        string Extension,
        long Number);
}
