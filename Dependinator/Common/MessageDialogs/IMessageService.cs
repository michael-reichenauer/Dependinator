using System.Media;
using System.Windows;


namespace Dependinator.Common.MessageDialogs
{
    internal class MessageService : IMessage
    {
        private readonly WindowOwner owner;


        public MessageService(WindowOwner owner)
        {
            this.owner = owner;
        }


        public void ShowInfo(string message, string title = null) =>
            Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);


        public bool ShowAskOkCancel(string message, string title = null) =>
            Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Question) == true;


        public void ShowWarning(string message, string title = null) =>
            Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);


        public bool ShowWarningAskYesNo(string message, string title = null) =>
            Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == true;


        public void ShowError(string message, string title = null)
        {
            SystemSounds.Beep.Play();
            Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }


        private bool? Show(string text, string title, MessageBoxButton button, MessageBoxImage image)
        {
            return Message.ShowDialog(owner, text, title, button, image);
        }
    }
}
