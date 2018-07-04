using System.Collections.Concurrent;
using System.Linq;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils.ErrorHandling;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using Mono.Cecil;


namespace Dependinator.ModelViewing.DataHandling.Private.Parsing.Private.AssemblyParsing.Private
{
	internal class Decompiler
	{
		private readonly ConcurrentDictionary<ModuleDefinition, CSharpDecompiler> decompilers =
			new ConcurrentDictionary<ModuleDefinition, CSharpDecompiler>();


		public R<string> GetCode(ModuleDefinition moduleDefinition, NodeName nodeName)
		{
			string fullName = nodeName.FullName;

			// The type starts after the module name, which is after the first '.'
			int typeIndex = fullName.IndexOf(".");

			if (typeIndex > -1)
			{
				string typeName = fullName.Substring(typeIndex + 1);

				TypeDefinition typeDefinition = moduleDefinition.GetType(typeName);

				if (typeDefinition != null)
				{
					// Found the specified type
					return GetDecompiledText(moduleDefinition, typeDefinition);
				}

				// Was no type, so it is a member of a type.
				int memberIndex = typeName.LastIndexOf('.');
				if (memberIndex > -1)
				{
					// Getting the type name after removing the member name part
					typeName = typeName.Substring(0, memberIndex);

					typeDefinition = moduleDefinition.GetType(typeName);

					if (typeDefinition != null
							&& TryGetMemberCode(moduleDefinition, typeDefinition, fullName, out string code))
					{
						return code;
					}
				}
			}

			return Error.From($"Failed to locate code for:\n{nodeName}");
		}


		private bool TryGetMemberCode(
			ModuleDefinition moduleDefinition,
			TypeDefinition typeDefinition,
			string fullName,
			out string code)
		{
			if (TryGetMethod(typeDefinition, fullName, out MethodDefinition method))
			{
				code = GetDecompiledText(moduleDefinition, method);
				return true;
			}
			else if (TryGetProperty(typeDefinition, fullName, out PropertyDefinition property))
			{
				code = GetDecompiledText(moduleDefinition, property);
				return true;
			}
			else if (TryGetField(typeDefinition, fullName, out FieldDefinition field))
			{
				code = GetDecompiledText(moduleDefinition, field);
				return true;
			}

			code = null;
			return false;
		}


		private static bool TryGetMethod(
			TypeDefinition type, string fullName, out MethodDefinition method)
		{
			method = type.Methods.FirstOrDefault(m => Name.GetMethodFullName(m) == fullName);
			return method != null;
		}

		private static bool TryGetProperty(
			TypeDefinition type, string fullName, out PropertyDefinition property)
		{
			property = type.Properties.FirstOrDefault(m => Name.GetMemberFullName(m) == fullName);
			return property != null;
		}

		private static bool TryGetField(
			TypeDefinition type, string fullName, out FieldDefinition field)
		{
			field = type.Fields.FirstOrDefault(m => Name.GetMemberFullName(m) == fullName);
			return field != null;
		}


		public string GetDecompiledText(ModuleDefinition moduleDefinition, TypeDefinition type)
		{
			CSharpDecompiler decompiler = GetDecompiler(moduleDefinition);

			return decompiler.DecompileTypesAsString(new[] { type }).Replace("\t", "  ");
		}


		public string GetDecompiledText(ModuleDefinition moduleDefinition, IMemberDefinition member)
		{
			CSharpDecompiler decompiler = GetDecompiler(moduleDefinition);

			return decompiler.DecompileAsString(member).Replace("\t", "  ");
		}


		private CSharpDecompiler GetDecompiler(ModuleDefinition module)
		{
			return decompilers.GetOrAdd(
				module,
				key => new CSharpDecompiler(key, new DecompilerSettings(LanguageVersion.Latest)));
		}
	}
}