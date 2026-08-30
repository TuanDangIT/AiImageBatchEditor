using System;
using System.Collections.Generic;
using System.Text;

namespace AiImageBatchEditor.Core;

public sealed class BatchProcessor : IBatchProcessor
{
    private readonly IImageAiProvider _provider;

    public BatchProcessor(IImageAiProvider provider) => _provider = provider;

    public async Task ProcessAsync(
        string inputFolder,
        string outputFolder,
        string prompt,
        IReadOnlyList<string> references,
        int maxConcurrency,
        IProgress<ImageJob>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputFolder);

        var files = Directory.EnumerateFiles(inputFolder)
            .Where(IsSupportedImage)
            .ToArray();

        using var gate = new SemaphoreSlim(Math.Max(1, maxConcurrency));

        var tasks = files.Select(async inputPath =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var outputPath = Path.Combine(
                    outputFolder,
                    Path.GetFileNameWithoutExtension(inputPath) + ".png");

                var job = new ImageJob
                {
                    InputPath = inputPath,
                    OutputPath = outputPath
                };

                if (File.Exists(outputPath))
                {
                    job.Status = JobStatus.Skipped;
                    progress?.Report(job);
                    return;
                }

                job.Status = JobStatus.Processing;
                progress?.Report(job);

                ImageEditResult result = new(false, null, null);

                for (var attempt = 1; attempt <= 3; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    result = await _provider.EditImageAsync(
                        new ImageEditRequest(
                            inputPath, prompt, references, outputPath),
                        cancellationToken);

                    if (result.Success)
                        break;

                    if (attempt < 3)
                        await Task.Delay(
                            TimeSpan.FromSeconds(attempt * 2),
                            cancellationToken);
                }

                if (result.Success && result.ImageBytes is not null)
                {
                    await File.WriteAllBytesAsync(
                        outputPath, result.ImageBytes, cancellationToken);
                    job.Status = JobStatus.Completed;
                }
                else
                {
                    job.Status = JobStatus.Failed;
                    job.ErrorMessage = result.ErrorMessage;
                }

                progress?.Report(job);
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    private static bool IsSupportedImage(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".webp";
    }
}
