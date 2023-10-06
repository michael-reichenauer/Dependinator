using Microsoft.JSInterop;

namespace DependinatorLib.Utils
{
    public interface IJsInterop
    {
        ValueTask<string> Prompt(string message);
        ValueTask<DOMRect> GetBoundingRectangle(string elementId);
    }

    [Transient]
    public class JsInterop : IJsInterop, IAsyncDisposable
    {
        private readonly Lazy<Task<IJSObjectReference>> moduleTask;

        public JsInterop(IJSRuntime jsRuntime)
        {
            moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/DependinatorLib/jsInterop.js").AsTask());
        }

        public async ValueTask<string> Prompt(string message)
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<string>("showPrompt", message);
        }

        public async ValueTask<DOMRect> GetBoundingRectangle(string elementId)
        {
            var module = await moduleTask.Value;
            return await module.InvokeAsync<DOMRect>("getBoundingRectangle", elementId);
        }

        public async ValueTask DisposeAsync()
        {
            if (moduleTask.IsValueCreated)
            {
                var module = await moduleTask.Value;
                await module.DisposeAsync();
            }
        }
    }

    public class DOMRect
    {
        public double Top { get; set; }
        public double Left { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }
}