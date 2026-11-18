namespace FileHandler.Models;

public sealed record MediaCopySummary(
    int ImagesToCopy,
    int VideosToCopy,
    int ImagesCopied,
    int VideosCopied,
    int ScanErrors,
    int CopyErrors,
    IReadOnlyList<MediaCopyEntry> SuccessfulCopies);
