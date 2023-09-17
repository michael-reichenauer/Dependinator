using Microsoft.AspNetCore.Components;

namespace DependinatorLib.Areas.Canvas;

public partial class Canvas : ComponentBase
{
    public string Data => """
        <svg xmlns="http://www.w3.org/2000/svg" width="300" height="800" viewBox="0 0 300 800">

            <circle cx="85" cy="100" r="15" fill="#00aade" stroke="#fff" />
            <circle cx="115" cy="150" r="15" fill="#00aa00" stroke="#fff" />

            <Connector X1=100 Y1=100 Dir1=Connector.Direction.Right X2=300 Y2=250 Dir2=Connector.Direction.Left />
        </svg>
    """;
}