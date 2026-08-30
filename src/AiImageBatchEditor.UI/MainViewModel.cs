using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using AiImageBatchEditor.Core;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AiImageBatchEditor.UI;

public partial class MainViewModel : ObservableObject
{
    private readonly IImageAiProvider _provider;
    private readonly IBatchProcessor _batchProcessor;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private string inputFolder = "";
    [ObservableProperty] private string outputFolder = "";
    [ObservableProperty] private string instructions =
        "Edit the input image according to the reference images. " +
        "Preserve the identity, shape, proportions, and important details of the main subject. " +
        "Only make the changes explicitly requested.";
    [ObservableProperty] private string? testImage;
    [ObservableProperty] private BitmapImage? previewInput;
    [ObservableProperty] private BitmapImage? previewOutput;
    [ObservableProperty] private bool isProcessing;
    [ObservableProperty] private int progress;
    [ObservableProperty] private string statusText = "Ready.";
    [ObservableProperty] private int maxConcurrency = 2;

    public ObservableCollection<string> References { get; } = [];
    public ObservableCollection<JobRow> Jobs { get; } = [];

    public bool CanTest =>
        !IsProcessing && File.Exists(TestImage) &&
        !string.IsNullOrWhiteSpace(Instructions);

    public bool CanProcess =>
        !IsProcessing &&
        Directory.Exists(InputFolder) &&
        Directory.Exists(OutputFolder) &&
        !string.IsNullOrWhiteSpace(Instructions);

    public MainViewModel(IImageAiProvider provider, IBatchProcessor batchProcessor)
    {
        _provider = provider;
        _batchProcessor = batchProcessor;
    }

    partial void OnInputFolderChanged(string value) => RefreshCanExecute();
    partial void OnOutputFolderChanged(string value) => RefreshCanExecute();
    partial void OnInstructionsChanged(string value) => RefreshCanExecute();

    partial void OnTestImageChanged(string? value)
    {
        PreviewInput = LoadBitmap(value);
        RefreshCanExecute();
    }

    partial void OnIsProcessingChanged(bool value) => RefreshCanExecute();

    public void AddReference(string path)
    {
        if (!References.Contains(path))
            References.Add(path);
    }

    public void RemoveReference(string path) => References.Remove(path);

    public void SetTestImage(string path)
    {
        TestImage = path;
        PreviewInput = LoadBitmap(path);
    }

    public async Task TestEditAsync()
    {
        if (!CanTest || TestImage is null)
            return;

        try
        {
            IsProcessing = true;
            StatusText = "Testing image...";

            var result = await _provider.EditImageAsync(
                new ImageEditRequest(
                    TestImage,
                    BuildPrompt(),
                    References.ToArray(),
                    ""),
                CancellationToken.None);

            if (!result.Success || result.ImageBytes is null)
            {
                StatusText = result.ErrorMessage ?? "Test failed.";
                return;
            }

            PreviewOutput = LoadBitmap(result.ImageBytes);
            StatusText = "Test completed.";
        }
        catch (Exception ex)
        {
            StatusText = $"Test failed: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    public async Task ProcessBatchAsync()
    {
        if (!CanProcess)
            return;

        _cts = new CancellationTokenSource();
        IsProcessing = true;
        Progress = 0;
        Jobs.Clear();

        try
        {
            var files = Directory.EnumerateFiles(InputFolder)
                .Where(IsSupportedImage)
                .ToArray();

            var completed = 0;

            var reporter = new Progress<ImageJob>(job =>
            {
                var existing = Jobs.FirstOrDefault(x =>
                    string.Equals(x.InputPath, job.InputPath,
                        StringComparison.OrdinalIgnoreCase));

                if (existing is null)
                    Jobs.Add(new JobRow(job));
                else
                {
                    existing.Status = job.Status.ToString();
                    existing.ErrorMessage = job.ErrorMessage ?? "";
                }

                if (job.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Skipped)
                {
                    completed++;
                    Progress = files.Length == 0
                        ? 100
                        : (int)Math.Round(completed * 100.0 / files.Length);
                    StatusText = $"{completed}/{files.Length} processed.";
                }
            });

            await _batchProcessor.ProcessAsync(
                InputFolder,
                OutputFolder,
                BuildPrompt(),
                References.ToArray(),
                MaxConcurrency,
                reporter,
                _cts.Token);

            Progress = 100;
            StatusText = "Batch complete.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Batch cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Batch failed: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    public void Cancel() => _cts?.Cancel();

    private string BuildPrompt()
    {
        return
            "You are performing a controlled image edit." + Environment.NewLine +
            Environment.NewLine +
            "Apply these instructions to the input image:" + Environment.NewLine +
            Instructions + Environment.NewLine +
            Environment.NewLine +
            "Reference images are examples of the desired visual result. " +
            "Use them as visual guidance, but do not copy unrelated objects from them." +
            Environment.NewLine +
            "Preserve the main subject's identity, geometry, proportions, text, logos, " +
            "and important details unless the instructions explicitly request changing them." +
            Environment.NewLine +
            "Do not invent changes that were not requested.";
    }

    private static bool IsSupportedImage(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".webp";
    }

    private static BitmapImage? LoadBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        return LoadBitmap(File.ReadAllBytes(path));
    }

    private static BitmapImage LoadBitmap(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void RefreshCanExecute()
    {
        OnPropertyChanged(nameof(CanTest));
        OnPropertyChanged(nameof(CanProcess));
    }
}

public sealed class JobRow
{
    public JobRow(ImageJob job)
    {
        InputPath = job.InputPath;
        OutputPath = job.OutputPath;
        Status = job.Status.ToString();
        ErrorMessage = job.ErrorMessage ?? "";
    }

    public string InputPath { get; }
    public string OutputPath { get; }
    public string FileName => Path.GetFileName(InputPath);
    public string Status { get; set; }
    public string ErrorMessage { get; set; }
}
