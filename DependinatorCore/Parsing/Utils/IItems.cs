namespace Dependinator.Core.Parsing.Utils;

interface IItems
{
    Task SendAsync(Node node);
    Task SendAsync(Link link);
}
