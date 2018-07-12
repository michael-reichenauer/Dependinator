using System.Linq;
using Dependinator.Utils.ErrorHandling;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using Mono.Cecil;
using Mono.Cecil.Cil;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.AssemblyParsing.Private
{
	internal class Decompiler
	{
		public R<string> GetCode(ModuleDefinition module, NodeName nodeName)
		{
			if (TryGetType(module, nodeName, out TypeDefinition type))
			{
				return GetDecompiledText(module, type);
			}
			else if (TryGetMember(module, nodeName, out IMemberDefinition member))
			{
				return GetDecompiledText(module, member);
			}

			return Error.From($"Failed to locate code for:\n{nodeName}");
		}


		public R<string> GetSourceFilePath(ModuleDefinition module, NodeName nodeName)
		{
			if (TryGetType(module, nodeName, out TypeDefinition type))
			{
				return GetFilePath( type);
			}
			else if (TryGetMember(module, nodeName, out IMemberDefinition member))
			{
				return GetFilePath( member);
			}

			return Error.From($"Failed to locate file path for:\n{nodeName}");
		}


		private static bool TryGetType(ModuleDefinition module, NodeName nodeName, out TypeDefinition type)
		{
			string fullName = nodeName.FullName;

			// The type starts after the module name, which is after the first '.'
			int typeIndex = fullName.IndexOf(".");

			if (typeIndex > -1)
			{
				string typeName = fullName.Substring(typeIndex + 1);
				
				type = module.GetType(typeName);
				return type != null;
			}

			type = null;
			return false;
		}


		private static bool TryGetMember(ModuleDefinition module,NodeName nodeName,out IMemberDefinition member)
		{
			string fullName = nodeName.FullName;

			// The type starts after the module name, which is after the first '.'
			int typeIndex = fullName.IndexOf(".");

			if (typeIndex > -1)
			{
				string name = fullName.Substring(typeIndex + 1);

				// Was no type, so it is a member of a type.
				int memberIndex = name.LastIndexOf('.');
				if (memberIndex > -1)
				{
					// Getting the type name after removing the member name part
					string typeName = name.Substring(0, memberIndex);

					TypeDefinition typeDefinition = module.GetType(typeName);

					if (typeDefinition != null
							&& TryGetMember(typeDefinition, fullName, out member))
					{
						return true;
					}
				}
			}

			member = null;
			return false;
		}


		private static bool TryGetMember(
			TypeDefinition typeDefinition, string fullName, out IMemberDefinition member)
		{
			if (TryGetMethod(typeDefinition, fullName, out member))
			{
				return true;
			}
			else if (TryGetProperty(typeDefinition, fullName, out member))
			{
				return true;
			}
			else if (TryGetField(typeDefinition, fullName, out member))
			{
				return true;
			}

			member = null;
			return false;
		}



		private static bool TryGetMethod(
			TypeDefinition type, string fullName, out IMemberDefinition method)
		{
			method = type.Methods.FirstOrDefault(m => Name.GetMethodFullName(m) == fullName);
			return method != null;
		}


		private static bool TryGetProperty(
			TypeDefinition type, string fullName, out IMemberDefinition property)
		{
			property = type.Properties.FirstOrDefault(m => Name.GetMemberFullName(m) == fullName);
			return property != null;
		}


		private static bool TryGetField(
			TypeDefinition type, string fullName, out IMemberDefinition field)
		{
			field = type.Fields.FirstOrDefault(m => Name.GetMemberFullName(m) == fullName);
			return field != null;
		}


		private static string GetDecompiledText(ModuleDefinition module, TypeDefinition type)
		{
			CSharpDecompiler decompiler = GetDecompiler(module);

			return decompiler.DecompileTypesAsString(new[] { type }).Replace("\t", "  ");
		}


		private static string GetDecompiledText(ModuleDefinition module, IMemberDefinition member)
		{
			CSharpDecompiler decompiler = GetDecompiler(module);

			return decompiler.DecompileAsString(member).Replace("\t", "  ");
		}


		private R<string> GetFilePath(TypeDefinition type)
		{
			foreach (MethodDefinition method in type.Methods)
			{
				SequencePoint sequencePoint = method.DebugInformation.SequencePoints.ElementAtOrDefault(0);
				if (sequencePoint != null)
				{
					return sequencePoint.Document.Url;
				}
			}

			return Error.From($"Failed to locate file path for {type.FullName}");
		}


		private R<string> GetFilePath(IMemberDefinition member)
		{
			if (member is MethodDefinition method)
			{
				SequencePoint sequencePoint = method.DebugInformation.SequencePoints.ElementAtOrDefault(0);
				if (sequencePoint != null)
				{
					return sequencePoint.Document.Url;
				}
			}

			return GetFilePath(member.DeclaringType);
		}


		private static CSharpDecompiler GetDecompiler(ModuleDefinition module) =>
			new CSharpDecompiler(module, new DecompilerSettings(LanguageVersion.Latest));
	}
}