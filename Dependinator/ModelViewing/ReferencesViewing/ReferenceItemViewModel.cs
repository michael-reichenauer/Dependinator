using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferenceItemViewModel : ViewModel
	{
		private readonly IReferenceItemService referenceItemService;
		public static readonly TimeSpan MouseEnterDelay = TimeSpan.FromMilliseconds(300);


		private readonly DelayDispatcher delayDispatcher = new DelayDispatcher();
		private bool isHidden = false;

		public ReferenceItemViewModel(
			IReferenceItemService referenceItemService,
			IEdge edge, string text, IEnumerable<ReferenceItemViewModel> subItems)
		{
			this.referenceItemService = referenceItemService;
			SubItems = new ObservableCollection<ReferenceItemViewModel>(subItems);

			Edge = edge;
			Text = text;
			TextBrush = referenceItemService.ItemTextBrush();
		}


		public IEdge Edge { get; }
		public string Text { get => Get(); set => Set(value); }
		public Brush TextBrush { get => Get<Brush>(); set => Set(value); }
		public bool IsSelected { get => Get(); set => Set(value); }

		public bool IsExpanded { get => Get(); set => Set(value); }

		public bool IsShowButton { get => Get(); set => Set(value); }

		public Command ToggleCommand => Command(() => Show(!isHidden));


		private void Show(bool isHide)
		{
			isHidden = isHide;
			TextBrush = isHidden ? referenceItemService.ItemTextHiddenBrush() : referenceItemService.ItemTextBrush();
			foreach (var item in SubItems)
			{
				item.Show(isHide);
			}
		}


		public void OnMouseEnter()
		{
			delayDispatcher.Delay(MouseEnterDelay, _ => { IsShowButton = true; });
		}


		public void OnMouseLeave()
		{
			delayDispatcher.Cancel();
			IsShowButton = false;
		}

		public ObservableCollection<ReferenceItemViewModel> SubItems { get; }
	}
}