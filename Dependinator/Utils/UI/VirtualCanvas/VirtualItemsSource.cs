using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;


namespace Dependinator.Utils.UI.VirtualCanvas
{
    public abstract class VirtualItemsSource : ISpatialItemsSource, IList
    {
        public static readonly Rect EmptyExtent = RectEx.Zero;

        /// <summary>
        ///     The virtual area, which would be needed to show all commits
        /// </summary>
        protected abstract Rect VirtualArea { get; }

        public object this[int id] { get { return GetVirtualItem(id); } set { } }


        int IList.Add(object value) => 0;


        void IList.Clear()
        {
        }


        bool IList.Contains(object value) => false;

        int IList.IndexOf(object value) => 0;


        void IList.Insert(int index, object value)
        {
        }


        void IList.Remove(object value)
        {
        }


        void IList.RemoveAt(int index)
        {
        }


        void ICollection.CopyTo(Array array, int index)
        {
        }


        bool IList.IsFixedSize => false;

        bool IList.IsReadOnly => true;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot { get; } = new object();

        int ICollection.Count => int.MaxValue;


        IEnumerator IEnumerable.GetEnumerator()
        {
            yield break;
        }


        public event EventHandler ExtentChanged;

        public event EventHandler QueryInvalidated;

        public Rect Extent => VirtualArea;

        public IEnumerable<int> Query(Rect viewArea) => GetVirtualItemIds(viewArea);


        /// <summary>
        ///     Returns range of item ids, which are visible in the view area currently shown
        /// </summary>
        protected abstract IEnumerable<int> GetVirtualItemIds(Rect viewArea);


        /// <summary>
        ///     Returns the item corresponding to the specified id.
        /// </summary>
        protected abstract object GetVirtualItem(int id);


        public void TriggerExtentChanged()
        {
            ExtentChanged?.Invoke(this, EventArgs.Empty);
        }


        public void TriggerItemsChanged()
        {
            QueryInvalidated?.Invoke(this, EventArgs.Empty);
        }


        public void TriggerInvalidated()
        {
            QueryInvalidated?.Invoke(this, EventArgs.Empty);
            ExtentChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
