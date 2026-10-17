using Demo.ServiceContracts.Application;
using Microsoft.Extensions.Configuration;

namespace Demo.Repositories.FileStorage;

public sealed class FilePathResolver(IConfiguration configuration, IUserSectionManager userSectionManager)
{
    const string FOLDER_NAME = "UserData";
    public string GetBuiltinStoreFolder(string? subDir)
    {
        var rootDir = Path.Combine(Environment.CurrentDirectory,
            configuration["FileStoragePath"] ?? throw new InvalidOperationException("Configuration is missing [FileStoragePath]."));

        return subDir is null ? rootDir : Path.Combine(rootDir, subDir);
    }

    public string GetUserStoreFolder(string? subDir)
    {
        var rootDir = Path.Combine(Environment.CurrentDirectory,
            configuration["FileStoragePath"] ?? throw new InvalidOperationException("Configuration is missing [FileStoragePath]."));

        var sectionId = userSectionManager.GetSectionId();
        if (string.IsNullOrWhiteSpace(sectionId))
            throw new InvalidOperationException("User section ID is not available.");

        return subDir is null ? Path.Combine(rootDir, sectionId) : Path.Combine(rootDir, FOLDER_NAME, sectionId, subDir);
    }
}
