using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Dependinator.Modeling.Dtos;
using Dependinator.Shared.Types;
using Shared;

namespace Dependinator.Shared.CloudSync;

// Serializes and deserializes cloud model payloads, including hashing and compression.
static class CloudModelSerializer
{
    static readonly JsonSerializerOptions serializerOptions = new() { PropertyNameCaseInsensitive = true };

    // Builds a stable cloud document: normalized key/path, content hash, timestamp, and compressed body.
    public static CloudModelDocument CreateDocument(string modelPath, ModelDto modelDto)
    {
        string normalizedPath = CloudModelPath.Normalize(modelPath);
        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(modelDto, serializerOptions);
        byte[] hashBytes = JsonSerializer.SerializeToUtf8Bytes(StripViewState(modelDto), serializerOptions);
        byte[] compressedBytes = Compress(jsonBytes);

        return new CloudModelDocument(
            CloudModelPath.CreateKey(normalizedPath),
            normalizedPath,
            DateTimeOffset.UtcNow,
            Convert.ToHexString(SHA256.HashData(hashBytes)).ToLowerInvariant(),
            compressedBytes.LongLength,
            Convert.ToBase64String(compressedBytes)
        );
    }

    // Computes SHA-256 hash over the serialized model DTO, excluding view-state fields
    // (Zoom, Offset, ViewRect) so that panning/zooming does not trigger sync conflicts.
    public static string GetContentHash(ModelDto modelDto)
    {
        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(StripViewState(modelDto), serializerOptions);
        return Convert.ToHexString(SHA256.HashData(jsonBytes)).ToLowerInvariant();
    }

    // Zeroes out view-state fields so they don't influence the content hash.
    static ModelDto StripViewState(ModelDto modelDto) =>
        modelDto with
        {
            Zoom = 0,
            Offset = Pos.None,
            ViewRect = Rect.None,
        };

    // Deserializes a compressed cloud document back into a model DTO.
    public static R<ModelDto> ReadModel(CloudModelDocument document)
    {
        try
        {
            byte[] compressedBytes = Convert.FromBase64String(document.CompressedContentBase64);
            byte[] jsonBytes = Decompress(compressedBytes);
            ModelDto? modelDto = JsonSerializer.Deserialize<ModelDto>(jsonBytes, serializerOptions);
            if (modelDto is null)
                return R.Error("Cloud sync returned an empty model.");

            return modelDto;
        }
        catch (Exception ex)
        {
            return R.Error(ex);
        }
    }

    // Compresses JSON bytes with GZip (smallest size mode).
    static byte[] Compress(byte[] jsonBytes)
    {
        using MemoryStream output = new();
        using (GZipStream gzip = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
            gzip.Write(jsonBytes);

        return output.ToArray();
    }

    // Decompresses GZip payload from remote storage.
    static byte[] Decompress(byte[] compressedBytes)
    {
        using MemoryStream input = new(compressedBytes);
        using GZipStream gzip = new(input, CompressionMode.Decompress);
        using MemoryStream output = new();
        gzip.CopyTo(output);
        return output.ToArray();
    }
}
