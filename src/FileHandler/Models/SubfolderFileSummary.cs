namespace FileHandler.Models;

public sealed record SubfolderFileSummary(
    int FilesFound,
    int Moved,
    int CollisionRenames,
    int AlreadyAtDestination,
    int Errors,
    int ScanErrors);
