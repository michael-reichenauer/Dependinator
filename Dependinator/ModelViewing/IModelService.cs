using Dependinator.ModelViewing.Serializing;


namespace Dependinator.ModelViewing
{
	internal interface IModelService
	{
		Model ToModel(DataModel dataModel, ModelViewData modelViewData);

		DataModel ToDataModel(Model elementTree);

		ModelViewData ToViewData(Model elementTree);
	}
}