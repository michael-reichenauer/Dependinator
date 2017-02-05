using System;
using System.Windows;
using System.Windows.Media;
using Dependiator.MainViews;
using Dependiator.Modeling.Analyzing;


namespace Dependiator.Modeling
{
	internal class NodeViewModel : ItemViewModel
	{
		private readonly Node node;

		public NodeViewModel(Node node)
			: base(node)
		{
			this.node = node;

			if (node.Element.Type == Element.MemberType)
			{
				StrokeThickness = 0.5;
			}
			else if (node.Element.Type == Element.TypeType)
			{
				StrokeThickness = 2;
			}
			else
			{
				StrokeThickness = 1;
			}
		}


		public string ToolTip => node.FullName;
		public double StrokeThickness { get; }
		public double RectangleWidth => node.ItemCanvasBounds.Width * node.CanvasScale - StrokeThickness * 2;
		public double RectangleHeight => node.ItemCanvasBounds.Height * node.CanvasScale - StrokeThickness * 2;
		public Brush RectangleBrush => node.RectangleBrush;
		public Brush HoverBrush => node.RectangleBrush;

		public Brush BackgroundBrush => node.BackgroundBrush;

		public string Name => node.ItemViewSize.Width > 40 ? node.Name : " ";

		public int CornerRadius => node.Element.Type == Element.TypeType
			? (int)(node.ItemScale * 10).MM(0, 30)
			: 0;

		public int FontSize
		{
			get
			{
				int fontSize = (int)(12 * node.ItemScale);
				return fontSize.MM(8, 20);
			}
		}


		internal void MouseMove(Point viewPosition, Vector viewOffset, bool isFirst)
		{
			node.MoveOrResize(viewPosition, viewOffset, isFirst);
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