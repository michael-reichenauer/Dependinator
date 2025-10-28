using Dependinator.Models;

namespace Dependinator.Shared;

interface IStreamService
{
    R<Stream> ReadStream(string path);
    bool Exists(string path);
}

[Scoped]
class StreamService : IStreamService
{
    readonly IFileService fileService;
    readonly IEmbeddedResources embeddedResources;

    public StreamService(IFileService fileService, IEmbeddedResources embeddedResources)
    {
        this.fileService = fileService;
        this.embeddedResources = embeddedResources;
    }

    public R<Stream> ReadStream(string path)
    {
        if (path == ExampleModel.Path)
        {
            return embeddedResources.OpenResource(ExampleModel.Path);
        }

        return fileService.ReadStream(path);
    }

    public bool Exists(string path)
    {
        return path == ExampleModel.Path || fileService.ExistsStream(path);
    }
}
