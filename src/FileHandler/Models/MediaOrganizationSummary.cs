namespace FileHandler.Models;

public sealed record MediaOrganizationSummary(
    int ImagesToOrganize,
    int VideosToOrganize,
    int ImagesMoved,
    int VideosMoved,
    int CollisionRenames,
    int AlreadyOrganized,
    int Errors,
    int ScanErrors);
