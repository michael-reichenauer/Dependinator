using System.Threading.Tasks;
using Dependinator.ModelViewing.DataHandling.Dtos;


namespace Dependinator.ModelViewing.Nodes
{
	internal interface ILocateService
	{
		void TryLocateNode(NodeId nodeId);
	}
}