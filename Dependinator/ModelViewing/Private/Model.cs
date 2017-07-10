using Dependinator.Modeling;

namespace Dependinator.ModelViewing.Private
{
	internal class Model
	{
		public Nodes Nodes { get; } = new Nodes();

		public Links Links { get; } = new Links();
	}
}