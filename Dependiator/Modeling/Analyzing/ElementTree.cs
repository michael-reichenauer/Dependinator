using System.Collections.Generic;


namespace Dependiator.Modeling.Analyzing
{
	internal class ElementTree
	{
		public ElementTree(NameSpaceElement root)
		{
			Root = root;
		}

		public NameSpaceElement Root { get; } 
	}
}