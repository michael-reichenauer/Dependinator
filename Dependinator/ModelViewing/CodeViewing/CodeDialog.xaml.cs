using System.IO;
using System.Reflection;
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

			SetSyntaxHighlighting();

			CodeView.Text = node.CodeText?.Value;
		}


		private void SetSyntaxHighlighting()
		{
			Assembly executingAssembly = Assembly.GetExecutingAssembly();
			string resourceName = $"{Product.Name}.Common.Resources.CSharp-Mode.xshd";

			using (Stream stream = executingAssembly.GetManifestResourceStream(resourceName))
			using (XmlTextReader reader = new XmlTextReader(stream))
			{
				CodeView.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
			}
		}
	}
}
