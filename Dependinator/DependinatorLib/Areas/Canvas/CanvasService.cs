namespace DependinatorLib.Areas.Canvas;


public interface ICanvasService
{
    string Get();
}


public class CanvasService : ICanvasService
{
    public string Get()
    {
        return """
            <svg xmlns="http://www.w3.org/2000/svg" width="300" height="800" viewBox="0 0 300 800">

                <circle cx="85" cy="100" r="15" fill="#00aade" stroke="#fff" />
                <circle cx="115" cy="150" r="15" fill="#00aa00" stroke="#fff" />

                <path d="M100 100 C 170 50, 230 250, 300 250" stroke="rgb(108, 117, 125)" stroke-width="1.5" fill="transparent" style="pointer-events:none !important;"/>
            </svg>
         """;
    }
}
