using System.Collections.Generic;
using System.Windows;
using Dependiator.Utils.UI.VirtualCanvas;


namespace Dependiator.MainViews.Private
{
	internal class MainViewVirtualItemsSource : VirtualItemsSource
	{
		private const int minCommitIndex = 0;
		private const int minBranchIndex = 1000000;
		private const int minMergeIndex = 2000000;

		private const int maxCommitIndex = minBranchIndex;
		private const int maxBranchIndex = minMergeIndex;
		private const int maxMergeIndex = 3000000;

		private readonly IReadOnlyList<ModuleViewModel> modules;

		private Rect virtualArea;

		public MainViewVirtualItemsSource(
			IReadOnlyList<ModuleViewModel> modules)
		{
			this.modules = modules;
		}


		public void DataChanged()
		{
			virtualArea = EmptyExtent;

			foreach (ModuleViewModel module in modules)
			{
				virtualArea.Union(module.CanvasBounds);
			}

			TriggerInvalidated();
		}

		//public void DataChanged()
		//{
		//	virtualArea = new Rect(0, 0, width, Converters.ToRowExtent(commits.Count));
		//	TriggerInvalidated();
		//}


		protected override Rect VirtualArea => virtualArea;


		/// <summary>
		/// Returns range of item ids, which are visible in the area currently shown
		/// </summary>
		protected override IEnumerable<int> GetItemIds(Rect viewArea)
		{
			if (VirtualArea == Rect.Empty || viewArea == Rect.Empty)
			{
				yield break;
			}

			// Get the part of the rectangle that is visible
			viewArea.Intersect(VirtualArea);

			//int viewAreaTopIndex = Converters.ToTopRowIndex(viewArea, commits.Count);
			//int viewAreaBottomIndex = Converters.ToBottomRowIndex(viewArea, commits.Count);


			// Return visible modules
			for (int i = 0; i < modules.Count; i++)
			{
				ModuleViewModel merge = modules[i];
				if (viewArea.IntersectsWith(merge.CanvasBounds))
				{
					yield return i + minMergeIndex;
				}
			}
			
		}


		/// <summary>
		/// Returns the item (commit, branch, merge) corresponding to the specified id.
		/// Commits are in the 0->branchBaseIndex-1 range
		/// Branches are in the branchBaseIndex->mergeBaseIndex-1 range
		/// Merges are mergeBaseIndex-> ... range
		/// </summary>
		protected override object GetItem(int virtualId)
		{
			//if (virtualId >= minCommitIndex && virtualId < maxCommitIndex)
			//{
			//	int commitIndex = virtualId - minCommitIndex;
			//	if (commitIndex < commits.Count)
			//	{
			//		return commits[commitIndex];
			//	}
			//}
			//else if (virtualId >= minBranchIndex && virtualId < maxBranchIndex)
			//{
			//	int branchIndex = virtualId - minBranchIndex;
			//	if (branchIndex < branches.Count)
			//	{
			//		return branches[branchIndex];
			//	}
			//}
			if (virtualId >= minMergeIndex && virtualId < maxMergeIndex)
			{
				int mergeIndex = virtualId - minMergeIndex;
				if (mergeIndex < modules.Count)
				{
					return modules[mergeIndex];
				}
			}

			return null;
		}


		private static bool IsVisable(
			int areaTopIndex,
			int areaBottomIndex,
			int itemTopIndex,
			int ItemBottomIndex)
		{
			return
				(itemTopIndex >= areaTopIndex && itemTopIndex <= areaBottomIndex)
				|| (ItemBottomIndex >= areaTopIndex && ItemBottomIndex <= areaBottomIndex)
				|| (itemTopIndex <= areaTopIndex && ItemBottomIndex >= areaBottomIndex);
		}
	}
}