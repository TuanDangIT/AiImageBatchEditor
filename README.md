https://chatgpt.com/share/6aa723d7-6184-83eb-9be8-6d243f5ce149
# AI Image Batch Editor

Windows desktop MVP: .NET 10 + WPF.

Features:
- Input/output folders
- Multiple reference images
- Reusable image-editing instructions
- Test one image before batch processing
- Batch processing with concurrency
- Retry failed API calls
- Skip outputs that already exist
- OpenAI Images API via HttpClient

## Run

Requirements:
- Windows 10/11
- .NET 10 SDK
- OpenAI API key

PowerShell:
```powershell
$env:OPENAI_API_KEY="your_api_key_here"
dotnet restore
dotnet run --project .\src\AiImageBatchEditor.UI\AiImageBatchEditor.UI.csproj
```

Do not commit the API key.

For a production distributed app, do not ship a shared API key inside the executable. Put the AI API behind your own authenticated backend.
