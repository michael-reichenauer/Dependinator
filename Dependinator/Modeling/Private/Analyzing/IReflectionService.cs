using Dependinator.Modeling.Private.Serializing;

namespace Dependinator.Modeling.Private.Analyzing
{
	internal interface IReflectionService
	{
		Data.Model Analyze(string path);
	}
}