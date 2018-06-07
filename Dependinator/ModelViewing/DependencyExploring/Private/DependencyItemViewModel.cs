using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dependinator.Common;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.DependencyExploring.Private
{
	internal class DependencyItemViewModel : ViewModel
	{
		private readonly IItemCommands itemCommands;
		private readonly bool isSourceItem;
		public static readonly TimeSpan MouseEnterDelay = TimeSpan.FromMilliseconds(300);
		
		private readonly DelayDispatcher delayDispatcher = new DelayDispatcher();


		public DependencyItemViewModel(
			DependencyItem item,
			IItemCommands itemCommands,
			bool isSourceItem)
		{
			this.Item = item;
			this.itemCommands = itemCommands;
			this.isSourceItem = isSourceItem;

			SubItems = ToSubItems(item.SubItems);
		}


		public DependencyItem Item { get; }

		public string Text => Item.NodeName == NodeName.Root ? "all nodes" : Item.NodeName.DisplayName;

		public ObservableCollection<DependencyItemViewModel> SubItems { get; }
		public bool IsShowCodeButton => IsShowButtons && Item.CodeText != null;
		public bool IsShowVisibilityButton => IsShowButtons;

		public string ToolTip { get => Get(); set => Set(value); }


		public bool IsShowButtons
		{
			get => Get();
			set => Set(value).Notify(nameof(IsShowCodeButton), nameof(IsShowVisibilityButton));
		}

		public bool IsSelected { get => Get(); set => Set(value); }
		public bool IsExpanded { get => Get(); set => Set(value); }
		//public Command ToggleVisibilityCommand => Command(ToggleVisibility);
		public Command ShowCodeCommand => Command(() => itemCommands.ShowCode(
			Item.NodeName.DisplayFullName, Item.CodeText));
		public Command ToggleCollapseCommand => Command(SetExpand);
		public Command FilterCommand => Command(Filter);


		private void Filter()
		{
			IsSelected = true;
			itemCommands.FilterOn(Item, isSourceItem);
		}


		private void SetExpand() => SetExpand(!IsExpanded);


		private void SetExpand(bool isExpand)
		{
			IsExpanded = isExpand;
			SubItems.ForEach(i => i.SetExpand(isExpand));
		}


		private ObservableCollection<DependencyItemViewModel> ToSubItems(
			IEnumerable<DependencyItem> subItems)
		{
			return new ObservableCollection<DependencyItemViewModel>(
				subItems.Select(i => new DependencyItemViewModel(i, itemCommands, isSourceItem)));
		}


		//private void ToggleVisibility()
		//{
		//	SetVisibility(!isHidden);

		//	if (isHidden)
		//	{
		//		IsExpanded = false;
		//	}
		//}



		//private void SetVisibility(bool isHide)
		//{
		//	isHidden = isHide;
		//	TextBrush = isHidden ? Item.ItemTextHiddenBrush() : Item.ItemTextBrush();
		//	SubItems.ForEach(s => s.SetVisibility(isHide));
		//}


		public void OnMouseEnter()
		{
			delayDispatcher.Delay(MouseEnterDelay, _ => { IsShowButtons = true; });
		}


		public void OnMouseLeave()
		{
			delayDispatcher.Cancel();
			IsShowButtons = false;
		}


		public void UpdateToolTip()
		{
			string filter = isSourceItem ? "to target" : "from source";
			ToolTip = $"{Item.NodeName.DisplayFullName}\nClick to filter dependencies {filter}";
		}
	}
}