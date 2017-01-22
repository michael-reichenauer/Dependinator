using System;
using System.Windows;
using System.Windows.Media;
using Dependiator.MainViews;
using Dependiator.Modeling.Analyzing;


namespace Dependiator.Modeling
{
	internal class ModuleViewModel : ItemViewModel
	{
		private readonly Module module;

		public ModuleViewModel(Module module)
			: base(module)
		{
			this.module = module;
			StrokeThickness = module.Element.Type == Element.MemberType ? 0.5 : 1;
		}


		public string ToolTip => module.FullName;
		public double StrokeThickness { get; }
		public double RectangleWidth => module.ItemBounds.Width * module.Scale - StrokeThickness * 2;
		public double RectangleHeight => module.ItemBounds.Height * module.Scale - StrokeThickness * 2;
		public Brush RectangleBrush => module.RectangleBrush;
		public Brush HoverBrush => module.RectangleBrush;

		public Brush BackgroundBrush => module.BackgroundBrush;

		public string Name => module.ViewNodeSize.Width > 40 ? module.Name : " ";


		public int FontSize
		{
			get
			{
				int fontSize = (int)(12 * module.Scale * module.NodeScale);
				fontSize = Math.Max(8, fontSize);
				return Math.Min(20, fontSize);
			}
		}


		internal void MouseMove(Point viewPosition, Vector viewOffset, bool isFirst)
		{
			module.Resize(viewPosition, viewOffset, isFirst);
		}


		//public string Id => Branch.Id;
			//public string Name => Branch.Name;
			//public string Dashes { get; set; }
			//public int NeonEffect { get; set; }
			//public int StrokeThickness { get; set; }





			//public Command ChangeColorCommand => Command(() =>
			//{
			//	themeService.ChangeBranchBrush(Branch);
			//	repositoryCommands.RefreshView();
			//});

	}
}