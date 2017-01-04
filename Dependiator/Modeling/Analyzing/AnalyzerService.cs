using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;


namespace Dependiator.Modeling.Analyzing
{
	internal class AnalyzerService : IAnalyzerService
	{
		internal const BindingFlags DeclaredOnlyFlags =
			BindingFlags.Public | BindingFlags.NonPublic
			| BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

		private static readonly char[] DotSeparator = ".".ToCharArray();


		public ElementTree Analyze()
		{
			ElementTree tree = new ElementTree();
			string location = Assembly.GetExecutingAssembly().Location;

			Assembly assembly = Assembly.ReflectionOnlyLoadFrom(location);

			IEnumerable<TypeInfo> typeInfos = assembly.DefinedTypes.ToList();

			Dictionary<string, NameSpaceElement> nameSpaces = new Dictionary<string, NameSpaceElement>();
			nameSpaces[tree.Root.FullName] = tree.Root;

			AddTypes(typeInfos, nameSpaces);

			return tree;
		}


		private void AddTypes(
			IEnumerable<TypeInfo> typeInfos, 
			Dictionary<string, NameSpaceElement> nameSpaces)
		{
			foreach (TypeInfo typeInfo in typeInfos)
			{
				string nameSpaceFullName = typeInfo.Namespace;

				if (!nameSpaces.TryGetValue(nameSpaceFullName, out NameSpaceElement nameSpace))
				{
					nameSpace = CreateNameSpaceElement(nameSpaceFullName, nameSpaces);
				}

				if (typeInfo.Name.IndexOf("<") == -1)
				{
					TypeElement type = new TypeElement(typeInfo.Name, typeInfo.FullName);
					nameSpace.AddChild(type);

					AddMembers(typeInfo, type);
				}
				
			}
		}


		private void AddMembers(TypeInfo typeInfo, TypeElement type)
		{
			foreach (MemberInfo memberInfo in typeInfo.GetMembers(DeclaredOnlyFlags))
			{
				string name = memberInfo.Name;

					string fullName = memberInfo.DeclaringType != null 
					? memberInfo.DeclaringType.FullName + "."  + name
					: name;

					MemberElement member= new MemberElement(name, fullName);
					type.AddChild(member);			
			}
		}


		private NameSpaceElement CreateNameSpaceElement(
			string nameSpaceFullName, 
			Dictionary<string, NameSpaceElement> nameSpaces)
		{
			IEnumerable<string> nameSpaceNameFullNames = GetNameSpaceNameFullNames(nameSpaceFullName);

			NameSpaceElement baseNameSpace = nameSpaces[""];

			foreach (string fullName in nameSpaceNameFullNames)
			{
				if (!nameSpaces.TryGetValue(fullName, out NameSpaceElement nameSpace))
				{
					string name = GetNameSpaceNamePart(fullName);
					nameSpace = new NameSpaceElement(name, fullName);
					baseNameSpace.AddChild(nameSpace);
					nameSpaces[fullName] = nameSpace;
				}

				baseNameSpace = nameSpace;
			}

			return baseNameSpace;
		}


		private IEnumerable<string> GetNameSpaceNameFullNames(string nameSpaceFullName)
		{
			List<string> fullNames = new List<string>();

			// Add global root namespace
			string fullName = "";
			fullNames.Add(fullName);

			string[] parts = nameSpaceFullName.Split(DotSeparator);

			foreach (string part in parts)
			{
				fullName += fullName == "" ? part : "." + part;
				fullNames.Add(fullName);
			}

			return fullNames;
		}

		private string GetNameSpaceNamePart(string nameSpaceFullName)
		{
			int index = nameSpaceFullName.LastIndexOf('.');

			if (index == -1)
			{
				// root namespace
				return nameSpaceFullName;
			}

			return nameSpaceFullName.Substring(index + 1);
		}
	}


	internal class ElementTree
	{	 
		public NameSpaceElement Root { get;  } = new NameSpaceElement("", "");

		public void AddChild(Element child)
		{
			Root.AddChild(child);
		}


		public void AddChildren(IEnumerable<Element> children)
		{
			Root.AddChildren(children);
		}
	}


	internal class Element
	{
		private readonly List<Element> childElements = new List<Element>();

		public string Name { get; }
		public string FullName { get; }


		public Element(string name, string fullName)
		{
			Name = name;
			FullName = fullName;
		}


		public IEnumerable<Element> ChildElements => childElements;


		public void AddChild(Element child)
		{
			childElements.Add(child);
		}


		public void AddChildren(IEnumerable<Element> children)
		{
			childElements.AddRange(children);
		}


		public override string ToString() => FullName;	
	}



	internal class NameSpaceElement : Element
	{
		public NameSpaceElement(string name, string fullName)
			: base(name, fullName)
		{
		}


		public IEnumerable<NameSpaceElement> NameSpaces => ChildElements.OfType<NameSpaceElement>();
		public IEnumerable<TypeElement> TypeItems => ChildElements.OfType<TypeElement>();
	}


	internal class TypeElement : Element
	{
		public TypeElement(string name, string fullName)
			: base(name, fullName)
		{
		}

		public IEnumerable<MemberElement> TypeItems => ChildElements.OfType<MemberElement>();
	}


	internal class MemberElement : Element
	{
		public MemberElement(string name, string fullName)
			: base(name, fullName)
		{
		}
	}
}