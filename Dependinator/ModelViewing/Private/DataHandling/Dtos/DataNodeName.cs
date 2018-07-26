using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private.DataHandling.Dtos
{
    internal class DataNodeName : Equatable<DataNodeName>
    {
        public static readonly DataNodeName None = new DataNodeName("");

        private string fullName;


        private DataNodeName(string fullName)
        {
            this.fullName = fullName;

            IsEqualWhenSame(fullName);
        }


        public static explicit operator DataNodeName(string fullName) => new DataNodeName(fullName);
        public static explicit operator string(DataNodeName dataName) => dataName.fullName;

        public override string ToString() => fullName;
    }
}
