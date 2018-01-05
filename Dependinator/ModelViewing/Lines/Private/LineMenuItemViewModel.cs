using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Lines.Private
{
	internal class LineMenuItemViewModel : ViewModel
	{
		private readonly Lazy<ObservableCollection<LineMenuItemViewModel>> subItems;

		public LineMenuItemViewModel(Link link, string text, IEnumerable<LineMenuItemViewModel> subLinkItems)
		{
			Link = link;
			Text = text;
			SubLinkItems = subLinkItems;
			subItems = new Lazy<ObservableCollection<LineMenuItemViewModel>>(() => 
				new ObservableCollection<LineMenuItemViewModel>(SubLinkItems));
		}


		public Link Link { get => Get<Link>(); set => Set(value); }

		public string Text { get => Get(); set => Set(value); }

		public IEnumerable<LineMenuItemViewModel> SubLinkItems { get; }

		public ObservableCollection<LineMenuItemViewModel> SubItems => subItems.Value;

		public Command<Link> LinkCommand { get; }

		public override string ToString() => $"{Text} ({SubLinkItems.Count()})";
	}
}