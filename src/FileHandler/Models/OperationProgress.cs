namespace FileHandler.Models;

public sealed record OperationProgress(int Processed, int Total, string Message, bool IsScanning = false);
