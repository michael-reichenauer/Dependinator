//using System.Windows;
//using Dependiator.MainViews;


//namespace Dependiator.Modeling
//{
//	internal class ModuleName : Node
//	{
//		public ModuleName(INodeService nodeService, string name, Module module) 
//			: base(nodeService, module)
//		{
//			Name = name;
//			ActualNodeBoundsNoScale = new Rect(0, 0, module.ActualNodeBounds.Width, 18);
//		}

//		public override ItemViewModel ViewModelFactory() => new ModuleNameViewModel(this);

//		public string Name { get; }


//		public override bool CanBeShown()
//		{
//			return ParentNode.ViewNodeSize.Width > 50;
//		}


//		//private Size MeasureString(string candidate)
//		//{
//		//	var formattedText = new FormattedText(
//		//			candidate,
//		//			CultureInfo.CurrentUICulture,
//		//			FlowDirection.LeftToRight,
//		//			new Typeface(FontFamily.., FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
//		//			this.textBlock.FontSize,
//		//			Brushes.Black);

//		//	return new Size(formattedText.Width, formattedText.Height);
//		//}
//	}
//}