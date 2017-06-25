using Dependinator.ModelViewing.Modeling.Serializing;

namespace Dependinator.ModelViewing.Modeling
{
	internal interface IModelService
	{
		Model ToModel(DataModel dataModel, ModelViewData modelViewData);

		DataModel ToDataModel(Model elementTree);

		ModelViewData ToViewData(Model elementTree);

		void Serialize(DataModel data, string path);

		bool TryDeserialize(string path, out DataModel data);
	}
}