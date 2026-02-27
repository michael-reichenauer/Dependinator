using System.Globalization;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Options;
using Shared;

namespace Api;

public interface ICloudModelStore
{
    Task<CloudModelDocument?> GetAsync(CloudUserInfo user, string modelKey, CancellationToken cancellationToken);
    Task<CloudModelMetadata> PutAsync(
        CloudUserInfo user,
        CloudModelDocument model,
        CancellationToken cancellationToken
    );
}

public sealed class BlobCloudModelStore : ICloudModelStore
{
    readonly BlobServiceClient blobServiceClient;
    readonly CloudSyncOptions options;

    public BlobCloudModelStore(BlobServiceClient blobServiceClient, IOptions<CloudSyncOptions> options)
    {
        this.blobServiceClient = blobServiceClient;
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
            IDictionary<string, string> metadata = downloadResult.Details.Metadata;

            return new CloudModelDocument(
                modelKey,
                GetMetadataValue(metadata, "normalizedpath"),
                DateTimeOffset.Parse(
                    GetMetadataValue(metadata, "updatedutc"),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind
                ),
                GetMetadataValue(metadata, "contenthash"),
                contentBytes.LongLength,
                Convert.ToBase64String(contentBytes)
            );
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<CloudModelMetadata> PutAsync(
        CloudUserInfo user,
        CloudModelDocument model,
        CancellationToken cancellationToken
    )
    {
        byte[] contentBytes = Convert.FromBase64String(model.CompressedContentBase64);
        BlobContainerClient containerClient = await GetContainerClientAsync(cancellationToken);
        BlobClient blobClient = containerClient.GetBlobClient(GetBlobName(user, model.ModelKey));
        long existingSizeBytes = await GetExistingBlobSizeAsync(blobClient, cancellationToken);
        long usedBytes = await GetUsedBytesAsync(containerClient, user, cancellationToken);
        long newUsedBytes = usedBytes - existingSizeBytes + contentBytes.LongLength;
        if (newUsedBytes > options.MaxUserQuotaBytes)
            throw new CloudSyncQuotaExceededException(newUsedBytes, options.MaxUserQuotaBytes);

        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["normalizedpath"] = model.NormalizedPath,
            ["updatedutc"] = model.UpdatedUtc.ToString("O", CultureInfo.InvariantCulture),
            ["contenthash"] = model.ContentHash,
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

    async Task<BlobContainerClient> GetContainerClientAsync(CancellationToken cancellationToken)
    {
        BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient(options.ContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        return containerClient;
    }

    async Task<long> GetExistingBlobSizeAsync(BlobClient blobClient, CancellationToken cancellationToken)
    {
        try
        {
            Response<BlobProperties> response = await blobClient.GetPropertiesAsync(
                cancellationToken: cancellationToken
            );
            return response.Value.ContentLength;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return 0;
        }
    }

    async Task<long> GetUsedBytesAsync(
        BlobContainerClient containerClient,
        CloudUserInfo user,
        CancellationToken cancellationToken
    )
    {
        long usedBytes = 0;
        string prefix = GetUserPrefix(user);
        await foreach (
            BlobItem blobItem in containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken)
        )
            usedBytes += blobItem.Properties.ContentLength ?? 0;

        return usedBytes;
    }

    static string GetBlobName(CloudUserInfo user, string modelKey) => $"{GetUserPrefix(user)}{modelKey}.json.gz";

    static string GetUserPrefix(CloudUserInfo user) => $"users/{Uri.EscapeDataString(user.UserId)}/models/";

    static string GetMetadataValue(IDictionary<string, string> metadata, string key)
    {
        if (!metadata.TryGetValue(key, out string? value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Blob metadata '{key}' was missing.");

        return value;
    }
}
