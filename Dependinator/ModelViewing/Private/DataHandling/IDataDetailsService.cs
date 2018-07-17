using System.Threading.Tasks;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling
{
	internal interface IDataDetailsService
	{
		Task<R<string>> GetCodeAsync(string filePath, NodeName nodeName);
		Task<R<string>> GetSourceFilePathAsync(string filePath, NodeName nodeName);
		Task<R<NodeName>> GetNodeForFilePathAsync(string filePath, string sourceFilePath);

	}
}