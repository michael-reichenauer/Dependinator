namespace DependinatorCore.Parsing.Utils;

interface IItems
{
    Task SendAsync(Node node);
    Task SendAsync(Link link);
}
