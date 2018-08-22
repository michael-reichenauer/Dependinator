namespace DependinatorApi.ApiHandling
{
    /// <summary>
    /// The IPC Remoting service base interface.
    /// On the server side, instances of classes, which inherits this class will receive IPC calls.
    /// On client side, proxy instances, based on that type, are used to make IPC calls.
    /// </summary>
    public interface IApiIpcService
    {
    }
}