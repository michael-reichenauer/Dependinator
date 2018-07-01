using System;
using System.Security.Cryptography;
using System.Text;
using Dependinator.ModelViewing.Nodes;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.DataHandling.Dtos
{
	internal class NodeId : Equatable<NodeId>
	{
		private static readonly SHA256 Hash = SHA256.Create();

		private readonly string id;

		public NodeId(NodeName nodeName)
		{
			this.id = ToUniqueId(nodeName.FullName);
			IsEqualWhenSame(id);
		}

		private NodeId(string id)
		{
			this.id = id;
			IsEqualWhenSame(id);
		}


		public static NodeId Root { get; } = new NodeId(NodeName.Root);


		public string AsString() => id;

		public static NodeId From(string id) => new NodeId(id);


		private static string ToUniqueId(string text)
		{
			byte[] dataBytes = Encoding.UTF8.GetBytes(text);
			byte[] hashBytes = ComputeHash(dataBytes);
			string base64String = Convert.ToBase64String(hashBytes, 0, 16);

			return base64String.Substring(0, 22);
		}


		private static byte[] ComputeHash(byte[] dataBytes)
		{
			lock (Hash)
			{
				return Hash.ComputeHash(dataBytes);
			}
		}
	}
}