namespace DependinatorCore.Parsing;

interface IItems
{
    Task SendAsync(Node node);
    Task SendAsync(Link link);
}
