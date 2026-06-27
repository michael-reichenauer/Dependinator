namespace Api;

public sealed class CloudSyncQuotaExceededException : InvalidOperationException
{
    public CloudSyncQuotaExceededException(long usedBytes, long maxBytes)
        : base($"Cloud sync quota exceeded. Used {usedBytes} bytes of {maxBytes} bytes.")
    {
        UsedBytes = usedBytes;
        MaxBytes = maxBytes;
    }

    public long UsedBytes { get; }
    public long MaxBytes { get; }
}
