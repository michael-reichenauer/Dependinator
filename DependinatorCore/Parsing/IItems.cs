using System.Threading;
using System.Threading.Tasks;

namespace DependinatorCore.Parsing;

interface IItems
{
    Task SendAsync(IItem item);
}
