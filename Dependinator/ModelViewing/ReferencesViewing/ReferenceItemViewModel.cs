using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
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
		private bool isSubReferences = false;


		public ReferenceItemViewModel(ReferenceItem item)
		{
			this.item = item;

			SubItems = ToSubItems(item.SubItems);

			TextBrush = item.ItemTextBrush();
			TextStyle = item.IsSubReference ? FontStyles.Italic : FontStyles.Normal;
		}



		public string Text => item.Text;
		public Brush TextBrush { get => Get<Brush>(); set => Set(value); }
		public FontStyle TextStyle { get => Get<FontStyle>(); set => Set(value); }
		public ObservableCollection<ReferenceItemViewModel> SubItems { get; }
		public bool IsShowIncomingButton => IsShowButtons && !item.IsIncoming;
		public bool IsShowOutgoingButton => IsShowButtons && item.IsIncoming;
		public bool IsIncomingIcon => item.IsIncoming && item.IsTitle;
		public bool IsOutgoingIcon => !item.IsIncoming && item.IsTitle;

		public bool IsIncoming => item.IsIncoming;
		public string ToolTip => item.ToolTip;
		public string IncomingButtonToolTip =>
			$"Toggle show references from {item.BaseNode.Name.DisplayFullNoParametersName}";
		public string OutgoingButtonToolTip => 
			$"Toggle show references to {item.BaseNode.Name.DisplayFullNoParametersName}";

		public bool IsShowButtons
		{
			get => Get(); set => Set(value && !item.IsSubReference)
				.Notify(nameof(IsShowIncomingButton), nameof(IsShowOutgoingButton));
		}

		public bool IsSelected { get => Get(); set => Set(value); }
		public bool IsExpanded { get => Get(); set => Set(value); }
		public Command ToggleVisibilityCommand => Command(ToggleVisibility);
		public Command IncomingCommand => Command(() => ToggleSubReferences(!item.IsIncoming));
		public Command OutgoingCommand => Command(() => ToggleSubReferences(!item.IsIncoming));


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
					item.ItemService, null, isIncoming, null, true, SubTitleText(isIncoming));
				item.AddChild(newItem);

				IEnumerable<ReferenceItem> subReferences = item.GetSubReferences(isIncoming);
				newItem.AddChildren(subReferences);

				ReferenceItemViewModel newItemViewModel = new ReferenceItemViewModel(newItem);
				newItemViewModel.IsExpanded = true;

				SubItems.Insert(0, newItemViewModel);
				IsExpanded = true;
			}
			else
			{
				var subReferences = SubItems.Where(i => i.item.IsSubReference).ToList();
				subReferences.ForEach(i => SubItems.Remove(i));
			}
		}


		private static string SubTitleText(bool isIncoming) => 
			isIncoming ? "References from:" : "References to:";


		private static ObservableCollection<ReferenceItemViewModel> ToSubItems(
			IEnumerable<ReferenceItem> subItems)
		{
			return new ObservableCollection<ReferenceItemViewModel>(
				subItems.Select(i => new ReferenceItemViewModel(i)));
		}


		private void SetVisibility(bool isHide)
		{
			isHidden = isHide;
			TextBrush = isHidden ? item.ItemTextHiddenBrush() : item.ItemTextBrush();
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