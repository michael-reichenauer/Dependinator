using Dependinator.Utils;

namespace Dependinator.ModelViewing.Private
{

	[SingleInstance]
	internal class Model
	{
		public Nodes Nodes { get; } = new Nodes();

		public Links Links { get; } = new Links();


	}
}