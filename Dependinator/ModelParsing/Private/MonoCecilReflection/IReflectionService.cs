using System.Threading.Tasks;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection
{
	internal interface IReflectionService
	{
		Task ParseAsync(string assemblyPath, ModelItemsCallback modelItemsCallback);
	}
}