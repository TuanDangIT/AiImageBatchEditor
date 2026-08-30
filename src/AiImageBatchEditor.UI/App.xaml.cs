using System.Net.Http;
using System.Windows;
using AiImageBatchEditor.Core;
using Microsoft.Extensions.DependencyInjection;

namespace AiImageBatchEditor.UI;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        var apiKey = "API_KEY";

        services.AddSingleton(new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        });

        services.AddSingleton<IImageAiProvider>(sp =>
            new OpenAiImageProvider(
                sp.GetRequiredService<HttpClient>(),
                apiKey));

        services.AddSingleton<IBatchProcessor, BatchProcessor>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        Services = services.BuildServiceProvider();
        Services.GetRequiredService<MainWindow>().Show();
    }
}
