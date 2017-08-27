using System.Threading.Tasks;

namespace Dependinator.ModelParsing.Private.Analyzing
{
	internal interface IReflectionService
	{
		Task AnalyzeAsync(string path, ItemsCallback itemsCallback);
	}
}