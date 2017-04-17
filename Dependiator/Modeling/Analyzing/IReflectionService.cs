using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling.Analyzing
{
	internal interface IReflectionService
	{
		DataModel Analyze(string path);
	}
}