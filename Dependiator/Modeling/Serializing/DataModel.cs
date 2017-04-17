using System.Collections.Generic;


namespace Dependiator.Modeling.Serializing
{
	internal class DataModel
	{
		public List<Data.Node> Nodes { get; set; } = new List<Data.Node>();

		public List<Data.Link> Links { get; set; } = new List<Data.Link>();


		public DataModel AddType(string name)
		{
			Nodes.Add(new Data.Node { Name = name, Type = "Type" });
			return this;
		}

		public DataModel AddMember(string name)
		{
			Nodes.Add(new Data.Node {Name = name, Type = "Member"});
			return this;
		}

		public DataModel AddLink(string source, string target)
		{
			Links.Add(new Data.Link { Source = source, Target = target });
			return this;
		}


		public override string ToString() => $"{Nodes.Count} nodes, {Links.Count} links.";
	}
}