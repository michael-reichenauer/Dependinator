using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using Dependinator.Common.MessageDialogs;
using Dependinator.Utils.ErrorHandling;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;


namespace Dependinator.ModelViewing.CodeViewing
{
	/// <summary>
	/// Interaction logic for ReferencesDialog.xaml
	/// </summary>
	public partial class CodeDialog : Window
	{
		private readonly IMessage message;


		internal CodeDialog(Window owner, IMessage message, string title, Task<R<string>> codeTask)
		{
			this.message = message;
			Owner = owner;
			InitializeComponent();

			DataContext = new CodeViewModel(title);

			SetSyntaxHighlighting();

			SetCodeText(codeTask);
		}


		private async void SetCodeText(Task<R<string>> codeTask)
		{
			CodeView.Text = "Getting code ...";

			R<string> codeResult = await codeTask;

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
