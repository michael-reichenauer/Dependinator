using System.Threading.Tasks;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection
{
	internal interface IReflectionService
	{
		Task AnalyzeAsync(string assemblyPath, ModelItemsCallback modelItemsCallback);
	}
}