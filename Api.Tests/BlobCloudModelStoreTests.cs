using System.Diagnostics;
using System.Collections.Concurrent;
using System.Net.Sockets;
using Api;
using Microsoft.Extensions.Options;

namespace Api.Tests;

public sealed class BlobCloudModelStoreTests : IClassFixture<AzuriteFixture>
{
    readonly AzuriteFixture azuriteFixture;

    public BlobCloudModelStoreTests(AzuriteFixture azuriteFixture)
    {
        this.azuriteFixture = azuriteFixture;
    }

    [Fact]
    public async Task PutAsync_AndGetAsync_ShouldRoundTripModel()
    {
        CloudSyncOptions options = new()
        {
            ContainerName = $"models-{Guid.NewGuid():N}",
            MaxUserQuotaBytes = 1024 * 1024,
            StorageConnectionString = azuriteFixture.ConnectionString,
        };
        BlobCloudModelStore sut = new(Options.Create(options));
        CloudUserInfo user = new("user-1", "user@example.com");
        CloudModelDocument document = new(
            ModelKey: CloudModelPath.CreateKey("/models/test.model"),
            NormalizedPath: "/models/test.model",
            UpdatedUtc: DateTimeOffset.UtcNow,
            ContentHash: "hash",
            CompressedSizeBytes: 4,
            CompressedContentBase64: Convert.ToBase64String([1, 2, 3, 4])
        );

        CloudModelMetadata metadata = await sut.PutAsync(user, document, CancellationToken.None);
        CloudModelDocument? roundTrippedDocument = await sut.GetAsync(user, document.ModelKey, CancellationToken.None);

        Assert.Equal(document.ModelKey, metadata.ModelKey);
        Assert.NotNull(roundTrippedDocument);
        Assert.Equal(document.NormalizedPath, roundTrippedDocument.NormalizedPath);
        Assert.Equal(document.CompressedContentBase64, roundTrippedDocument.CompressedContentBase64);
    }

    [Fact]
    public async Task PutAsync_ShouldThrow_WhenQuotaWouldBeExceeded()
    {
        CloudSyncOptions options = new()
        {
            ContainerName = $"quota-{Guid.NewGuid():N}",
            MaxUserQuotaBytes = 3,
            StorageConnectionString = azuriteFixture.ConnectionString,
        };
        BlobCloudModelStore sut = new(Options.Create(options));
        CloudUserInfo user = new("user-1", "user@example.com");
        CloudModelDocument document = new(
            ModelKey: CloudModelPath.CreateKey("/models/test.model"),
            NormalizedPath: "/models/test.model",
            UpdatedUtc: DateTimeOffset.UtcNow,
            ContentHash: "hash",
            CompressedSizeBytes: 4,
            CompressedContentBase64: Convert.ToBase64String([1, 2, 3, 4])
        );

        await Assert.ThrowsAsync<CloudSyncQuotaExceededException>(() => sut.PutAsync(user, document, CancellationToken.None));
    }
}

public sealed class AzuriteFixture : IAsyncLifetime, IDisposable
{
    const string DevStoreAccountName = "devstoreaccount1";
    const string DevStoreAccountKey =
        "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    Process? process;
    string? tempDirectory;
    readonly ConcurrentQueue<string> standardOutput = new();
    readonly ConcurrentQueue<string> standardError = new();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        int blobPort = GetFreePort();
        int queuePort = GetFreePort();
        int tablePort = GetFreePort();
        tempDirectory = Path.Combine(Path.GetTempPath(), $"dependinator-azurite-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        (string fileName, string arguments) = ResolveCommand(blobPort, queuePort, tablePort, tempDirectory);
        process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.OutputDataReceived += (_, e) => EnqueueLine(standardOutput, e.Data);
        process.ErrorDataReceived += (_, e) => EnqueueLine(standardError, e.Data);

        if (!process.Start())
            throw new InvalidOperationException("Failed to start Azurite.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        ConnectionString =
            $"DefaultEndpointsProtocol=http;AccountName={DevStoreAccountName};AccountKey={DevStoreAccountKey};BlobEndpoint=http://127.0.0.1:{blobPort}/{DevStoreAccountName};";

        await WaitForPortAsync(blobPort);
    }

    public Task DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try
        {
            if (process is { HasExited: false })
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Ignore cleanup failures during test teardown.
        }

        if (!string.IsNullOrWhiteSpace(tempDirectory) && Directory.Exists(tempDirectory))
            Directory.Delete(tempDirectory, recursive: true);
    }

    static (string FileName, string Arguments) ResolveCommand(int blobPort, int queuePort, int tablePort, string location)
    {
        string arguments =
            $"--silent --disableProductStyleUrl --location \"{location}\" --blobPort {blobPort} --queuePort {queuePort} --tablePort {tablePort}";
        if (CommandExists("azurite"))
            return ("azurite", arguments);

        if (CommandExists("npx"))
            return ("npx", $"--yes azurite {arguments}");

        throw new InvalidOperationException("Azurite was not found. Install 'azurite' or ensure 'npx' is available.");
    }

    static bool CommandExists(string commandName)
    {
        string checker = OperatingSystem.IsWindows() ? "where" : "which";
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = checker,
                Arguments = commandName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        process.Start();
        process.WaitForExit();
        return process.ExitCode == 0;
    }

    async Task WaitForPortAsync(int port)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            if (process is { HasExited: true })
                throw new InvalidOperationException(
                    $"Azurite exited before it started listening on port {port}.{Environment.NewLine}{FormatDiagnostics()}"
                );

            try
            {
                using TcpClient client = new();
                await client.ConnectAsync("127.0.0.1", port);
                return;
            }
            catch
            {
                await Task.Delay(200);
            }
        }

        throw new TimeoutException(
            $"Timed out waiting for Azurite on port {port}.{Environment.NewLine}{FormatDiagnostics()}"
        );
    }

    static int GetFreePort()
    {
        TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    static void EnqueueLine(ConcurrentQueue<string> queue, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        queue.Enqueue(line);
        while (queue.Count > 50 && queue.TryDequeue(out _)) { }
    }

    string FormatDiagnostics()
    {
        string output = string.Join(Environment.NewLine, standardOutput);
        string error = string.Join(Environment.NewLine, standardError);
        return $"Azurite stdout:{Environment.NewLine}{output}{Environment.NewLine}Azurite stderr:{Environment.NewLine}{error}";
    }
}
