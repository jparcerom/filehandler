namespace FileHandler.Models;

public sealed record MediaDuplicateScanResult(
    string SourcePath,
    int ImageCount,
    int VideoCount,
    int ScanErrors,
    int VerificationErrors,
    IReadOnlyList<MediaDuplicateGroup> DuplicateGroups,
    IReadOnlyList<MediaNameMatchGroup> NameMatches);
