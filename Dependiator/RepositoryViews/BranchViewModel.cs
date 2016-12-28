using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Dependiator.Common.ThemeHandling;
using Dependiator.GitModel;
using Dependiator.Utils.UI;


namespace Dependiator.RepositoryViews
{
	internal class BranchViewModel : ViewModel
	{
		private readonly IThemeService themeService;
		private readonly IRepositoryCommands repositoryCommands;

		private readonly Command<Branch> showBranchCommand;

		private readonly ObservableCollection<BranchItem> childBranches
			= new ObservableCollection<BranchItem>();

		public BranchViewModel(
			IThemeService themeService,
			IRepositoryCommands repositoryCommands)
		{
			this.themeService = themeService;
			this.repositoryCommands = repositoryCommands;
			this.showBranchCommand = Command<Branch>(repositoryCommands.ShowBranch);
		}

		// UI properties
		public string Type => nameof(BranchViewModel);
		public int ZIndex => 200;
		public string Id => Branch.Id;
		public string Name => Branch.Name;
		public bool IsMultiBranch => Branch.IsMultiBranch;
		public bool HasChildren => ChildBranches.Count > 0;
		public Rect Rect { get; set; }
		public double Width => Rect.Width;
		public double Top => Rect.Top;
		public double Left => Rect.Left;
		public double Height => Rect.Height;
		public string Line { get; set; }
		public string Dashes { get; set; }

		public int NeonEffect { get; set; }

		public int StrokeThickness { get; set; }
		public Brush Brush { get; set; }
		public Brush HoverBrush { get; set; }
		public Brush HoverBrushNormal { get; set; }
		public Brush HoverBrushHighlight { get; set; }
		public Color DimColor { get; set; }
		public string BranchToolTip { get; set; }
		public bool CanPublish => Branch.CanBePublish;
		public bool CanPush => Branch.CanBePushed;
		public bool CanUpdate => Branch.CanBeUpdated && !Branch.IsLocalPart;
		public bool IsMergeable => Branch.IsCanBeMergeToOther;


		public string SwitchBranchText => $"Switch to branch '{Name}'";
		public string MergeToBranchText => $"Merge to branch '{CurrentBranchName}'";
		public string CurrentBranchName { get; set; }
		public bool CanDeleteBranch => Branch.IsLocal || Branch.IsRemote;

		// Values used by UI properties
		public Branch Branch { get; set; }
		public ObservableCollection<BranchItem> ActiveBranches { get; set; }
		public ObservableCollection<BranchItem> ShownBranches { get; set; }

		public ObservableCollection<BranchItem> ChildBranches
		{
			get
			{
				childBranches.Clear();
				GetChildBranches().ForEach(b => childBranches.Add(b));
				return childBranches;
			}
		}

		public Command ChangeColorCommand => Command(() =>
		{
			themeService.ChangeBranchBrush(Branch);
			repositoryCommands.RefreshView();
		});

		// Some values used by Merge items and to determine if item is visible
		public int BranchColumn { get; set; }
		public int X { get; set; }
		public int TipRowIndex { get; set; }
		public int FirstRowIndex { get; set; }
		public Brush DimBrushHighlight { get; set; }


		public void SetNormal()
		{
			NeonEffect = themeService.Theme.NeonEffect;
			StrokeThickness = 2;
			Brush = HoverBrushNormal;
			DimColor = ((SolidColorBrush)HoverBrushHighlight).Color;

			Notify(nameof(StrokeThickness), nameof(Brush), nameof(DimColor));
		}


		public void SetHighlighted()
		{
			StrokeThickness = 3;
			Brush = HoverBrushHighlight;
			DimColor = ((SolidColorBrush)DimBrushHighlight).Color;
			Notify(nameof(StrokeThickness), nameof(Brush), nameof(DimColor));
		}

		public override string ToString() => $"{Branch}";


		private IReadOnlyList<BranchItem> GetChildBranches()
		{
			return BranchItem.GetBranches(
				Branch.GetChildBranches()
					.Where(b => !ActiveBranches.Any(ab => ab.Branch == b))
					.Take(50)
					.ToList(),
				showBranchCommand);
		}


		public void SetColor(Brush brush)
		{
			Brush = brush;
			HoverBrushNormal = Brush;
			HoverBrushHighlight = themeService.Theme.GetLighterBrush(Brush);
			DimBrushHighlight = themeService.Theme.GetLighterLighterBrush(Brush);
		}
	}
}