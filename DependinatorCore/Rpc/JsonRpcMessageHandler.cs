using System.Buffers;
using System.Buffers.Binary;
using System.Threading.Channels;
using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace DependinatorCore.Rpc;

public delegate ValueTask WriteBinaryMessageActionAsync(ReadOnlyMemory<byte> binaryMessage, CancellationToken ct);
public delegate ValueTask WriteBase64MessageActionAsync(string base64Message, CancellationToken ct);

public sealed class JsonRpcMessageHandler : MessageHandlerBase
{
    const int MaxChunkSize = 10 * 1024;
    const int ChunkPrefixLength = 4;
    const int ChunkHeaderSize = ChunkPrefixLength + sizeof(int) + sizeof(int);
    const int MaxChunkPayloadSize = MaxChunkSize - ChunkHeaderSize;
    static ReadOnlySpan<byte> ChunkPrefix => "DNK1"u8;

    readonly SemaphoreSlim writeGate = new(1, 1);
    readonly object readGate = new();
    readonly Channel<ReadOnlyMemory<byte>> messagesChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    WriteBinaryMessageActionAsync sendBinaryMessageActionAsync = null!;
    byte[]? pendingChunkBuffer;
    int pendingChunkTotalLength;
    int pendingChunkBytesReceived;
    int pendingChunkNextOffset;

    public JsonRpcMessageHandler()
        //       : base(new StreamJsonRpc.MessagePackFormatter()) { } // More efficient
        : base(CreateFormatter()) { }

    static IJsonRpcMessageFormatter CreateFormatter()
    {
        var formatter = new SystemTextJsonFormatter();
        formatter.JsonSerializerOptions.Converters.Add(new ResultJsonConverter());
        formatter.JsonSerializerOptions.Converters.Add(new ResultJsonConverterFactory());
        return formatter;
    }

    public override bool CanRead => true;
    public override bool CanWrite => true;

    // Registers the write message action, that should transfer the message to the "other" side
    public void RegisterSendMessageAction(WriteBinaryMessageActionAsync sendBinaryMessageActionAsync)
    {
        if (sendBinaryMessageActionAsync is null)
            throw new InvalidOperationException($"{nameof(sendBinaryMessageActionAsync)} cannot be null");

        this.sendBinaryMessageActionAsync = sendBinaryMessageActionAsync;
    }

    // Registers the write message action, that should transfer the message to the "other" side
    public void RegisterSendMessageAction(WriteBase64MessageActionAsync sendBase64MessageActionAsync)
    {
        if (sendBase64MessageActionAsync is null)
            throw new InvalidOperationException($"{nameof(sendBase64MessageActionAsync)} cannot be null");

        this.sendBinaryMessageActionAsync = async (binaryMessage, ct) =>
        {
            var base64Message = Convert.ToBase64String(binaryMessage.ToArray());
            await sendBase64MessageActionAsync(base64Message, ct).ConfigureAwait(false);
        };
    }

    // Adds the message, that was received from the "other" side
    public ValueTask AddReceivedMessageAsync(string base64Message, CancellationToken ct)
    {
        var binaryPackage = Convert.FromBase64String(base64Message);
        return AddReceivedMessageAsync(binaryPackage, ct);
    }

    // Adds the message, that was received from the "other" side
    public ValueTask AddReceivedMessageAsync(ReadOnlyMemory<byte> binaryMessage, CancellationToken ct)
    {
        // Log.Info($"Received binary message {binaryMessage.Length} bytes");
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
                pendingChunkBytesReceived += payload.Length;
                pendingChunkNextOffset += payload.Length;

                if (pendingChunkBytesReceived < pendingChunkTotalLength)
                    return ValueTask.CompletedTask; // Got, part of total, need more parts...

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

        // Write total message
        return messagesChannel.Writer.WriteAsync(messageToWrite.Value, ct);
    }

    protected override async ValueTask<JsonRpcMessage?> ReadCoreAsync(CancellationToken ct)
    {
        var payload = await messagesChannel.Reader.ReadAsync(ct).ConfigureAwait(false);
        Log.Info($"Read message {payload.Length} bytes");

        // Formatter expects a ReadOnlySequence<byte>.
        var seq = new ReadOnlySequence<byte>(payload);
        var rpcMessage = this.Formatter.Deserialize(seq);

        return rpcMessage;
    }

    protected override async ValueTask WriteCoreAsync(JsonRpcMessage rpcMessage, CancellationToken ct)
    {
        if (sendBinaryMessageActionAsync is null)
            throw new InvalidOperationException($"{nameof(RegisterSendMessageAction)} has not yet been called");

        // Serialize message into a buffer.
        var buffer = new ArrayBufferWriter<byte>(initialCapacity: 1024);
        this.Formatter.Serialize(buffer, rpcMessage);

        await writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Log.Info($"Write message {buffer.WrittenMemory.Length} bytes");
            await SendBinaryMessageAsync(buffer.WrittenMemory, ct);
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

    async ValueTask SendBinaryMessageAsync(ReadOnlyMemory<byte> binaryMessage, CancellationToken ct)
    {
        // Check if the message can be sent as is or needs to be split in sub chunks
        if (binaryMessage.Length <= MaxChunkSize)
        {
            // No need to split, send total message as is
            // Log.Info($"Send binary message {binaryMessage.Length} bytes");
            await sendBinaryMessageActionAsync(binaryMessage, ct).ConfigureAwait(false);
            return;
        }

        // Splitting message in sub chunks sent one ofter the other, which will be merged in other
        // side by AddReceivedMessageAsync()
        var totalLength = binaryMessage.Length;
        for (var offset = 0; offset < totalLength; offset += MaxChunkPayloadSize)
        {
            var payloadLength = Math.Min(MaxChunkPayloadSize, totalLength - offset);
            var chunk = new byte[ChunkHeaderSize + payloadLength];
            WriteChunkHeader(chunk.AsSpan(0, ChunkHeaderSize), totalLength, offset);
            binaryMessage.Slice(offset, payloadLength).Span.CopyTo(chunk.AsSpan(ChunkHeaderSize));

            // Send sub chunk
            // Log.Info($"Send binary chunk message {chunk.Length} bytes");
            await sendBinaryMessageActionAsync(chunk, ct).ConfigureAwait(false);
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

    void StartPendingChunk(int totalLength)
    {
        pendingChunkBuffer = new byte[totalLength];
        pendingChunkTotalLength = totalLength;
        pendingChunkBytesReceived = 0;
        pendingChunkNextOffset = 0;
    }

    void ResetPendingChunk()
    {
        pendingChunkBuffer = null;
        pendingChunkTotalLength = 0;
        pendingChunkBytesReceived = 0;
        pendingChunkNextOffset = 0;
    }
}
