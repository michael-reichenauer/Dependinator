using System.Collections.Generic;
using System.Linq;


namespace Dependiator.Modeling.Analyzing
{
	internal class NameSpaceElement : Element
	{
		public NameSpaceElement(string name, string fullName)
			: base(name, fullName)
		{
		}


		public IEnumerable<NameSpaceElement> NameSpaces => ChildElements.OfType<NameSpaceElement>();
		public IEnumerable<TypeElement> Types => ChildElements.OfType<TypeElement>();
	}
}