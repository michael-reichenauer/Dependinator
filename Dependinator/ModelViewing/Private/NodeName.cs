using System;
using System.Collections.Generic;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private
{
	internal class NodeName : Equatable<NodeName>
	{
		private static readonly Dictionary<string, NodeName> Names = new Dictionary<string, NodeName>();
		public static NodeName Root = From("");

		public static int count = 0;

		private readonly string fullName;
		private readonly Lazy<string> name;
		private readonly Lazy<string> displayName;
		private readonly Lazy<string> shortName;
		private readonly Lazy<NodeName> parentName;


		private NodeName(string fullName)
		{
			this.fullName = fullName;
			name = new Lazy<string>(GetName);
			displayName = new Lazy<string>(GetDisplayName);
			shortName = new Lazy<string>(GetShortName);
			parentName = new Lazy<NodeName>(GetParentName);
			IsEqualWhenSame(fullName);
		}


		public static NodeName From(string fullName)
		{
			//return new NodeName(fullName);
			if (!Names.TryGetValue(fullName, out NodeName name))
			{
				name = new NodeName(fullName);
				Names[fullName] = name;
			}
			else
			{
				count++;
			}

			return name;
		}


		public string Name => name.Value;
		public string DisplayName => displayName.Value;
		public string ShortName => shortName.Value;
		public NodeName ParentName => parentName.Value;

		public string AsString() => fullName;


		public override string ToString() => this != Root ? fullName : "<root>";


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

			text = text.Replace("*", ".");

			return text;
		}


		private string GetDisplayName() => Name.Replace("?", "").Replace("$", "");


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
			
			return From(text);
		}
	}

	internal class NodePath : Equatable<NodePath>
	{
		private static readonly Dictionary<string, NodePath> Names = new Dictionary<string, NodePath>();
		public static NodePath Root = From("");


		public static int count = 0;

		private readonly string fullName;
		private readonly Lazy<string> name;
		private readonly Lazy<NodePath> parentName;


		private NodePath(string fullName)
		{
			this.fullName = fullName;
			name = new Lazy<string>(GetName);

			parentName = new Lazy<NodePath>(GetParentName);
			IsEqualWhenSame(fullName);
		}

		public static NodePath From(string fullName)
		{
			//return new NodeName(fullName);
			if (!Names.TryGetValue(fullName, out NodePath name))
			{
				name = new NodePath(fullName);
				Names[fullName] = name;
			}
			else
			{
				count++;
			}

			return name;
		}



		public string Name => name.Value;

		public NodePath ParentName => parentName.Value;


		public override string ToString() => this != Root ? fullName : "<root>";


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

			text = text.Replace("*", ".");

			return text;
		}



		private NodePath GetParentName()
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

			return From(text);
		}
	}

}