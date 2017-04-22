using Dependiator.Modeling.Serializing;


namespace Dependiator.Modeling.Nodes
{
	internal interface INodeService
	{
		Model ToModel(DataModel dataModel, ModelViewData modelViewData);

		DataModel ToDataModel(Model elementTree);

		ModelViewData ToViewData(Model elementTree);
	}
}