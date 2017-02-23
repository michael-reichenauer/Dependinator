using System.Collections;
using Dependiator.Modeling.Analyzing;
using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling
{
	internal interface INodeService
	{
		Model ToModel(DataModel dataModel, ModelViewData modelViewData);

		DataModel ToDataModel(Model elementTree);

		ModelViewData ToViewData(Model elementTree);
	}
}