namespace DependinatorLib.Diagrams.Elements;

interface IElement
{
    public string Svg { get; }
    void Update() { }
}
