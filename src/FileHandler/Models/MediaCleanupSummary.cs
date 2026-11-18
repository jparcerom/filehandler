namespace FileHandler.Models;

public sealed record MediaCleanupSummary(
    int ImagesDeleted,
    int VideosDeleted,
    int FilesSkipped,
    int Errors,
    int EmptyDirectoriesDeleted,
    int ScanErrors);
