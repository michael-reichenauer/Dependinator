using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependinator.ModelViewing.CodeViewing;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.UI;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	internal class ReferenceItemViewModel : ViewModel
	{
		
		private readonly ReferencesViewModel referencesViewModel;
		private readonly bool isSource;
		public static readonly TimeSpan MouseEnterDelay = TimeSpan.FromMilliseconds(300);


		private readonly DelayDispatcher delayDispatcher = new DelayDispatcher();
		private bool isHidden = false;
		private bool isSubReferences = false;


		public ReferenceItemViewModel(
			ReferenceItem item,
			ReferencesViewModel referencesViewModel,
			bool isSource)
		{
			this.Item = item;
			this.referencesViewModel = referencesViewModel;
			this.isSource = isSource;

			SubItems = ToSubItems(item.SubItems);

			TextBrush = item.ItemTextBrush();
			TextStyle = item.IsSubReference ? FontStyles.Italic : FontStyles.Normal;
		}


		public ReferenceItem Item { get; }

		public string Text => Item.Text;
		public Brush TextBrush { get => Get<Brush>(); set => Set(value); }

		public FontStyle TextStyle { get => Get<FontStyle>(); set => Set(value); }
		public ObservableCollection<ReferenceItemViewModel> SubItems { get; }
		public bool IsShowIncomingButton => IsShowButtons && !Item.IsIncoming && !Item.IsSubReference;
		public bool IsShowOutgoingButton => IsShowButtons && Item.IsIncoming && !Item.IsSubReference;
		public bool IsShowCodeButton => IsShowButtons && Item.Node.CodeText != null;
		public bool IsShowVisibilityButton => IsShowButtons && !Item.IsSubReference;
		//public bool IsIncomingIcon => item.IsIncoming && item.IsTitle;
		//public bool IsOutgoingIcon => !item.IsIncoming && item.IsTitle;

		//public bool IsIncoming => item.IsIncoming;
		public string ToolTip => Item.ToolTip;
		public string IncomingButtonToolTip =>
			$"Toggle show references from within {Item.BaseNode?.Name.DisplayFullNoParametersName}";
		public string OutgoingButtonToolTip =>
			$"Toggle show references to within {Item.BaseNode?.Name.DisplayFullNoParametersName}";

		public bool IsShowButtons
		{
			get => Get(); set => Set(value)
				.Notify(
					nameof(IsShowIncomingButton),
					nameof(IsShowOutgoingButton),
					nameof(IsShowCodeButton),
					nameof(IsShowVisibilityButton));
		}




		private bool isDisableCallFilter = false;

		public bool IsSelected
		{
			get => Get();
			set => Set(value);
		}

		public bool IsExpanded { get => Get(); set => Set(value); }
		public Command ToggleVisibilityCommand => Command(ToggleVisibility);
		public Command IncomingCommand => Command(() => ToggleSubReferences(!Item.IsIncoming));
		public Command OutgoingCommand => Command(() => ToggleSubReferences(!Item.IsIncoming));
		public Command ShowCodeCommand => Command(() => Item.ShowCode());
		public Command ToggleCollapseCommand => Command(() => SetExpand(!IsExpanded));
		public Command FilterCommand => Command(() => referencesViewModel.FilterOn(Item, isSource));


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


		private void ToggleSubReferences(bool isIncoming)
		{
			isSubReferences = !isSubReferences;

			if (isSubReferences)
			{
				ReferenceItem newItem = new ReferenceItem(
					Item.ItemService, null, isIncoming, null, true, SubTitleText(isIncoming));
				Item.AddChild(newItem);

				IEnumerable<ReferenceItem> subReferences = Item.GetSubReferences(isIncoming);
				newItem.AddChildren(subReferences);

				ReferenceItemViewModel newItemViewModel = new ReferenceItemViewModel(
					newItem, referencesViewModel, isSource);
				newItemViewModel.IsExpanded = true;

				SubItems.Insert(0, newItemViewModel);
				IsExpanded = true;
			}
			else
			{
				var subReferences = SubItems.Where(i => i.Item.IsSubReference).ToList();
				subReferences.ForEach(i => SubItems.Remove(i));
			}
		}


		private static string SubTitleText(bool isIncoming) =>
			isIncoming ? "References from:" : "References to:";


		private ObservableCollection<ReferenceItemViewModel> ToSubItems(
			IEnumerable<ReferenceItem> subItems)
		{
			return new ObservableCollection<ReferenceItemViewModel>(
				subItems.Select(i => new ReferenceItemViewModel(i, referencesViewModel, isSource)));
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