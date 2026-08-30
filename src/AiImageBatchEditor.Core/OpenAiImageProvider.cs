using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AiImageBatchEditor.Core;

public sealed class OpenAiImageProvider : IImageAiProvider
{
    private const long MaxImageBytes = 20 * 1024 * 1024; // 20 MB (gpt-image-1 limit)
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAiImageProvider(HttpClient httpClient, string apiKey, string model = "gpt-image-2")
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
    }

    public async Task<ImageEditResult> EditImageAsync(
        ImageEditRequest request,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(request.InputPath))
            return new(false, null, $"Input file does not exist: {request.InputPath}");

        if (string.IsNullOrWhiteSpace(_apiKey))
            return new(false, null, "OPENAI_API_KEY is not configured.");

        var inputValidation = ValidateImageFile(request.InputPath);
        if (inputValidation is not null)
            return new(false, null, inputValidation);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(_model), "model");
        form.Add(new StringContent(request.Prompt), "prompt");

        AddImageStream(form, request.InputPath, "image[]");


        var validReferences = request.ReferencePaths
            .Where(p => File.Exists(p) && ValidateImageFile(p) is null)
            .ToList();

        foreach (var reference in validReferences)
            AddImageStream(form, reference, "image[]");

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            "https://api.openai.com/v1/images/edits");

        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        message.Content = form;

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, null, "The request timed out.");
        }
        catch (HttpRequestException ex)
        {
            return new(false, null, $"Network error: {ex.Message}");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new(
                    false,
                    null,
                    $"OpenAI API returned {(int)response.StatusCode}: {ExtractError(body)}");
            }

            return ParseResponse(body);
        }
    }

    private static string? ValidateImageFile(string path)
    {
        var ext = Path.GetExtension(path);
        if (!SupportedExtensions.Contains(ext))
            return $"Unsupported image format '{ext}': {path}";

        var info = new FileInfo(path);
        if (info.Length == 0)
            return $"Image file is empty: {path}";

        if (info.Length > MaxImageBytes)
            return $"Image exceeds {MaxImageBytes / (1024 * 1024)} MB limit: {path}";

        return null;
    }

    private static void AddImageStream(
        MultipartFormDataContent form,
        string path,
        string fieldName)
    {
        // FileStream is disposed by MultipartFormDataContent when it disposes its contents
        var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true);

        var content = new StreamContent(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue(GetMimeType(path));
        form.Add(content, fieldName, Path.GetFileName(path));
    }

    private static ImageEditResult ParseResponse(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            var data = json.RootElement.GetProperty("data");

            if (data.GetArrayLength() == 0)
                return new(false, null, "The API returned no generated image.");

            var item = data[0];

            // gpt-image-1 returns b64_json by default; url is also possible
            if (item.TryGetProperty("b64_json", out var b64) &&
                b64.GetString() is { Length: > 0 } b64String)
            {
                return new(true, Convert.FromBase64String(b64String), null);
            }

            if (item.TryGetProperty("url", out var url) &&
                url.GetString() is { Length: > 0 } urlString)
            {
                return new(false, null,
                    $"API returned a URL instead of base64 data. URL: {urlString}");
            }

            return new(false, null, "The API response contained no image data.");
        }
        catch (Exception ex)
        {
            return new(false, null, $"Could not parse API response: {ex.Message}");
        }
    }

    private static string GetMimeType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

    private static string ExtractError(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
                return message.GetString() ?? body;
        }
        catch { }

        return body.Length > 1000 ? body[..1000] : body;
    }
}
