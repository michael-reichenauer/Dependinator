using System.Threading.Tasks;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling
{
	internal interface IDataDetailsService
	{
		Task<R<string>> GetCode(string filePath, NodeName nodeName);
		Task<R<string>> GetSourceFilePath(string filePath, NodeName nodeName);
	}
}