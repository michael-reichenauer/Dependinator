namespace Dependiator.Modeling.Analyzing
{
	internal class ElementFactory : IElementFactory
	{
		public MemberElement CreateMember(ElementName name, TypeElement parent)
		{
			return new MemberElement(name, parent);
		}

		public NameSpaceElement CreateNameSpace(ElementName name, NameSpaceElement parent)
		{
			return new NameSpaceElement(name, parent);
		}

		public TypeElement CreateType(ElementName name, Element parent)
		{
			return new TypeElement(name, parent);
		}
	}
}