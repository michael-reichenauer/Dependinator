using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;


namespace Dependinator.ModelViewing.CodeViewing
{
	/// <summary>
	/// Interaction logic for ReferencesDialog.xaml
	/// </summary>
	public partial class CodeDialog : Window
	{
		internal CodeDialog(Window owner, string title, Lazy<string> codeText)
		{
			Owner = owner;
			InitializeComponent();

			DataContext = new CodeViewModel(title);

			SetSyntaxHighlighting();

			SetCodeText(codeText);
		}


		private async void SetCodeText(Lazy<string> codeText)
		{
			CodeView.Text = "Getting code ...";
			CodeView.Text = await Task.Run(() => codeText.Value);
		}


		private void SetSyntaxHighlighting()
		{
			Assembly programAssembly = Program.Assembly;
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
