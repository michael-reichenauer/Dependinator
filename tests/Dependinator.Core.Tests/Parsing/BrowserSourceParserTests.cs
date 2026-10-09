using System.IO.Compression;
using System.Net;
using Dependinator.Core.Parsing;
using Dependinator.Core.Parsing.Sources.Wasm;
using Dependinator.Core.Shared;

namespace Dependinator.Core.Tests.Parsing;

// The browser cannot parse, so its parser only serves the pre-parsed demo models, each from its
// own static asset.
public class BrowserSourceParserTests
{
    [Theory]
    [InlineData("/Demo.sln", true)]
    [InlineData("/Demo.bicep", true)]
    [InlineData("/workspaces/App/App.sln", false)]
    [InlineData("/workspaces/App/infra/main.bicep", false)]
    public void CanParse_ShouldAcceptOnlyTheDemoModels(string path, bool expected)
    {
        Assert.Equal(expected, new SourceParser(new HttpClient()).CanParse(path));
    }

    [Theory]
    [InlineData("/Demo.sln", "demo.model")]
    [InlineData("/Demo.bicep", "demo-bicep.model")]
    public async Task ParseAsync_ShouldDownloadTheDemoModelAsset(string path, string expectedFile)
    {
        var handler = new RecordingHandler(GzipJson("""[{"Node":{"Name":"Demo","Properties":{"Type":"Solution"}}}]"""));
        var parser = new SourceParser(new HttpClient(handler) { BaseAddress = new Uri("https://app.test/") });

        var items = AssertOk(await parser.ParseAsync(path, SolutionParseOptions.Default));

        Assert.Equal($"https://app.test/{expectedFile}", handler.RequestedUri);
        Assert.Equal("Demo", Assert.Single(items).Node!.Name);
    }

    [Fact]
    public async Task ParseAsync_ShouldReturnError_ForOtherPaths()
    {
        var parser = new SourceParser(new HttpClient(new RecordingHandler([])));

        var e = AssertError(await parser.ParseAsync("/workspaces/App/App.sln", SolutionParseOptions.Default));

        Assert.Contains("not supported", e.Message);
    }

    static byte[] GzipJson(string json)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        using (var writer = new StreamWriter(gzip))
        {
            writer.Write(json);
        }
        return output.ToArray();
    }

    sealed class RecordingHandler(byte[] content) : HttpMessageHandler
    {
        public string? RequestedUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestedUri = request.RequestUri?.ToString();
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) }
            );
        }
    }
}
