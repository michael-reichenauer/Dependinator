using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.ModelViewing.Links
{
	internal class LinkItem : ViewModel
	{
		private readonly Lazy<ObservableCollection<LinkItem>> subItems;

		public LinkItem(Link link, string text, IEnumerable<LinkItem> subLinkItems)
		{
			Link = link;
			Text = text;
			SubLinkItems = subLinkItems;
			subItems = new Lazy<ObservableCollection<LinkItem>>(() => 
				new ObservableCollection<LinkItem>(SubLinkItems));
		}


		public Link Link { get => Get<Link>(); set => Set(value); }

		public string Text { get => Get(); set => Set(value); }

		public IEnumerable<LinkItem> SubLinkItems { get; }

		public ObservableCollection<LinkItem> SubItems => subItems.Value;

		public Command<Link> LinkCommand { get; }

		public override string ToString() => $"{Text} ({SubLinkItems.Count()})";
	}
}