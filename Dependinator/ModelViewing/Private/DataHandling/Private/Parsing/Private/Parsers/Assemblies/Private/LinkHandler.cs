using System;
using System.Linq;
using Mono.Cecil;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers.Assemblies.Private
{
    internal class LinkHandler
    {
        private readonly Action<LinkData> linkCallback;


        public LinkHandler(Action<LinkData> linkCallback)
        {
            this.linkCallback = linkCallback;
        }


        public int LinksCount { get; private set; } = 0;


        public void AddLink(string source, string target, string targetType)
        {
            SendLink(source, target, targetType);
        }


        public void AddLinkToType(NodeData sourceNode, TypeReference targetType)
        {
            if (targetType is GenericInstanceType genericType)
            {
                genericType.GenericArguments.ForEach(argType => AddLinkToType(sourceNode, argType));
            }

            if (IsIgnoredReference(targetType))
            {
                return;
            }

            string targetNodeName = Name.GetTypeFullName(targetType);

            if (IsIgnoredTargetName(targetNodeName))
            {
                return;
            }

            SendLink(sourceNode.Name, targetNodeName, NodeData.TypeType);
        }


        public void AddLinkToMember(NodeData sourceNode, IMemberDefinition memberInfo)
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

            SendLink(sourceNode.Name, targetNodeName, NodeData.MemberType);
        }


        private void SendLink(string source, string targetName, string targetType)
        {
            LinkData dataLink = new LinkData(source, targetName, targetType);
            linkCallback(dataLink);
            LinksCount++;
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
                   || targetType is ByReferenceType refType && refType.ElementType.IsGenericParameter;
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
