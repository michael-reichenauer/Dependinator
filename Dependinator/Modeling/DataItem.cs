namespace Dependinator.Modeling
{
	internal class DataItem
	{
		public DataItem(
			DataNode node,
			DataLink link)
		{
			Node = node;
			Link = link;
		}

		public DataNode Node { get; }

		public DataLink Link { get; }


		public override string ToString() => $"{Node}{Link}";
	}
}