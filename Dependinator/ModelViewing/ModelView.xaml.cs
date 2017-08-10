﻿using System.Windows;
using UserControl = System.Windows.Controls.UserControl;


namespace Dependinator.ModelViewing
{
	/// <summary>
	/// Interaction logic for ModelView.xaml
	/// </summary>
	public partial class ModelView : UserControl
	{
		private ModelViewModel viewModel;


		public ModelView()
		{
			InitializeComponent();
		}


		private async void MainView_OnLoaded(object sender, RoutedEventArgs e)
		{
			viewModel = (ModelViewModel)DataContext;
			ItemsView.SetFocus();
			await viewModel.LoadAsync();
		}
	}
}
