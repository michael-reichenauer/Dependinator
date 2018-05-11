using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
		private readonly ILineViewModelService lineViewModelService;
		private readonly DelayDispatcher delayDispatcher = new DelayDispatcher();
		private readonly Lazy<ObservableCollection<LineMenuItemViewModel>> sourceLinks;
		private readonly Lazy<ObservableCollection<LineMenuItemViewModel>> targetLinks;


		public LineViewModel(ILineViewModelService lineViewModelService, Line line)
		{
			this.lineViewModelService = lineViewModelService;
			this.Line = line;
			line.View.ViewModel = this;
			LineControl = lineViewModelService.GetLineControl(line);
			ItemZIndex = -1;

			UpdateLine();
			TrackSourceOrTargetChanges();

			sourceLinks = new Lazy<ObservableCollection<LineMenuItemViewModel>>(GetSourceLinkItems);
			targetLinks = new Lazy<ObservableCollection<LineMenuItemViewModel>>(GetTargetLinkItems);
		}



		public LineControl LineControl;

		public Line Line { get; }

		public override bool CanShow =>
			ItemScale < 40
			&& Line.Source.View.CanShow && Line.Target.View.CanShow;

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


		public ObservableCollection<LineMenuItemViewModel> SourceLinks => sourceLinks.Value;


		public ObservableCollection<LineMenuItemViewModel> TargetLinks => targetLinks.Value;


		public Command RemovePointCommand => Command(LineControl.RemovePoint);

		public Command ShowIncomingCommand => Command(() => lineViewModelService.ShowReferences(this, true));
		public Command ShowOutgoingCommand => Command(() => lineViewModelService.ShowReferences(this, false));


		public override void MoveItem(Vector moveOffset) => LineControl.MovePoints(moveOffset);



		public void Toggle()
		{
			lineViewModelService.Toggle(Line);
		}



		public void UpdateToolTip() =>
			ToolTip = $"{Line.Source.Name.DisplayFullName} -> {Line.Target.Name.DisplayFullName}, {Line.Links.Count} links";


		private ObservableCollection<LineMenuItemViewModel> GetSourceLinkItems()
		{
			IEnumerable<LineMenuItemViewModel> items = lineViewModelService.GetSourceLinkItems(Line);
			return new ObservableCollection<LineMenuItemViewModel>(items);
		}



		private ObservableCollection<LineMenuItemViewModel> GetTargetLinkItems()
		{
			IEnumerable<LineMenuItemViewModel> items = lineViewModelService.GetTargetLinkItems(Line);
			return new ObservableCollection<LineMenuItemViewModel>(items);
		}


		public void Clicked()
		{
			lineViewModelService.Clicked(this);
		}


		public void OnMouseEnter()
		{
			delayDispatcher.Delay(ModelViewModel.MouseEnterDelay, _ =>
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