using System.Collections.Generic;


namespace Dependiator.Modeling.Analyzing
{
	internal enum ReferenceKind
	{
		Main,
		Direkt,
		Sibling,
		Parent,
		Child
	}


	internal class Reference
	{
		private List<Reference> subReferences = new List<Reference>();

		public Reference(Element source, Element target, ReferenceKind kind)
		{
			Source = source;
			Target = target;
			Kind = kind;
		}

		public Element Source { get; }

		public Element Target { get; }

		public IReadOnlyList<Reference> SubReferences => subReferences;

		public ReferenceKind Kind { get; }

		public void Add(Reference subReference) => subReferences.Add(subReference);

		public override string ToString() => $"{Source} -> {Target} ({subReferences.Count})";
	}
}