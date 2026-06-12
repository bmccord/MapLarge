using MapLargeBrowser.Api.Configuration;

namespace MapLargeBrowser.Api.Services;

public sealed class SampleSeedingStartup(BrowseRoot root, ISampleSeeder seeder) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (root.IsFallback && seeder.IsEmpty(root.AbsolutePath))
            seeder.Seed(root.AbsolutePath);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
