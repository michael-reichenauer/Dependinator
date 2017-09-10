using System.Threading.Tasks;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection
{
	internal interface IReflectionService
	{
		Task AnalyzeAsync(string path, ModelItemsCallback modelItemsCallback);
	}
}