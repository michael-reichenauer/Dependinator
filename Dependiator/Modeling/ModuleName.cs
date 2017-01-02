using Dependiator.MainViews;


namespace Dependiator.Modeling
{
	internal class ModuleName : Node
	{
		public ModuleName(INodeService nodeService, string name) 
			: base(nodeService, null)
		{
			Name = name;
		}

		public override ItemViewModel ViewModelFactory() => new ModuleNameViewModel(this);

		public string Name { get; }


		public override bool CanBeShown()
		{
			return ViewNodeBounds.Width > 50;
		}


		//private Size MeasureString(string candidate)
		//{
		//	var formattedText = new FormattedText(
		//			candidate,
		//			CultureInfo.CurrentUICulture,
		//			FlowDirection.LeftToRight,
		//			new Typeface(FontFamily.., FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
		//			this.textBlock.FontSize,
		//			Brushes.Black);

		//	return new Size(formattedText.Width, formattedText.Height);
		//}
	}
}