using System;
using System.Linq;
using Dependinator.Utils;


namespace Dependinator.Common
{
	internal class NodeName : Equatable<NodeName>
	{
		public static NodeName Root = From("");

		private readonly Lazy<NodeName> parentName;
		private readonly Lazy<DisplayParts> displayParts;


		private NodeName(string fullName)
		{
			this.FullName = fullName;

			parentName = new Lazy<NodeName>(GetParentName);
			displayParts = new Lazy<DisplayParts>(GetDisplayParts);

			IsEqualWhenSame(fullName);
		}


		public string FullName { get; }
		public NodeName ParentName => parentName.Value;


		public string DisplayName => displayParts.Value.Name;

		public string DisplayFullName => displayParts.Value.FullName;

		public string DisplayFullNoParametersName => displayParts.Value.FullNameNoParameters;

		public string DisplayFullNameWithType => displayParts.Value.FullNameWithType; 


		public static NodeName From(string fullName)
		{
			return new NodeName(fullName);
		}


		public bool IsSame(string nameText) => nameText == FullName;

		public override string ToString() => this != Root ? FullName : "<root>";


		private NodeName GetParentName()
		{
			// Split full name in name and parent name,
			int index = FullName.LastIndexOf('.');

			return index > -1 ? From(FullName.Substring(0, index)) : Root;
		}


		private DisplayParts GetDisplayParts()
		{
			string name = null;

			string[] parts = FullName.Split(".".ToCharArray());
			
			string namePart = parts[parts.Length - 1];
			int index = namePart.IndexOf('(');
			if (index > -1)
			{
				name = namePart.Substring(0, index);
			}
			else
			{
				name = namePart;
			}

			name = ToNiceText(name);


			string fullNameWithType;

			string fullName = string.Join(".", parts
				.Where(part =>  !part.StartsWithTxt("?")));
			string fullNameNoParameters = fullName;

			if (string.IsNullOrEmpty(fullName))
			{
				fullName = ToNiceText(FullName);
				fullNameNoParameters = fullName;

				fullNameWithType = fullName;
				if (parts[parts.Length - 1].StartsWith("$"))
				{
					fullNameWithType = fullName;
				}
				else if (parts[parts.Length - 1].StartsWith("?"))
				{
					fullNameWithType = fullName;
				}
			}
			else
			{
				fullName = ToNiceText(fullName);
				fullName = ToNiceParameters(fullName);
				fullNameNoParameters = ToNoParameters(fullName);
				fullNameWithType = fullName;
			}
			
			return new DisplayParts(name, fullName, fullNameNoParameters, fullNameWithType);
		}


		private string ToNoParameters(string fullName)
		{
			int index1 = fullName.IndexOf('(');
			int index2 = fullName.IndexOf(')');

			if (index1 > -1 && index2 > index1)
			{
				fullName = $"{fullName.Substring(0, index1)}()";
			}

			return fullName;
		}


		private static string ToNiceParameters(string fullName)
		{
			int index1 = fullName.IndexOf('(');
			int index2 = fullName.IndexOf(')');

			if (index1 > -1 && index2 > (index1 + 1))
			{
				string parameters = fullName.Substring(index1 + 1, (index2 - index1) - 1);
				string[] parametersParts = parameters.Split(",".ToCharArray());

				// Simplify parameter types to just get last part of each type
				parameters = string.Join(",", parametersParts
					.Select(part => part.Split(".".ToCharArray()).Last()));

				fullName = $"{fullName.Substring(0, index1)}({parameters})";
			}

			return fullName;
		}


		public static string ToNiceText(string name)
		{
			return name.Replace("*", ".")
				.Replace("#", ".")
				.Replace("?", "")
				.Replace("$", "")
				.Replace("%", "")
				.Replace("`1", "<T>")
				.Replace("`2", "<T,T>")
				.Replace("`3", "<T,T,T>")
				.Replace("`4", "<T,T,T,T>")
				.Replace("`5", "<T,T,T,T,T>")
				.Replace("op_Equality", "==")
				.Replace("op_Inequality", "!=");
		}


		private class DisplayParts
		{
			public string Name { get; }
			public string FullName { get; }
			public string FullNameNoParameters { get; }
			public string FullNameWithType { get; }


			public DisplayParts(
				string name, 
				string fullName,
				string fullNameNoParameters,
				string fullNameWithType)
			{
				Name = name;
				FullName = fullName;
				FullNameNoParameters = fullNameNoParameters;
				FullNameWithType = fullNameWithType;
			}
		}
	}
}