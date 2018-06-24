using System.Threading.Tasks;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.DataHandling
{
	internal interface IDataDetailsService
	{
		Task<R<string>> GetCode(string filePath, NodeName nodeName);
	}
}