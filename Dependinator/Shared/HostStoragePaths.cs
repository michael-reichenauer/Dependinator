namespace Dependinator.Shared;

public interface IHostStoragePaths
{
    string ConfigPath { get; }
    string WebFilesPrefix { get; }
}

public record HostStoragePaths(
    string ConfigPath = "/.dependinator/DependinatorConfig.json",
    string WebFilesPrefix = "/.dependinator/web-files/"
) : IHostStoragePaths;
