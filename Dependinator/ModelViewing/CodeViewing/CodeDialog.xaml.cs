using System.IO;
using System.Windows;
using System.Xml;
using Dependinator.ModelViewing.ModelHandling.Core;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;


namespace Dependinator.ModelViewing.CodeViewing
{
	/// <summary>
	/// Interaction logic for ReferencesDialog.xaml
	/// </summary>
	public partial class CodeDialog : Window
	{
		internal CodeDialog(Window owner, Node node)
		{
			Owner = owner;
			InitializeComponent();

			DataContext = new CodeViewModel(node);

			string path = @"C:\Work Files\CodeViewer\CodeViewer\Custom-CSharp-Mode.xshd";

			using (Stream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
			using (XmlTextReader reader = new XmlTextReader(stream))
			{
				CodeView.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
			}

			CodeView.Text = node.CodeText?.Value;
		}
	}
}
