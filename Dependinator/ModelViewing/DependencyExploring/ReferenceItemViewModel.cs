using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.DependencyExploring
{
	internal class ReferenceItemViewModel : ViewModel
	{
		private readonly ReferencesViewModel referencesViewModel;
		private readonly bool isSourceItem;
		public static readonly TimeSpan MouseEnterDelay = TimeSpan.FromMilliseconds(300);
		
		private readonly DelayDispatcher delayDispatcher = new DelayDispatcher();
		private bool isHidden = false;
		private bool isSubReferences = false;


		public ReferenceItemViewModel(
			ReferenceItem item,
			ReferencesViewModel referencesViewModel,
			bool isSourceItem)
		{
			this.Item = item;
			this.referencesViewModel = referencesViewModel;
			this.isSourceItem = isSourceItem;

			SubItems = ToSubItems(item.SubItems);

			TextBrush = item.ItemTextBrush();
		}


		public ReferenceItem Item { get; }

		public string Text => Item.Text;
		public Brush TextBrush { get => Get<Brush>(); set => Set(value); }

		public ObservableCollection<ReferenceItemViewModel> SubItems { get; }
		public bool IsShowCodeButton => IsShowButtons && Item.Node.CodeText != null;
		public bool IsShowVisibilityButton => IsShowButtons;

		public string ToolTip => Item.ToolTip;

		public bool IsShowButtons
		{
			get => Get();
			set => Set(value).Notify(nameof(IsShowCodeButton), nameof(IsShowVisibilityButton));
		}

		public bool IsSelected { get => Get(); set => Set(value); }
		public bool IsExpanded { get => Get(); set => Set(value); }
		public Command ToggleVisibilityCommand => Command(ToggleVisibility);
		public Command ShowCodeCommand => Command(() => Item.ShowCode());
		public Command ToggleCollapseCommand => Command(() => SetExpand(!IsExpanded));
		public Command FilterCommand => Command(() => Filter());


		private void Filter()
		{
			IsSelected = true;
			referencesViewModel.FilterOn(Item, isSourceItem);
		}


		private void SetExpand(bool isExpand)
		{
			IsExpanded = isExpand;
			SubItems.ForEach(i => i.SetExpand(isExpand));
		}


		private void ToggleVisibility()
		{
			SetVisibility(!isHidden);

			if (isHidden)
			{
				IsExpanded = false;
			}
		}


		private ObservableCollection<ReferenceItemViewModel> ToSubItems(
			IEnumerable<ReferenceItem> subItems)
		{
			return new ObservableCollection<ReferenceItemViewModel>(
				subItems.Select(i => new ReferenceItemViewModel(i, referencesViewModel, isSourceItem)));
		}


		private void SetVisibility(bool isHide)
		{
			isHidden = isHide;
			TextBrush = isHidden ? Item.ItemTextHiddenBrush() : Item.ItemTextBrush();
			SubItems.ForEach(s => s.SetVisibility(isHide));
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