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

    public StreamService(IFileService fileService)
    {
        this.fileService = fileService;
    }

    public R<Stream> ReadStream(string path)
    {
        return fileService.ReadStream(path);
    }

    public bool Exists(string path) => fileService.ExistsStream(path);
}
