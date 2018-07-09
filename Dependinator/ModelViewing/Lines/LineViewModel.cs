using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.Lines.Private;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Lines
{
	internal class LineViewModel : ItemViewModel, ISelectableItem
	{
		public static readonly TimeSpan MouseEnterDelay = TimeSpan.FromMilliseconds(100);


		private readonly ILineViewModelService lineViewModelService;
		private readonly DelayDispatcher delayDispatcher = new DelayDispatcher();


		public LineViewModel(ILineViewModelService lineViewModelService, Line line)
		{
			this.lineViewModelService = lineViewModelService;
			this.Line = line;
			line.View.ViewModel = this;
			LineControl = lineViewModelService.GetLineControl(line);
			ItemZIndex = -1;

			UpdateLine();
			TrackSourceOrTargetChanges();
		}



		public LineControl LineControl;

		public Line Line { get; }

		public override bool CanShow =>
			ItemScale < 10
			&& Line.Source.View.CanShow && Line.Target.View.CanShow
			&& !Line.IsHidden;

		public double LineWidth => lineViewModelService.GetLineWidth(Line);

		public double ArrowWidth => lineViewModelService.GetArrowWidth(Line);

		public Brush LineBrush => Line.Source != Line.Target.Parent
			? Line.Source.View.ViewModel.RectangleBrush
			: Line.Target.View.ViewModel.RectangleBrush;

		public bool IsMouseOver { get => Get(); private set => Set(value); }

		public bool IsSelected
		{
			get => Get();
			set => Set(value).Notify(nameof(LineBrush), nameof(LineWidth), nameof(ArrowWidth));
		}

		public string LineData => lineViewModelService.GetLineData(Line);

		public string PointsData => lineViewModelService.GetPointsData(Line);
		public string EndPointsData => lineViewModelService.GetEndPointsData(Line);

		public string ArrowData => lineViewModelService.GetArrowData(Line);

		public string StrokeDash => "";

		public string ToolTip { get => Get(); private set => Set(value); }



		public Command RemovePointCommand => Command(LineControl.RemovePoint);

		public Command ShowDependenciesCommand => Command(() => lineViewModelService.ShowReferences(this));


		public override void MoveItem(Vector moveOffset) => LineControl.MovePoints(moveOffset);


		public void UpdateToolTip() =>
			ToolTip = $"{Line.Source.Name.DisplayLongName} -> {Line.Target.Name.DisplayLongName}\n{Line.LinkCount} links";



		public void Clicked()
		{
			lineViewModelService.Clicked(this);
		}


		public void OnMouseEnter()
		{
			delayDispatcher.Delay(MouseEnterDelay, _ =>
			{
				IsMouseOver = true;
				Notify(nameof(LineBrush), nameof(LineWidth), nameof(ArrowWidth));
			});
		}


		public void OnMouseLeave()
		{
			if (!IsSelected)
			{
				delayDispatcher.Cancel();
				IsMouseOver = false;
				Notify(nameof(LineBrush), nameof(LineWidth), nameof(ArrowWidth));
			}
		}


		public void OnMouseWheel(UIElement uiElement, MouseWheelEventArgs e) =>
			lineViewModelService.OnMouseWheel(this, uiElement, e);


		public void UpdateLine()
		{
			if (!CanShow)
			{
				return;
			}

			lineViewModelService.UpdateLineEndPoints(Line);
			lineViewModelService.UpdateLineBounds(Line);
		}


		public override string ToString() => $"{Line}";


		private void TrackSourceOrTargetChanges()
		{
			// Source and targets are siblings. update line when either node is moved
			WhenSet(Line.Source.View.ViewModel, nameof(Line.Source.View.ViewModel.ItemBounds))
				.Notify(SourceOrTargetChanged);
			WhenSet(Line.Target.View.ViewModel, nameof(Line.Target.View.ViewModel.ItemBounds))
				.Notify(SourceOrTargetChanged);
		}


		private void SourceOrTargetChanged(string propertyName)
		{
			UpdateLine();
			NotifyAll();
		}
	}
}