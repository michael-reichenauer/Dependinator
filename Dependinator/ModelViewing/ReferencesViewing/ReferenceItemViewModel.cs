using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferenceItemViewModel : ViewModel
	{
		private readonly ReferenceItem item;
		public static readonly TimeSpan MouseEnterDelay = TimeSpan.FromMilliseconds(300);


		private readonly DelayDispatcher delayDispatcher = new DelayDispatcher();
		private bool isHidden = false;


		public ReferenceItemViewModel(ReferenceItem item)
		{
			this.item = item;

			SubItems = new ObservableCollection<ReferenceItemViewModel>(
				item.Items.Select(i => new ReferenceItemViewModel(i)));

			Text = GetText();
			TextBrush = item.ItemTextBrush();
		}


		private string GetText()
		{
			if (item.Parent != null && item.Parent.Node == item.Node.Parent)
			{
				return item.Node.Name.DisplayName;
			}

			return item.Node.Name.DisplayFullName;
		}


		public string Text { get => Get(); set => Set(value); }
		public Brush TextBrush { get => Get<Brush>(); set => Set(value); }
		public ObservableCollection<ReferenceItemViewModel> SubItems { get; }

		public bool IsShowIncomingButton => IsShowButtons && !item.IsIncoming;
		public bool IsShowOutgoingButton => IsShowButtons && item.IsIncoming;
		public bool IsShowButtons
		{
			get => Get(); set => Set(value)
				.Notify(nameof(IsShowIncomingButton), nameof(IsShowOutgoingButton));
		}

		public bool IsSelected { get => Get(); set => Set(value); }
		public bool IsExpanded { get => Get(); set => Set(value); }
		public Command ToggleCommand => Command(() => Show(!isHidden));
		public Command IncomingCommand => Command(() => { });
		public Command OutgoingCommand => Command(() => { });


		private void Show(bool isHide)
		{
			isHidden = isHide;
			TextBrush = isHidden ? item.ItemTextHiddenBrush() : item.ItemTextBrush();
			SubItems.ForEach(s => s.Show(isHide));
		}


		public void OnMouseEnter()
		{
			delayDispatcher.Delay(MouseEnterDelay, _ => { IsShowButtons = true; });
		}


		public void OnMouseLeave()
		{
			delayDispatcher.Cancel();
			IsShowButtons = false;
		}


	}
}