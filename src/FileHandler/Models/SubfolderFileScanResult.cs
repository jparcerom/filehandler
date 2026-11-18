namespace FileHandler.Models;

public sealed record SubfolderFileScanResult(
    string SourcePath,
    IReadOnlyList<string> Files,
    int ScanErrors);
