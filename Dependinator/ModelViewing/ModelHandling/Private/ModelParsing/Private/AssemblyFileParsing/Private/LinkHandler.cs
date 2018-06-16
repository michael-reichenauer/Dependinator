using System;
using System.Collections.Generic;
using System.Linq;
using Dependinator.ModelViewing.ModelDataHandling;
using Dependinator.ModelViewing.ModelHandling.Core;
using Mono.Cecil;


namespace Dependinator.ModelViewing.ModelHandling.Private.ModelParsing.Private.AssemblyFileParsing.Private
{
	internal class LinkHandler
	{
		private readonly ModelItemsCallback itemsCallback;

		private readonly Dictionary<string, ModelNode> sentTargetNodes = new Dictionary<string, ModelNode>();

		public LinkHandler(ModelItemsCallback itemsCallback)
		{
			this.itemsCallback = itemsCallback;
		}


		public void AddLink(NodeId sourceId, string targetName, NodeType targetType)
		{
			SendLink(sourceId, targetName, targetType);
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

			SendLink(sourceNode.Id, targetNodeName, NodeType.Type);

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

			SendLink(sourceNode.Id, targetNodeName, NodeType.Member);
		}


		private void SendLink(NodeId sourceId, string targetName, NodeType targetType)
		{
			if (!sentTargetNodes.TryGetValue(targetName, out ModelNode targetNode))
			{
				NodeName targetNodeName = NodeName.From(targetName);
				NodeId targetId = new NodeId(targetNodeName);
				
				targetNode = new ModelNode(targetId, targetNodeName, null, targetType, null, null, true);
				sentTargetNodes[targetName] = targetNode;
			}

			if (sourceId == targetNode.Id)
			{
				// Skipping link to self
				return;
			}
			
			ModelLink modelLink = new ModelLink(sourceId, targetNode.Id);

			itemsCallback(targetNode);
			itemsCallback(modelLink);
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
		/// Return true if type is a generic type parameter T, as in e.g. Get'T'(T value)
		/// </summary>
		private static bool IsGenericTypeArgument(MemberReference targetType)
		{
			return
				targetType.FullName == null
				&& targetType.DeclaringType == null;
		}
	}
}