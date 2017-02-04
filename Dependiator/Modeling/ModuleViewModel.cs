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

			if (module.Element.Type == Element.MemberType)
			{
				StrokeThickness = 0.5;
			}
			else if (module.Element.Type == Element.TypeType)
			{
				StrokeThickness = 2;
			}
			else
			{
				StrokeThickness = 1;
			}
		}


		public string ToolTip => module.FullName;
		public double StrokeThickness { get; }
		public double RectangleWidth => module.ItemCanvasBounds.Width * module.CanvasScale - StrokeThickness * 2;
		public double RectangleHeight => module.ItemCanvasBounds.Height * module.CanvasScale - StrokeThickness * 2;
		public Brush RectangleBrush => module.RectangleBrush;
		public Brush HoverBrush => module.RectangleBrush;

		public Brush BackgroundBrush => module.BackgroundBrush;

		public string Name => module.ViewNodeSize.Width > 40 ? module.Name : " ";

		public int CornerRadius => module.Element.Type == Element.TypeType
			? (int)(module.NodeScale * 10).MM(0, 30)
			: 0;

		public int FontSize
		{
			get
			{
				int fontSize = (int)(12 * module.NodeScale);
				return fontSize.MM(8, 20);
			}
		}


		internal void MouseMove(Point viewPosition, Vector viewOffset, bool isFirst)
		{
			module.MoveOrResize(viewPosition, viewOffset, isFirst);
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