using System.Globalization;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Shared;

namespace Api;

public interface ICloudModelStore
{
    Task<IReadOnlyList<CloudModelMetadata>> ListAsync(CloudUserInfo user, CancellationToken cancellationToken);
    Task<CloudModelDocument?> GetAsync(CloudUserInfo user, string modelKey, CancellationToken cancellationToken);
    Task<CloudModelMetadata> PutAsync(
        CloudUserInfo user,
        CloudModelDocument model,
        CancellationToken cancellationToken
    );
}

public sealed class BlobCloudModelStore : ICloudModelStore
{
    const string NormalizedPathMetadataKey = "normalizedpath";
    const string UpdatedUtcMetadataKey = "updatedutc";
    const string ContentHashMetadataKey = "contenthash";

    readonly CloudSyncOptions options;
    BlobContainerClient? cachedContainerClient;

    public BlobCloudModelStore(IOptions<CloudSyncOptions> options)
    {
        this.options = options.Value;
    }

    public async Task<CloudModelDocument?> GetAsync(
        CloudUserInfo user,
        string modelKey,
        CancellationToken cancellationToken
    )
    {
        BlobContainerClient containerClient = await GetContainerClientAsync(cancellationToken);
        BlobClient blobClient = containerClient.GetBlobClient(GetBlobName(user, modelKey));

        try
        {
            BlobDownloadResult downloadResult = await blobClient.DownloadContentAsync(cancellationToken);
            byte[] contentBytes = downloadResult.Content.ToArray();
            CloudModelMetadata metadata = ToModelMetadata(
                modelKey,
                downloadResult.Details.Metadata,
                contentBytes.LongLength
            );

            return new CloudModelDocument(
                metadata.ModelKey,
                metadata.NormalizedPath,
                metadata.UpdatedUtc,
                metadata.ContentHash,
                metadata.CompressedSizeBytes,
                Convert.ToBase64String(contentBytes)
            );
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<CloudModelMetadata>> ListAsync(
        CloudUserInfo user,
        CancellationToken cancellationToken
    )
    {
        BlobContainerClient containerClient = await GetContainerClientAsync(cancellationToken);
        string prefix = GetUserPrefix(user);
        List<CloudModelMetadata> models = [];

        await foreach (
            BlobItem blobItem in containerClient.GetBlobsAsync(
                traits: BlobTraits.Metadata,
                states: BlobStates.None,
                prefix: prefix,
                cancellationToken: cancellationToken
            )
        )
        {
            string blobName = blobItem.Name;
            if (!blobName.EndsWith(".json.gz", StringComparison.OrdinalIgnoreCase))
                continue;

            string modelKey = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(blobName));
            models.Add(ToModelMetadata(modelKey, blobItem.Metadata, blobItem.Properties.ContentLength ?? 0));
        }

        return models.OrderByDescending(model => model.UpdatedUtc).ToList();
    }

    public async Task<CloudModelMetadata> PutAsync(
        CloudUserInfo user,
        CloudModelDocument model,
        CancellationToken cancellationToken
    )
    {
        byte[] contentBytes = Convert.FromBase64String(model.CompressedContentBase64);
        BlobContainerClient containerClient = await GetContainerClientAsync(cancellationToken);
        string blobName = GetBlobName(user, model.ModelKey);
        BlobClient blobClient = containerClient.GetBlobClient(blobName);
        long otherModelsBytes = await GetUsedBytesAsync(containerClient, user, blobName, cancellationToken);
        long newUsedBytes = otherModelsBytes + contentBytes.LongLength;
        if (newUsedBytes > options.MaxUserQuotaBytes)
            throw new CloudSyncQuotaExceededException(newUsedBytes, options.MaxUserQuotaBytes);

        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            [NormalizedPathMetadataKey] = model.NormalizedPath,
            [UpdatedUtcMetadataKey] = model.UpdatedUtc.ToString("O", CultureInfo.InvariantCulture),
            [ContentHashMetadataKey] = model.ContentHash,
        };

        using MemoryStream contentStream = new(contentBytes, writable: false);
        await blobClient.UploadAsync(
            contentStream,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/gzip" },
                Metadata = metadata,
            },
            cancellationToken
        );

        return new CloudModelMetadata(
            model.ModelKey,
            model.NormalizedPath,
            model.UpdatedUtc,
            model.ContentHash,
            contentBytes.LongLength
        );
    }

    // Cached after the container has been created once; concurrent first calls may
    // create the container twice, which is idempotent.
    async Task<BlobContainerClient> GetContainerClientAsync(CancellationToken cancellationToken)
    {
        BlobContainerClient? containerClient = cachedContainerClient;
        if (containerClient is not null)
            return containerClient;

        BlobServiceClient blobServiceClient = CreateBlobServiceClient();
        containerClient = blobServiceClient.GetBlobContainerClient(options.ContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        cachedContainerClient = containerClient;
        return containerClient;
    }

    BlobServiceClient CreateBlobServiceClient()
    {
        string? connectionString = options.StorageConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("No blob storage connection string was configured.");

        return new BlobServiceClient(connectionString);
    }

    // Sums the user's stored bytes, excluding the blob about to be overwritten so the
    // quota check compares against the size after the upload replaces it.
    static async Task<long> GetUsedBytesAsync(
        BlobContainerClient containerClient,
        CloudUserInfo user,
        string excludedBlobName,
        CancellationToken cancellationToken
    )
    {
        long usedBytes = 0;
        string prefix = GetUserPrefix(user);
        await foreach (
            BlobItem blobItem in containerClient.GetBlobsAsync(
                traits: BlobTraits.None,
                states: BlobStates.None,
                prefix: prefix,
                cancellationToken: cancellationToken
            )
        )
        {
            if (string.Equals(blobItem.Name, excludedBlobName, StringComparison.Ordinal))
                continue;

            usedBytes += blobItem.Properties.ContentLength ?? 0;
        }

        return usedBytes;
    }

    static string GetBlobName(CloudUserInfo user, string modelKey) => $"{GetUserPrefix(user)}{modelKey}.json.gz";

    static string GetUserPrefix(CloudUserInfo user)
    {
        string storageKey = CloudUserStorageKey.Create(user);
        return $"users/{Uri.EscapeDataString(storageKey)}/models/";
    }

    static CloudModelMetadata ToModelMetadata(
        string modelKey,
        IDictionary<string, string> metadata,
        long compressedSizeBytes
    )
    {
        return new CloudModelMetadata(
            modelKey,
            GetMetadataValue(metadata, NormalizedPathMetadataKey),
            DateTimeOffset.Parse(
                GetMetadataValue(metadata, UpdatedUtcMetadataKey),
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind
            ),
            GetMetadataValue(metadata, ContentHashMetadataKey),
            compressedSizeBytes
        );
    }

    static string GetMetadataValue(IDictionary<string, string> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Blob metadata '{key}' was missing.");

        return value;
    }
}
