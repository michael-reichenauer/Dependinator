namespace Dependinator.Modeling
{
	internal interface IModelingService
	{
		void Analyze(string path);

		void Serialize(ModelOld model, string path);

		bool TryDeserialize(string path, out ModelOld model);
	}
}