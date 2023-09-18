namespace DependinatorLib.Areas.Canvas;


public interface ICanvasService
{
    string Get();
}


[Singleton]
public class CanvasService : ICanvasService
{
    public string Get()
    {
        return """
            <svg xmlns="http://www.w3.org/2000/svg" width="300" height="800" viewBox="0 0 300 800">

                <circle cx="80" cy="100" r="20" fill="#00aade" stroke="#fff" />
                <circle cx="180" cy="180" r="20" fill="#00aa00" stroke="#fff" />

                <path d="M100 100 160 180" stroke="rgb(108, 117, 125)" stroke-width="1.5" fill="transparent" style="pointer-events:none !important;"/>
            </svg>
         """;
    }
}
