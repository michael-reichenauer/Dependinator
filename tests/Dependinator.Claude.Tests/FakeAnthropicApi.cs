using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Dependinator.Claude.Tests;

// A local stand-in for Anthropic's API: answers every request with one status and body, and keeps
// the last request's body, so tests run the SDK's real request path without a key or network.
sealed class FakeAnthropicApi : IDisposable
{
    readonly HttpListener listener = new();

    public FakeAnthropicApi(HttpStatusCode status, string body)
    {
        Url = $"http://127.0.0.1:{FreePort()}";
        listener.Prefixes.Add(Url + "/");
        listener.Start();
        _ = Task.Run(() => ServeAsync(status, body));
    }

    public string Url { get; }

    public string? LastRequestBody { get; private set; }

    public void Dispose() => listener.Close();

    async Task ServeAsync(HttpStatusCode status, string body)
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException)
            {
                return;
            }
            using (var reader = new StreamReader(context.Request.InputStream))
                LastRequestBody = await reader.ReadToEndAsync();
            context.Response.StatusCode = (int)status;
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(Encoding.UTF8.GetBytes(body));
            context.Response.Close();
        }
    }

    static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
