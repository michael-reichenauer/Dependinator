using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace Dependinator.Core.Rpc;

public delegate ValueTask SendBinaryMessageAsync(ReadOnlyMemory<byte> binaryMessage, CancellationToken ct);
public delegate ValueTask SendBase64MessageAsync(string base64Message, CancellationToken ct);

// StreamJsonRpc message handler that transfers JSON-RPC messages over a caller-provided send
// action, either as binary or base64 (for string-based transports like the VS Code webview
// and LSP tunnel). Two framing layers are applied on top of the serialized JSON payload:
// * Compression: payloads >= MinCompressionPayloadSize are gzipped and prefixed with
//   "DNG1" + <uncompressedLength>, but only when it actually saves space.
// * Chunking: messages larger than MaxChunkSize are then split into ordered chunks, each
//   prefixed with "DNK1" + <totalLength> + <offset>, and reassembled by the receiver, so the
//   transports never see a single huge message.
// Plain JSON payloads start with '{', so they cannot be mistaken for a prefixed message.
// The peers are trusted (our own hosts); malformed or out-of-sequence messages are validated
// for consistency, then logged and dropped, which leaves the remote caller's pending request
// unanswered.
public sealed class JsonRpcMessageHandler : MessageHandlerBase
{
    const int MaxChunkSize = 1000 * 1024;
    const int ChunkPrefixLength = 4;
    const int ChunkHeaderSize = ChunkPrefixLength + sizeof(int) + sizeof(int);
    const int MaxChunkPayloadSize = MaxChunkSize - ChunkHeaderSize;
    const int CompressionPrefixLength = 4;
    const int CompressionHeaderSize = CompressionPrefixLength + sizeof(int);
    const int MinCompressionPayloadSize = 64 * 1024;
    const int MinCompressionSavingsBytes = 1024;
    static ReadOnlySpan<byte> ChunkPrefix => "DNK1"u8;
    static ReadOnlySpan<byte> CompressionPrefix => "DNG1"u8;
    static readonly bool IsCompressionSupported = CheckCompressionSupport();

    readonly SemaphoreSlim writeGate = new(1, 1);
    readonly object readGate = new();
    readonly Channel<ReadOnlyMemory<byte>> messagesChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    SendBinaryMessageAsync sendBinaryMessageAsync = null!;
    byte[]? pendingChunkBuffer;
    int pendingChunkTotalLength;
    int pendingChunkNextOffset;

    public JsonRpcMessageHandler()
        : base(CreateFormatter()) { }

    static IJsonRpcMessageFormatter CreateFormatter()
    {
        // System.Text.Json is used (rather than the more compact MessagePackFormatter) so the
        // custom converters for R/R<T> results in RPC interfaces can be applied.
        var formatter = new SystemTextJsonFormatter();
        formatter.JsonSerializerOptions.Converters.Add(new ResultJsonConverter());
        formatter.JsonSerializerOptions.Converters.Add(new ResultJsonConverterFactory());
        return formatter;
    }

    public override bool CanRead => true;
    public override bool CanWrite => true;

    // Registers the send message action, that should transfer the message to the "other" side
    public void RegisterSendMessageAction(SendBinaryMessageAsync sendBinaryMessageAsync)
    {
        ArgumentNullException.ThrowIfNull(sendBinaryMessageAsync);

        this.sendBinaryMessageAsync = sendBinaryMessageAsync;
    }

    // Registers the send message action, that should transfer the message to the "other" side
    public void RegisterSendMessageAction(SendBase64MessageAsync sendBase64MessageAsync)
    {
        ArgumentNullException.ThrowIfNull(sendBase64MessageAsync);

        this.sendBinaryMessageAsync = async (binaryMessage, ct) =>
        {
            var base64Message = Convert.ToBase64String(binaryMessage.Span);
            await sendBase64MessageAsync(base64Message, ct).ConfigureAwait(false);
        };
    }

    // Adds the message, that was received from the "other" side
    public ValueTask AddReceivedMessageAsync(string base64Message, CancellationToken ct)
    {
        var binaryPackage = Convert.FromBase64String(base64Message);
        return AddReceivedMessageAsync(binaryPackage, ct);
    }

