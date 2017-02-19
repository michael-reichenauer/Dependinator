namespace Dependiator.Modeling.Analyzing
{
	internal class ElementTree
	{
		public ElementTree(Node root)
		{
			Root = root;
		}

		public Node Root { get; }
	}
}