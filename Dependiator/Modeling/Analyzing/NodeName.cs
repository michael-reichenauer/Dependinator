namespace Dependiator.Modeling.Analyzing
{
	internal class NodeName
	{
		public static string Root = "";

		public NodeName(string name, string fullName)
		{
			Name = name;
			FullName = fullName;
		}

		public string Name { get; }

		public string FullName { get; }
	

		public override string ToString() => FullName;
	}
}