    // Adds the message, that was received from the "other" side. Messages must be delivered in
    // send order (chunk reassembly relies on it), and the buffer must not be reused by the
    // caller since it is kept until the message has been processed.
    public ValueTask AddReceivedMessageAsync(ReadOnlyMemory<byte> binaryMessage, CancellationToken ct)
    {
        ReadOnlyMemory<byte>? messageToWrite = null;

        lock (readGate)
        {
            // Check if other side sent a chunked part or a larger message
            if (TryReadChunkHeader(binaryMessage, out var totalLength, out var offset, out var payload))
            {
                if (offset == 0)
                    StartPendingChunk(totalLength);

                if (
                    pendingChunkBuffer is null
                    || totalLength != pendingChunkTotalLength
                    || offset != pendingChunkNextOffset
                )
                {
                    Log.Error("Chunked message out of sequence");
                    ResetPendingChunk();
                    return ValueTask.CompletedTask;
                }

                // Store this chunk in pending buffer
                payload.Span.CopyTo(pendingChunkBuffer.AsSpan(offset));
                pendingChunkNextOffset += payload.Length;

                if (pendingChunkNextOffset < pendingChunkTotalLength)
                    return ValueTask.CompletedTask; // Got part of total, need more parts...

                // Got total message,
                messageToWrite = pendingChunkBuffer;
                ResetPendingChunk();
            }
            else
            {
                if (pendingChunkBuffer is not null)
                {
                    Log.Error("Chunked message interrupted by non-chunked payload");
                    ResetPendingChunk();
                    return ValueTask.CompletedTask;
                }

                // a normal "small" non chunked message is total message
                messageToWrite = binaryMessage;
            }
        }

        if (messageToWrite.Value.Span.StartsWith(CompressionPrefix))
        {
            if (!IsCompressionSupported)
            {
                Log.Error("Received compressed RPC message but compression is not supported on this platform");
                return ValueTask.CompletedTask;
            }

            if (!TryReadCompressedHeader(messageToWrite.Value, out var uncompressedLength, out var compressedPayload))
            {
                Log.Error("Invalid compressed message header");
                return ValueTask.CompletedTask;
            }

            if (!TryDecompressMessage(compressedPayload, uncompressedLength, out var decompressed))
            {
                Log.Error("Failed to decompress RPC message");
                return ValueTask.CompletedTask;
            }

            messageToWrite = decompressed;
        }

        // Write total message
        return messagesChannel.Writer.WriteAsync(messageToWrite.Value, ct);
    }

    protected override async ValueTask<JsonRpcMessage?> ReadCoreAsync(CancellationToken ct)
    {
        var payload = await messagesChannel.Reader.ReadAsync(ct).ConfigureAwait(false);

        // Formatter expects a ReadOnlySequence<byte>.
        var seq = new ReadOnlySequence<byte>(payload);
        var rpcMessage = this.Formatter.Deserialize(seq);

        return rpcMessage;
    }

