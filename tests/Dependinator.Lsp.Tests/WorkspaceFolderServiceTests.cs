using Dependinator.Core.Shared;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Dependinator.Lsp.Tests;

public class WorkspaceFolderServiceTests
{
    readonly Mock<IWorkspaceFileService> fileService = new();
    List<string> lastPaths = [];

    public WorkspaceFolderServiceTests()
    {
        fileService
            .Setup(f => f.SetWorkspaceFolders(It.IsAny<IReadOnlyList<string>>()))
            .Callback<IReadOnlyList<string>>(paths => lastPaths = [.. paths]);
    }

    WorkspaceFolderService CreateSut() => new(fileService.Object);

    static WorkspaceFolder Folder(string path) => new() { Uri = DocumentUri.FromFileSystemPath(path), Name = path };

    [Fact]
    public void Initialize_ShouldUseWorkspaceFolders_WhenProvided()
    {
        WorkspaceFolderService sut = CreateSut();
        InitializeParams initializeParams = new()
        {
            WorkspaceFolders = new Container<WorkspaceFolder>(Folder("/repo/a"), Folder("/repo/b")),
            RootUri = DocumentUri.FromFileSystemPath("/ignored"),
        };

        sut.Initialize(initializeParams);

        Assert.Equal(["/repo/a", "/repo/b"], lastPaths.Order());
    }

    [Fact]
    public void Initialize_ShouldFallBackToRootUri_WhenNoWorkspaceFolders()
    {
        WorkspaceFolderService sut = CreateSut();
        InitializeParams initializeParams = new() { RootUri = DocumentUri.FromFileSystemPath("/repo/root") };

        sut.Initialize(initializeParams);

        Assert.Equal(["/repo/root"], lastPaths);
    }

    [Fact]
    public void AddFolders_ShouldAddNewFolders_AndIgnoreDuplicates()
    {
        WorkspaceFolderService sut = CreateSut();
        sut.Initialize(new InitializeParams { RootUri = DocumentUri.FromFileSystemPath("/repo/a") });

        sut.AddFolders([Folder("/repo/b"), Folder("/Repo/A")]);

        Assert.Equal(["/repo/a", "/repo/b"], lastPaths.Order());
    }

    [Fact]
    public void RemoveFolders_ShouldRemoveFolders()
    {
        WorkspaceFolderService sut = CreateSut();
        sut.Initialize(
            new InitializeParams
            {
                WorkspaceFolders = new Container<WorkspaceFolder>(Folder("/repo/a"), Folder("/repo/b")),
            }
        );

        sut.RemoveFolders([Folder("/repo/a")]);

        Assert.Equal(["/repo/b"], lastPaths);
    }
}
