using System.Threading;
using System.Threading.Tasks;

namespace Dependinator.Parsing;

interface IItems
{
    Task SendAsync(IItem item, CancellationToken cancellationToken = default);
}
