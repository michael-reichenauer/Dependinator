using System;


namespace Dependinator.Utils.UI.VirtualCanvas
{
	public class ItemEventArgs : EventArgs
	{
		public int VirtualId { get; }

		public ItemEventArgs(int virtualId)
		{
			VirtualId = virtualId;
		}
	}
}