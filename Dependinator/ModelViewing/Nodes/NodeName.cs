using System;
using Dependinator.Utils;

namespace Dependinator.ModelViewing.Nodes
{
	internal class NodeName : Equatable<NodeName>
	{
		public static NodeName Root = new NodeName("");

		private readonly string fullName;
		private readonly Lazy<string> name;
		private readonly Lazy<string> shortName;
		private readonly Lazy<NodeName> parentName;


		public NodeName(string fullName)
		{
			this.fullName = fullName;
			name = new Lazy<string>(GetName);
			shortName = new Lazy<string>(GetShortName);
			parentName = new Lazy<NodeName>(GetParentName);
			IsEqualWhenSame(fullName);
		}

		public string Name => name.Value;
		public string ShortName => shortName.Value;
		public NodeName ParentName => parentName.Value;

		public override string ToString() => this != Root ? fullName : "<root>";

		public string AsString() => fullName;



		private string GetName()
		{
			string text = fullName;

			// Skipping parameters (if method)
			int index = text.IndexOf('(');
			if (index > 0)
			{
				text = text.Substring(0, index);
			}

			// Getting last name part
			index = text.LastIndexOf('.');
			if (index > -1)
			{
				text = text.Substring(index + 1);
			}

			return text;
		}


		private string GetShortName() =>
			string.IsNullOrEmpty(ParentName.Name) ? Name : $"{ParentName.Name}.{Name}";


		private NodeName GetParentName()
		{
			string text = fullName;

			// Skipping parameters (if method)
			int index = text.IndexOf('(');
			if (index > 0)
			{
				text = text.Substring(0, index);
			}

			// Getting last name part
			index = text.LastIndexOf('.');
			if (index > -1)
			{
				text = text.Substring(0, index);
			}
			else
			{
				return Root;
			}
			
			return new NodeName(text);
		}
	}
}