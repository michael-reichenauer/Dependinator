using System.Collections;
using Dependiator.Modeling.Analyzing;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling
{
	internal interface INodeService
	{
		Model ToModel(Data.Model dataModel, ModelViewData modelViewData);

		Data.Model ToData(Model elementTree);

		ModelViewData ToViewData(Model elementTree);
	}
}