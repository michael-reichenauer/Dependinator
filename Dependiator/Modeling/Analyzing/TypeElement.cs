using System.Collections.Generic;
using System.Linq;


namespace Dependiator.Modeling.Analyzing
{
	internal class TypeElement : Element
	{
		public TypeElement(string name, string fullName)
			: base(name, fullName)
		{
		}

		public IEnumerable<MemberElement> TypeItems => ChildElements.OfType<MemberElement>();
	}
}