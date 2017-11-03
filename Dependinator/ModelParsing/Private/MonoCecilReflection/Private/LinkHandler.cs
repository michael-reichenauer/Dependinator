using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;


namespace Dependinator.ModelParsing.Private.MonoCecilReflection.Private
{
	internal class LinkHandler
	{
		private readonly Sender sender;
		private readonly List<Reference> links = new List<Reference>();


		public LinkHandler(Sender sender)
		{
			this.sender = sender;
		}


		public void SendAllLinks()
		{
			links.ForEach(SendReference);
		}


		public void AddLinkToReference(Reference reference)
		{
			links.Add(reference);
		}


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

			links.Add(new Reference(sourceNode.Name, targetNodeName, JsonTypes.NodeType.Type));

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

			links.Add(new Reference(sourceNode.Name, targetNodeName, JsonTypes.NodeType.Member));
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


		private void SendReference(Reference reference)
		{
			sender.SendLink(reference.SourceName, reference.TargetName, reference.TargetType);
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