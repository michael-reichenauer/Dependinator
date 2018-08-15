using System;
using System.Security.Cryptography;
using System.Text;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private.DataHandling.Dtos
{
    internal class DataNodeName : Equatable<DataNodeName>
    {
        public static readonly DataNodeName None = new DataNodeName("");
        private static readonly SHA256 Hash = SHA256.Create();
        private readonly string fullName;


        private DataNodeName(string fullName)
        {
            this.fullName = fullName;

            IsEqualWhenSame(fullName);
        }


        public static explicit operator DataNodeName(string fullName) => new DataNodeName(fullName);
        public static explicit operator string(DataNodeName dataName) => dataName.fullName;


        public string AsId()
        {
            byte[] dataBytes = Encoding.UTF8.GetBytes(fullName);
            byte[] hashBytes = ComputeHash(dataBytes);
            string base64String = Convert.ToBase64String(hashBytes, 0, 10);

            return base64String.Substring(0, 10);
        }

        public override string ToString() => fullName;


        
        private static byte[] ComputeHash(byte[] dataBytes)
        {
            lock (Hash)
            {
                return Hash.ComputeHash(dataBytes);
            }
        }
    }


    //internal class NodeId : Equatable<NodeId>
    //{
    //    private static readonly SHA256 Hash = SHA256.Create();
    //    private readonly string fullName;


    //    private NodeId(string fullName)
    //    {
    //        this.fullName = fullName;

    //        IsEqualWhenSame(fullName);
    //    }


    //    public static explicit operator NodeId(string fullName) => new NodeId(fullName);
    //    public static explicit operator NodeId(DataNodeName dataName) => dataName.fullName;


    //    public string AsId()
    //    {
    //        byte[] dataBytes = Encoding.UTF8.GetBytes(fullName);
    //        byte[] hashBytes = ComputeHash(dataBytes);
    //        string base64String = Convert.ToBase64String(hashBytes, 0, 10);

    //        return base64String.Substring(0, 10);
    //    }

    //    public override string ToString() => fullName;



    //    private static byte[] ComputeHash(byte[] dataBytes)
    //    {
    //        lock (Hash)
    //        {
    //            return Hash.ComputeHash(dataBytes);
    //        }
    //    }
    //}
}
