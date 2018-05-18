﻿using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dependinator.ModelViewing.ModelHandling.Core;
using Dependinator.Utils;


namespace Dependinator.ModelViewing.ReferencesViewing
{
	/// <summary>
	/// Interaction logic for ReferencesDialog.xaml
	/// </summary>
	public partial class ReferencesDialog : Window
	{
		internal ReferencesDialog(
			IReferenceItemService referenceItemService, 
			Window owner, 
			Node node, 
			Line line,
			bool isIncoming)
		{
			Owner = owner;
			InitializeComponent();

			DataContext = new ReferencesViewModel(referenceItemService, node, line, isIncoming);
		}
	}
}
