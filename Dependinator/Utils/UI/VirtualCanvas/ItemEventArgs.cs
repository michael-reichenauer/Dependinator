using System;


namespace Dependinator.Utils.UI.VirtualCanvas
{
    public class ItemEventArgs : EventArgs
    {
        public ItemEventArgs(int virtualId)
        {
            VirtualId = virtualId;
        }


        public int VirtualId { get; }
    }
}
