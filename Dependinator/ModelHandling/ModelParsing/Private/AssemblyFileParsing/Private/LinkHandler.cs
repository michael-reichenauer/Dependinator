using System;
using System.Linq;
using Dependinator.ModelHandling.Core;
using Mono.Cecil;



namespace Dependinator.ModelHandling.ModelParsing.Private.AssemblyFileParsing.Private
{
	internal class LinkHandler
	{
		private readonly ModelItemsCallback itemsCallback;


		public LinkHandler(ModelItemsCallback itemsCallback)
		{
			this.itemsCallback = itemsCallback;
		}


		public void AddLink(ModelLink link) => SendLink(link);


		public void AddLinkToType(ModelNode sourceNode, TypeReference targetType)
		{
			if (IsIgnoredReference(targetType))
			{
				return;
			}

			string targetNodeName = Name.GetTypeFullName(targetType);

			if (IsIgnoredTargetName(targetNodeName))
			{
				return;
			}

			SendLink(new ModelLink(sourceNode.Name, targetNodeName, NodeType.Type));

			if (targetType.IsGenericInstance)
			{
				targetType.GenericParameters
					.ForEach(argType => AddLinkToType(sourceNode, argType));
			}
		}


		public void AddLinkToMember(ModelNode sourceNode, IMemberDefinition memberInfo)
		{
			if (IsIgnoredTargetMember(memberInfo))
			{
				return;
			}

			string targetNodeName = Name.GetMemberFullName(memberInfo);

			if (IsIgnoredTargetName(targetNodeName))
			{
				return;
			}

			SendLink(new ModelLink(sourceNode.Name, targetNodeName, NodeType.Member));
		}


		private void SendLink(ModelLink link)
		{
			if (link.Source == link.Target)
			{
				// Skipping link to self
				return;
			}

			itemsCallback(link);
		}


		private static bool IsIgnoredTargetMember(IMemberDefinition memberInfo)
		{
			return IgnoredTypes.IsIgnoredSystemType(memberInfo.DeclaringType)
						 || IsGenericTypeArgument(memberInfo.DeclaringType);
		}


		private static bool IsIgnoredTargetName(string targetNodeName)
		{
			return Name.IsCompilerGenerated(targetNodeName) ||
						 targetNodeName.StartsWithTxt("mscorlib.");
		}


		private static bool IsIgnoredReference(TypeReference targetType)
		{
			return targetType.FullName == "System.Void"
						 || targetType.IsGenericParameter
						 || IgnoredTypes.IsIgnoredSystemType(targetType)
						 || IsGenericTypeArgument(targetType)
						 || (targetType is ByReferenceType refType && refType.ElementType.IsGenericParameter);
		}


		/// <summary>
		/// Return true if type is a generic type parameter T, as in e.g. Get/T/ (T value)
		/// </summary>
		private static bool IsGenericTypeArgument(TypeReference targetType)
		{
			return
				targetType.FullName == null
				&& targetType.DeclaringType == null;
		}
	}
}