﻿using System.Windows;
using Dependiator.Git;


namespace Dependiator.Features.Branches.Private
{
	/// <summary>
	/// Interaction logic for DeleteBranchDialog.xaml
	/// </summary>
	public partial class DeleteBranchDialog : Window
	{
		private readonly DeleteBranchDialogViewModel viewModel;


		public DeleteBranchDialog(
			Window owner,
			BranchName branchName,
			bool isLocal,
			bool isRemote)
		{
			Owner = owner;
			InitializeComponent();

			viewModel = new DeleteBranchDialogViewModel();
			DataContext = viewModel;

			viewModel.BranchName = branchName;
			viewModel.IsLocal = isLocal;
			viewModel.CanLocal = isLocal && isRemote;
			viewModel.IsRemote = isRemote;
			viewModel.CanRemote = isRemote && isLocal;
		}


		public BranchName BranchName => viewModel.BranchName;
		public bool IsLocal => viewModel.IsLocal;
		public bool IsRemote => viewModel.IsRemote;
	}
}
