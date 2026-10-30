using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Dependinator.Claude.Tests;

// A local stand-in for Anthropic's API: answers every request with one status and body (or with the
// given bodies in turn, the last one again once they run out), and keeps the requests' bodies, so
// tests run the SDK's real request path without a key or network.
sealed class FakeAnthropicApi : IDisposable
{
    readonly HttpListener listener = new();

    readonly List<string> requestBodies = [];

    public FakeAnthropicApi(HttpStatusCode status, string body)
        : this(status, [body]) { }

    public FakeAnthropicApi(params string[] bodies)
        : this(HttpStatusCode.OK, bodies) { }

    FakeAnthropicApi(HttpStatusCode status, IReadOnlyList<string> bodies)
    {
        Url = $"http://127.0.0.1:{FreePort()}";
        listener.Prefixes.Add(Url + "/");
        listener.Start();
        _ = Task.Run(() => ServeAsync(status, bodies));
    }

    public string Url { get; }

    public string? LastRequestBody
    {
        get
        {
            lock (requestBodies)
                return requestBodies.LastOrDefault();
        }
    }

    public IReadOnlyList<string> RequestBodies
    {
        get
        {
            lock (requestBodies)
                return [.. requestBodies];
        }
    }

    public void Dispose() => listener.Close();

    async Task ServeAsync(HttpStatusCode status, IReadOnlyList<string> bodies)
    {
        var served = 0;
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
            {
                var requestBody = await reader.ReadToEndAsync();
                lock (requestBodies)
                    requestBodies.Add(requestBody);
            }
            var body = bodies[Math.Min(served++, bodies.Count - 1)];
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