    protected override async ValueTask WriteCoreAsync(JsonRpcMessage rpcMessage, CancellationToken ct)
    {
        if (sendBinaryMessageAsync is null)
            throw new InvalidOperationException($"{nameof(RegisterSendMessageAction)} has not yet been called");

        // Serialize message into a buffer.
        var buffer = new ArrayBufferWriter<byte>(initialCapacity: 1024);
        this.Formatter.Serialize(buffer, rpcMessage);

        await writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var payloadToSend = buffer.WrittenMemory;
            if (IsCompressionSupported && TryCompressMessage(payloadToSend, out var compressedMessage))
            {
                payloadToSend = compressedMessage;
            }

            await SendMessageAsync(payloadToSend, ct);
        }
        finally
        {
            writeGate.Release();
        }
    }

    protected override ValueTask FlushAsync(CancellationToken ct) => default;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            writeGate.Dispose();
        }
        base.Dispose(disposing);
    }

    async ValueTask SendMessageAsync(ReadOnlyMemory<byte> binaryMessage, CancellationToken ct)
    {
        // Check if the message can be sent as is or needs to be split in sub chunks
        if (binaryMessage.Length <= MaxChunkSize)
        {
            // No need to split, send total message as is
            await sendBinaryMessageAsync(binaryMessage, ct).ConfigureAwait(false);
            return;
        }

        // Splitting message in sub chunks sent one after the other, which will be merged in other
        // side by AddReceivedMessageAsync()
        var totalLength = binaryMessage.Length;
        for (var offset = 0; offset < totalLength; offset += MaxChunkPayloadSize)
        {
            var payloadLength = Math.Min(MaxChunkPayloadSize, totalLength - offset);
            var chunk = new byte[ChunkHeaderSize + payloadLength];
            WriteChunkHeader(chunk.AsSpan(0, ChunkHeaderSize), totalLength, offset);
            binaryMessage.Slice(offset, payloadLength).Span.CopyTo(chunk.AsSpan(ChunkHeaderSize));

            // Send sub chunk
            await sendBinaryMessageAsync(chunk, ct).ConfigureAwait(false);
        }
    }

    static void WriteChunkHeader(Span<byte> header, int totalLength, int offset)
    {
        ChunkPrefix.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(ChunkPrefixLength, sizeof(int)), totalLength);
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(ChunkPrefixLength + sizeof(int), sizeof(int)), offset);
    }

    static bool TryReadChunkHeader(
        ReadOnlyMemory<byte> message,
        out int totalLength,
        out int offset,
        out ReadOnlyMemory<byte> payload
    )
    {
        totalLength = 0;
        offset = 0;
        payload = default;

        if (message.Length > MaxChunkSize || message.Length <= ChunkHeaderSize)
            return false;

        var span = message.Span;
        if (!span.StartsWith(ChunkPrefix))
            return false;

        totalLength = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(ChunkPrefixLength, sizeof(int)));
        offset = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(ChunkPrefixLength + sizeof(int), sizeof(int)));

        var payloadLength = message.Length - ChunkHeaderSize;
        if (totalLength <= 0 || offset < 0 || offset + payloadLength > totalLength)
            return false;

        payload = message.Slice(ChunkHeaderSize);
        return true;
    }

    static bool TryDecompressMessage(
        ReadOnlyMemory<byte> compressedPayload,
        int uncompressedLength,
        out ReadOnlyMemory<byte> decompressedMessage
    )
    {
        decompressedMessage = default;
        try
        {
            // Wrap the payload's underlying array (always array-backed here) to avoid a copy
            if (!MemoryMarshal.TryGetArray(compressedPayload, out ArraySegment<byte> segment))
                segment = new ArraySegment<byte>(compressedPayload.ToArray());

            using var input = new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: false);

            var decompressed = new byte[uncompressedLength];
            gzip.ReadExactly(decompressed);

            // Ensure the payload does not decompress to more bytes than declared in the header.
            if (gzip.ReadByte() != -1)
                return false;

            decompressedMessage = decompressed;
            return true;
        }
        catch (Exception ex)
            when (ex is PlatformNotSupportedException
                || ex is InvalidDataException
                || ex is EndOfStreamException
                || ex is IOException
            )
        {
            return false;
        }
    }

    static bool CheckCompressionSupport()
    {
        var source = new byte[] { 1, 2, 3, 4 };
        try
        {
            using var compressed = new MemoryStream();
            using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
            {
                gzip.Write(source);
            }

            compressed.Position = 0;
            using var decompress = new GZipStream(compressed, CompressionMode.Decompress, leaveOpen: false);
            Span<byte> roundtrip = stackalloc byte[4];
            decompress.ReadExactly(roundtrip);
            return roundtrip.SequenceEqual(source);
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
    }

    static bool TryCompressMessage(ReadOnlyMemory<byte> message, out ReadOnlyMemory<byte> compressedMessage)
    {
        compressedMessage = default;
        if (message.Length < MinCompressionPayloadSize)
            return false;

        byte[] compressedBuffer;
        try
        {
            using var output = new MemoryStream(capacity: CompressionHeaderSize + message.Length / 2);
            var header = new byte[CompressionHeaderSize];
            WriteCompressedHeader(header, message.Length);
            output.Write(header);

            using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
            {
                gzip.Write(message.Span);
            }

            compressedBuffer = output.ToArray();
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }

        if (message.Length - compressedBuffer.Length < MinCompressionSavingsBytes)
            return false;

        compressedMessage = compressedBuffer;
        return true;
    }

    static void WriteCompressedHeader(Span<byte> header, int uncompressedLength)
    {
        CompressionPrefix.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(CompressionPrefixLength, sizeof(int)), uncompressedLength);
    }

    static bool TryReadCompressedHeader(
        ReadOnlyMemory<byte> message,
        out int uncompressedLength,
        out ReadOnlyMemory<byte> compressedPayload
    )
    {
        uncompressedLength = 0;
        compressedPayload = default;

        if (message.Length <= CompressionHeaderSize)
            return false;

        var span = message.Span;
        if (!span.StartsWith(CompressionPrefix))
            return false;

        uncompressedLength = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(CompressionPrefixLength, sizeof(int)));

        var compressedPayloadLength = message.Length - CompressionHeaderSize;
        if (uncompressedLength <= 0 || compressedPayloadLength <= 0)
            return false;

        compressedPayload = message.Slice(CompressionHeaderSize);
        return true;
    }

    void StartPendingChunk(int totalLength)
    {
        pendingChunkBuffer = new byte[totalLength];
        pendingChunkTotalLength = totalLength;
        pendingChunkNextOffset = 0;
    }

    void ResetPendingChunk()
    {
        pendingChunkBuffer = null;
        pendingChunkTotalLength = 0;
        pendingChunkNextOffset = 0;
    }
}
