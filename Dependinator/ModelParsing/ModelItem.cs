namespace Dependinator.ModelParsing
{
	internal class ModelItem
	{
		public ModelItem(
			ModelNode node,
			ModelLink link)
		{
			Node = node;
			Link = link;
		}

		public ModelNode Node { get; }

		public ModelLink Link { get; }


		public override string ToString() => $"{Node}{Link}";
	}
}