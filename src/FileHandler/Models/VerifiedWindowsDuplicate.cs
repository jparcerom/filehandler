namespace FileHandler.Models;

public sealed record VerifiedWindowsDuplicate(
    string SourcePath,
    string CanonicalPath,
    long Number);
