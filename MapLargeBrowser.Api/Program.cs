using System.Text.Json.Serialization;
using MapLargeBrowser.Api.Configuration;
using MapLargeBrowser.Api.Services;
using Microsoft.AspNetCore.Http.Features;

namespace MapLargeBrowser.Api;

public static class Program
{
    private const long MaxUploadBytes = 250L * 1024 * 1024;
    private const string DevCorsPolicy = "DevCors";

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });
        builder.Services.AddSingleton(BrowseRoot.Resolve(builder.Environment));
        builder.Services.AddSingleton<IPathResolver, PathResolver>();
        builder.Services.AddSingleton<ISampleSeeder, SampleSeeder>();
        builder.Services.AddSingleton<IFileBrowser, FileBrowser>();
        builder.Services.AddHostedService<SampleSeedingStartup>();

        builder.Services.Configure<FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = MaxUploadBytes;
        });

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Limits.MaxRequestBodySize = MaxUploadBytes;
        });

        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(DevCorsPolicy, policy => policy
                    .AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod());
            });
        }

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.UseCors(DevCorsPolicy);
        }

        app.MapControllers();

        app.Run();
    }
}
