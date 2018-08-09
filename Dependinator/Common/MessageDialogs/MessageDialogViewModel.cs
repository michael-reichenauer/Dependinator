using System.Windows;
using Dependinator.Utils.UI.Mvvm;


namespace Dependinator.Common.MessageDialogs
{
    internal class MessageDialogViewModel : ViewModel
    {
        public Command<Window> OkCommand => Command<Window>(SetOK);
        public Command<Window> CancelCommand => Command<Window>(w => w.DialogResult = false);

        public string Title { get => Get(); set => Set(value); }

        public string Message { get => Get(); set => Set(value); }

        public bool IsInfo { get => Get(); set => Set(value); }

        public bool IsQuestion { get => Get(); set => Set(value); }

        public bool IsWarn { get => Get(); set => Set(value); }

        public bool IsError { get => Get(); set => Set(value); }

        public string OkText { get => Get(); set => Set(value); }

        public string CancelText { get => Get(); set => Set(value); }

        public bool IsCancelVisible { get => Get(); set => Set(value); }

        private void SetOK(Window window) => window.DialogResult = true;
    }
}
