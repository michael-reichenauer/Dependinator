using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dependinator.ModelViewing.DataHandling;
using Dependinator.ModelViewing.DataHandling.Dtos;
using Dependinator.ModelViewing.ModelHandling.Core;
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

		public string Text => Item.NodeName == NodeName.Root && SubItems.Any()
			? "all nodes" : Item.NodeName == NodeName.Root ? "<no dependencies>" : Item.NodeName.DisplayName;

		public ObservableCollection<DependencyItemViewModel> SubItems { get; }
		public bool IsShowCodeButton => IsShowButtons && Item.HasCode;
		public bool IsShowVisibilityButton => IsShowButtons;

		public string ToolTip { get => Get(); set => Set(value); }


		public bool IsShowButtons
		{
			get
			{
				if (Item.NodeName == NodeName.Root && !SubItems.Any())
				{
					return false;
				}

				return Get();
			}
			set => Set(value).Notify(nameof(IsShowCodeButton), nameof(IsShowVisibilityButton));
		}

		public bool IsSelected { get => Get(); set => Set(value); }
		public bool IsExpanded { get => Get(); set => Set(value); }

		public Command ShowCodeCommand => Command(() => itemCommands.ShowCode(Item.NodeName));
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