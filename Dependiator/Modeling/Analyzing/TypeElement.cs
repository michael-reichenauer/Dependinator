using System.Collections.Generic;
using System.Linq;


namespace Dependiator.Modeling.Analyzing
{
	internal class TypeElement : Element
	{
		public TypeElement(string name, string fullName, Element element)
			: base(name, fullName, element)
		{
		}

		public IEnumerable<MemberElement> TypeItems => ChildElements.OfType<MemberElement>();
	}
}