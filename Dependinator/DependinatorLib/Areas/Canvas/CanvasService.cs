using Microsoft.AspNetCore.Components.Web;

namespace DependinatorLib.Areas.Canvas;


public interface ICanvasService
{
    string Get();

    void MouseMove(object obj, MouseEventArgs evt);
    void MouseUp(object obj, MouseEventArgs evt);
}


[Singleton]
public class CanvasService : ICanvasService
{
    public string Get()
    {
        Log.Debug("Get");

        return """
                <circle cx="80" cy="100" r="20" fill="#00aade" stroke="#fff" />
                <circle cx="180" cy="180" r="20" fill="#00aa00" stroke="#fff" />

                <path d="M100 100 160 180" stroke="rgb(108, 117, 125)" stroke-width="1.5" fill="transparent" style="pointer-events:none !important;"/>
         """;
    }

    public void MouseMove(object obj, MouseEventArgs e)
    {
        Log.Info($"FireMove {e.Type}: {e.OffsetX},{e.OffsetY}");
        // OnMove?.Invoke(obj, e);
    }

    public void MouseUp(object obj, MouseEventArgs e)
    {
        Log.Info($"FireUp {e.Type}: {e.OffsetX},{e.OffsetY}");
        // OnUp?.Invoke(obj, e);
    }
}
