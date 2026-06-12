namespace MapLargeBrowser.Api.Services;

public interface ISampleSeeder
{
    bool IsEmpty(string rootPath);

    void Seed(string rootPath);

    void Reset(string rootPath);
}
