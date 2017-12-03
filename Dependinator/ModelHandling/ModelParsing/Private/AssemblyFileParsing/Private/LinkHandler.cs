using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelHandling.Core;
using Mono.Cecil;
//using Dependinator.ModelHandling.ModelPersistence.Private.Serializing;


namespace Dependinator.ModelHandling.ModelParsing.Private.AssemblyFileParsing.Private
{
	internal class LinkHandler
	{
		private readonly Sender sender;
		//private readonly List<Reference> links = new List<Reference>();


		public LinkHandler(Sender sender)
		{
			this.sender = sender;
		}


		//public void SendAllLinks()
		//{
		//	links.ForEach(SendReference);
		//}


		public void AddLinkToReference(Reference reference)
		{
			SendReference(reference);
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

			SendReference(new Reference(sourceNode.Name, targetNodeName, NodeType.Type));

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

			SendReference(new Reference(sourceNode.Name, targetNodeName, NodeType.Member));
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
			if (reference.SourceName == reference.TargetName)
			{
				// Skipping link to self
				return;
			}

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