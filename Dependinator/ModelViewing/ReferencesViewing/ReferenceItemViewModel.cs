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

			Text = GetText();
			TextBrush = item.ItemTextBrush();
			TextStyle = item.IsSubReference ? FontStyles.Italic : FontStyles.Normal;
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
		public FontStyle TextStyle { get => Get<FontStyle>(); set => Set(value); }
		public ObservableCollection<ReferenceItemViewModel> SubItems { get; }
		public bool IsShowIncomingButton => IsShowButtons && !item.IsIncoming;
		public bool IsShowOutgoingButton => IsShowButtons && item.IsIncoming;

		public bool IsIncoming => item.IsIncoming;
		public string ToolTip => item.Node.Name.DisplayFullNameWithType;

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
				IEnumerable<ReferenceItem> subReferences = item.GetSubReferences(isIncoming);

				int index = 0;
				subReferences.ForEach(i =>
				{
					item.AddChild(i);
					SubItems.Insert(index++, new ReferenceItemViewModel(i));
				});

				IsExpanded = true;
			}
			else
			{
				var subReferences = SubItems.Where(i => i.item.IsSubReference).ToList();
				subReferences.ForEach(i => SubItems.Remove(i));
			}
		}


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