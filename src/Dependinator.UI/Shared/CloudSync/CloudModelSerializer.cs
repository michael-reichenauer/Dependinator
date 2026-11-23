using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dependinator.UI.Modeling.Dtos;
using Dependinator.UI.Shared.Types;
using Shared;

namespace Dependinator.UI.Shared.CloudSync;

// Serializes and deserializes cloud model payloads, including hashing and compression.
static class CloudModelSerializer
{
    static readonly JsonSerializerOptions serializerOptions = new() { PropertyNameCaseInsensitive = true };

    // Builds a stable cloud document: normalized key/path, content hash, timestamp, and compressed body.
    public static CloudModelDocument CreateDocument(string modelPath, ModelDto modelDto)
    {
        string normalizedPath = CloudModelPath.Normalize(modelPath);
        // Whether the user edited the model since its last sync is each device's own
        modelDto = modelDto with { HasUserEdits = null };
        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(modelDto, serializerOptions);
        byte[] compressedBytes = Compress(jsonBytes);

        return new CloudModelDocument(
            CloudModelPath.CreateKey(normalizedPath),
            normalizedPath,
            DateTimeOffset.UtcNow,
            GetContentHash(modelDto),
            compressedBytes.LongLength,
            Convert.ToBase64String(compressedBytes)
        );
    }

    // Computes SHA-256 hash over the serialized model DTO, excluding what is each device's own:
    // the view-state fields (Zoom, Offset, ViewRect), so that panning/zooming does not trigger
    // sync conflicts, and the mark of the user's edits since the last sync.
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
            HasUserEdits = null,
        };

    // A hash of what the model is made of, as a parse gives it: the nodes and links with their
    // parsed properties, in a fixed order, without anything of the layout. Two models of the
    // same source have the same structure hash whatever was laid out, shown or loaded in which
    // order, so it tells a new parse result from a model that only looks different.
    public static string GetStructureHash(ModelDto modelDto)
    {
        IEnumerable<string> nodes = modelDto.Nodes.Select(n =>
            $"N\t{n.Name}\t{n.ParentName}\t{n.Type}\t{JsonSerializer.Serialize(n.Properties, serializerOptions)}"
        );
        IEnumerable<string> links = modelDto.Links.Select(l =>
            $"L\t{l.SourceName}\t{l.TargetName}\t{l.TargetType}\t{l.IsManual}\t{l.IsInheritance}\t{l.IsConfiguration}"
        );
        IEnumerable<string> problems = modelDto.ParseProblems.Select(p =>
            $"P\t{JsonSerializer.Serialize(p, serializerOptions)}"
        );

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string item in nodes.Concat(links).Concat(problems).Order(StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(item));
            hash.AppendData([(byte)'\n']);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    // Deserializes a compressed cloud document back into a model DTO.
    public static Result<ModelDto> ReadModel(CloudModelDocument document)
    {
        try
        {
            byte[] compressedBytes = Convert.FromBase64String(document.CompressedContentBase64);
            byte[] jsonBytes = Decompress(compressedBytes);
            ModelDto? modelDto = JsonSerializer.Deserialize<ModelDto>(jsonBytes, serializerOptions);
            if (modelDto is null)
                return new Error("Device sync returned an empty model.");

            return modelDto;
        }
        catch (Exception ex)
        {
            return new Error(ex);
        }
    }

    // Reads a pulled document with the cloud's own hash of it (PulledModel), or the hash made here
    // for a document stored without one.
    public static Result<PulledModel> ReadPulledModel(CloudModelDocument document)
    {
        Result<ModelDto> modelResult = ReadModel(document);
        if (modelResult is not ModelDto modelDto)
            return modelResult.Error;

        string contentHash = string.IsNullOrWhiteSpace(document.ContentHash)
            ? GetContentHash(modelDto)
            : document.ContentHash;
        return new PulledModel(modelDto, contentHash);
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
