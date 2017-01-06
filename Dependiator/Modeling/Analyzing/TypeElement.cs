using System.Collections.Generic;
using System.Linq;


namespace Dependiator.Modeling.Analyzing
{
	internal class TypeElement : Element
	{
		public TypeElement(ElementName name, Element element)
			: base(name, element)
		{
		}

		public IEnumerable<MemberElement> TypeItems => Children.OfType<MemberElement>();
	}
}