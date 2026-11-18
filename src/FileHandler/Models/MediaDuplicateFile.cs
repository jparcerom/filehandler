namespace FileHandler.Models;

public sealed record MediaDuplicateFile(
    string Path,
    long? Size,
    bool IsImage,
    string? Sha256);
