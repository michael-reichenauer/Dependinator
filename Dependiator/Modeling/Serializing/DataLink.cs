namespace Dependiator.Modeling.Serializing
{
	public class DataLink
	{
		public string Source { get; set; }

		public string Target{ get; set; }

		public override string ToString() => $"{Source}->{Target}";
	}
}