namespace FileHandler.Models;

public sealed record MediaScanResult(
    string SourcePath,
    IReadOnlyList<string> ImageFiles,
    IReadOnlyList<string> VideoFiles,
    int SubdirectoryCount,
    int Errors);
