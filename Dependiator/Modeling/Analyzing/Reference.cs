namespace Dependiator.Modeling.Analyzing
{
	internal class Reference
	{
		public Reference(Element source, Element target)
		{
			Source = source;
			Target = target;
		}


		public Element Source { get; }
		public Element Target { get; }

		public override string ToString() => $"{Source} -> {Target}";
	}
}