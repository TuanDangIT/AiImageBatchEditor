namespace AiImageBatchEditor.Core;

public enum JobStatus
{
    Pending, Processing, Completed, Failed, Skipped
}

public sealed record ImageEditRequest(
    string InputPath,
    string Prompt,
    IReadOnlyList<string> ReferencePaths,
    string OutputPath);

public sealed record ImageEditResult(
    bool Success,
    byte[]? ImageBytes,
    string? ErrorMessage);

public sealed class ImageJob
{
    public required string InputPath { get; init; }
    public required string OutputPath { get; init; }
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public string? ErrorMessage { get; set; }
}

public interface IImageAiProvider
{
    Task<ImageEditResult> EditImageAsync(
        ImageEditRequest request,
        CancellationToken cancellationToken);
}

public interface IBatchProcessor
{
    Task ProcessAsync(
        string inputFolder,
        string outputFolder,
        string prompt,
        IReadOnlyList<string> references,
        int maxConcurrency,
        IProgress<ImageJob>? progress,
        CancellationToken cancellationToken);
}
