using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Dependinator.ModelViewing.Items;
using Dependinator.ModelViewing.Links.Private;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Links
{
	internal class LineViewModel : ItemViewModel
	{
		private readonly ILineViewModelService lineViewModelService;
		private readonly DelayDispatcher delayDispatcher = new DelayDispatcher();
		private readonly Lazy<ObservableCollection<LinkItem>> sourceLinks;
		private readonly Lazy<ObservableCollection<LinkItem>> targetLinks;

	


		public LineViewModel(
			ILineViewModelService lineViewModelService,
			ILineControlService lineControlService,
			Line line)
		{
			this.lineViewModelService = lineViewModelService;

			this.Line = line;
			line.View.ViewModel = this;
			LineControl = new LineControl(lineControlService, line);
			ItemZIndex = -1;

			UpdateLine();
			TrackSourceOrTargetChanges();

			sourceLinks = new Lazy<ObservableCollection<LinkItem>>(GetSourceLinkItems);
			targetLinks = new Lazy<ObservableCollection<LinkItem>>(GetTargetLinkItems);
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


		public void UpdateToolTip() => ToolTip = lineViewModelService.GetLineToolTip(Line);

		public ObservableCollection<LinkItem> SourceLinks => sourceLinks.Value;

		public ObservableCollection<LinkItem> TargetLinks => targetLinks.Value;

		public Command RemovePointCommand => Command(LineControl.RemovePoint);


		public void ToggleLine()
		{

		}


		public override void MoveItem(Vector moveOffset) => LineControl.MovePoints(moveOffset);



		private ObservableCollection<LinkItem> GetSourceLinkItems()
		{
			IEnumerable<LinkItem> items = lineViewModelService.GetSourceLinkItems(Line);
			return new ObservableCollection<LinkItem>(items);
		}



		private ObservableCollection<LinkItem> GetTargetLinkItems()
		{
			IEnumerable<LinkItem> items = lineViewModelService.GetTargetLinkItems(Line);
			return new ObservableCollection<LinkItem>(items);
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





		public override string ToString() => $"{Line}";


		public void UpdateLine()
		{
			try
			{
				if (!CanShow)
				{
					return;
				}

				lineViewModelService.UpdateLineEndPoints(Line);
				lineViewModelService.UpdateLineBounds(Line);
			}
			catch (Exception e)
			{
				Log.Exception(e);
			}
		}



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


		public void OnMouseWheel(UIElement uiElement, MouseWheelEventArgs e)
		{
			lineViewModelService.OnMouseWheel(this, uiElement, e);
		}
	}
}