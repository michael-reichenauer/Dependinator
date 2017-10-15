using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private
{
	internal class NodeName : Equatable<NodeName>
	{
		private static readonly Dictionary<string, NodeName> Names = new Dictionary<string, NodeName>();
		public static NodeName Root = From("");

		private readonly Lazy<string> displayName;
		private readonly Lazy<string> displayFullName;
		private readonly Lazy<string> shortName;


		private NodeName(string fullName)
		{
			this.FullName = fullName;
			SetName();

			displayName = new Lazy<string>(GetDisplayName);
			displayFullName = new Lazy<string>(GetDisplayFullName);
			shortName = new Lazy<string>(GetShortName);

			IsEqualWhenSame(fullName);
		}


		public string FullName { get; }
		private string Name { get; set; }
		public NodeName ParentName { get; private set; }
		public string DisplayName => displayName.Value;
		public string DisplayFullName => displayFullName.Value;
		public string ShortName => shortName.Value;


		public static NodeName From(string fullName)
		{
			//return new NodeName(fullName);
			if (!Names.TryGetValue(fullName, out NodeName name))
			{
				name = new NodeName(fullName);
				Names[fullName] = name;
			}

			return name;
		}


		public override string ToString() => this != Root ? FullName : "<root>";


		private void SetName()
		{
			// Split full name in name and parent name,
			int index = FullName.LastIndexOf('.');
			if (index > -1)
			{
				Name = FullName.Substring(index + 1);
				ParentName = From(FullName.Substring(0, index));
			}
			else
			{
				Name = FullName;
				ParentName = Root;
			}
		}


		private string GetDisplayName()
		{
			string name = Name;
			int index = Name.IndexOf('(');
			if (index > -1)
			{
				name = Name.Substring(0, index);
			}

			return name.Replace("*", ".").Replace("$", "").Replace("?", "");
		}
		

		private string GetDisplayFullName()
		{
			string[] parts = FullName.Split(".".ToCharArray());

			string name = string.Join(".", parts
				.Where(part => !part.StartsWithTxt("$") && !part.StartsWithTxt("?")));

			name = name.Replace("*", ".").Replace("#", ".").Replace("?", "");

			if (string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(DisplayName))
			{
				name = DisplayName;
			}

			return name;
		}


		private string GetShortName() =>
			ParentName == Root ? DisplayName : $"{ParentName.DisplayName}.{DisplayName}";
	}
}