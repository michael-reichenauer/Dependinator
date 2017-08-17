using System.Threading.Tasks;

namespace Dependinator.Modeling.Private.Analyzing
{
	internal interface IReflectionService
	{
		Task AnalyzeAsync(string path);
	}
}