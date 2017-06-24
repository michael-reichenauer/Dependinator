using Dependinator.Modeling.Serializing;


namespace Dependinator.Modeling.Analyzing
{
	internal interface IReflectionService
	{
		DataModel Analyze(string path);
	}
}