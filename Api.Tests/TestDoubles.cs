using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.Tests;

sealed class TestFunctionContext : FunctionContext
{
    readonly IDictionary<object, object> items = new Dictionary<object, object>();
    readonly IInvocationFeatures features = new Mock<IInvocationFeatures>().Object;
    readonly string invocationId = Guid.NewGuid().ToString();
    IServiceProvider serviceProvider;

    public TestFunctionContext()
    {
        ServiceCollection services = [];
        services
            .AddOptions<WorkerOptions>()
            .Configure(workerOptions => workerOptions.Serializer = new JsonObjectSerializer());
        serviceProvider = services.BuildServiceProvider();
    }

    public override string InvocationId => invocationId;
    public override string FunctionId => "FunctionId";
    public override TraceContext TraceContext { get; } = new Mock<TraceContext>().Object;
    public override BindingContext BindingContext { get; } = new Mock<BindingContext>().Object;
    public override RetryContext RetryContext => null!;
    public override IServiceProvider InstanceServices
    {
        get => serviceProvider;
        set => serviceProvider = value;
    }
    public override FunctionDefinition FunctionDefinition { get; } = new Mock<FunctionDefinition>().Object;
    public override IDictionary<object, object> Items
    {
        get => items;
        set { }
    }
    public override IInvocationFeatures Features => features;
    public override CancellationToken CancellationToken => CancellationToken.None;
}

sealed class TestHttpRequestData : HttpRequestData
{
    readonly Stream body;

    public TestHttpRequestData(
        FunctionContext functionContext,
        string method = "GET",
        string? bodyText = null,
        string? url = null
    )
        : base(functionContext)
    {
        Method = method;
        Url = new Uri(url ?? "https://example.test");
        body = new MemoryStream(bodyText is null ? [] : Encoding.UTF8.GetBytes(bodyText));
        Headers = new HttpHeadersCollection();
        Cookies = [];
        Identities = [];
    }

    public override Stream Body => body;
    public override HttpHeadersCollection Headers { get; }
    public override IReadOnlyCollection<IHttpCookie> Cookies { get; }
    public override Uri Url { get; }
    public override IEnumerable<ClaimsIdentity> Identities { get; }
    public override string Method { get; }

    public override HttpResponseData CreateResponse()
    {
        return new TestHttpResponseData(FunctionContext);
    }
}

sealed class TestHttpResponseData : HttpResponseData
{
    public TestHttpResponseData(FunctionContext functionContext)
        : base(functionContext)
    {
        Headers = new HttpHeadersCollection();
        Body = new MemoryStream();
        Cookies = new TestHttpCookies();
    }

    public override HttpStatusCode StatusCode { get; set; }
    public override HttpHeadersCollection Headers { get; set; }
    public override Stream Body { get; set; }
    public override HttpCookies Cookies { get; }

    public string ReadBodyText()
    {
        Body.Seek(0, SeekOrigin.Begin);
        using StreamReader reader = new(Body, Encoding.UTF8, leaveOpen: true);
        return reader.ReadToEnd();
    }
}

sealed class TestHttpCookies : HttpCookies
{
    readonly List<IHttpCookie> cookies = [];

    public override void Append(string name, string value)
    {
        cookies.Add(new HttpCookie(name, value));
    }

    public override void Append(IHttpCookie cookie)
    {
        cookies.Add(cookie);
    }

    public override IHttpCookie CreateNew()
    {
        return new HttpCookie(string.Empty, string.Empty);
    }
}
