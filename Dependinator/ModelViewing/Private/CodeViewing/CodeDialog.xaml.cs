using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Xml;
using Dependinator.Common;
using Dependinator.Common.MessageDialogs;
using Dependinator.Common.ModelMetadataFolders;
using Dependinator.ModelViewing.Private.DataHandling;
using Dependinator.ModelViewing.Private.ModelHandling;
using Dependinator.Utils.ErrorHandling;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;


namespace Dependinator.ModelViewing.Private.CodeViewing
{
	/// <summary>
	/// Interaction logic for ReferencesDialog.xaml
	/// </summary>
	public partial class CodeDialog : Window
	{
		private readonly IDataDetailsService dataDetailsService;
		private readonly Lazy<IModelNotifications> modelNotifications;
		private readonly IMessage message;
		private readonly ModelMetadata modelMetadata;
		private readonly NodeName nodeName;


		internal CodeDialog(
			IDataDetailsService dataDetailsService,
			Lazy<IModelNotifications> modelNotifications,
			IMessage message,
			ModelMetadata modelMetadata,
			WindowOwner owner,
			NodeName nodeName)
		{
			this.dataDetailsService = dataDetailsService;
			this.modelNotifications = modelNotifications;
			this.message = message;
			this.modelMetadata = modelMetadata;
			this.nodeName = nodeName;
			Owner = owner;
			InitializeComponent();

			DataContext = new CodeViewModel(nodeName.DisplayLongName);

			SetSyntaxHighlighting();

			SetCodeText();

			modelNotifications.Value.ModelUpdated += OnModelUpdated;
		}


		protected override void OnClosing(CancelEventArgs e)
		{
			modelNotifications.Value.ModelUpdated -= OnModelUpdated;

			base.OnClosing(e);
		}


		private void OnModelUpdated(object sender, EventArgs e) => SetCodeText();


		private async void SetCodeText()
		{
			CodeView.Text = "Getting code ...";

			R<string> codeResult = await dataDetailsService.GetCode(modelMetadata.ModelFilePath, nodeName);

			if (codeResult.IsFaulted)
			{
				message.ShowWarning(codeResult.Message);
				Close();
				return;
			}

			CodeView.Text = codeResult.Value;
		}


		private void SetSyntaxHighlighting()
		{
			Assembly programAssembly = ProgramInfo.Assembly;
			string name = programAssembly.FullName.Split(',')[0];
			string resourceName = $"{name}.Common.Resources.CSharp-Mode.xshd";

			using (Stream stream = programAssembly.GetManifestResourceStream(resourceName))
			using (XmlTextReader reader = new XmlTextReader(stream))
			{
				CodeView.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
			}
		}
	}
}
