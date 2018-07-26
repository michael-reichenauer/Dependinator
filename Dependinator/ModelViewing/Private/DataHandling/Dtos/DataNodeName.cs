using Dependinator.Utils;


namespace Dependinator.ModelViewing.Private.DataHandling.Dtos
{
	internal class DataNodeName : Equatable<DataNodeName>
	{
		public static readonly DataNodeName Root = new DataNodeName("");

		public DataNodeName(string fullName)
		{
			this.FullName = fullName;

			IsEqualWhenSame(fullName);
		}


		public string FullName { get; }

		public static DataNodeName From(string fullName)
		{
			return new DataNodeName(fullName);
		}

		public override string ToString() => FullName;
	}
}