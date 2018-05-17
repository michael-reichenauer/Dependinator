﻿using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Dependinator.Utils.UI;


namespace Dependinator.ModelViewing.Open
{
	/// <summary>
	/// Interaction logic for OpenModelView.xaml
	/// </summary>
	public partial class OpenModelView : UserControl
	{
		private OpenModelViewModel ViewModel => DataContext as OpenModelViewModel;

		//private MouseClicked mouseClicked;


		public OpenModelView()
		{
			InitializeComponent();

			//mouseClicked = new MouseClicked(this, Clicked);
		}


		private void RecentFile_OnClick(object sender, MouseButtonEventArgs e)
		{
			((sender as FrameworkElement)?.DataContext as FileItem)?.OpenFileCommand.Execute();
			}


		private void OpenFile_OnClick(object sender, MouseButtonEventArgs e)
		{
			ViewModel?.OpenFile();
		}


		private void OpenExample_OnClick(object sender, MouseButtonEventArgs e)
		{
			ViewModel?.OpenExampleFile();
		}
	}
}
