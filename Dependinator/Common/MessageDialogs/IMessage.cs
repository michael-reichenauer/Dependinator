namespace Dependinator.Common.MessageDialogs
{
    internal interface IMessage
    {
        void ShowInfo(string message, string title = null);

        bool ShowAskOkCancel(string message, string title = null);

        void ShowWarning(string message, string title = null);

        bool ShowWarningAskYesNo(string message, string title = null);

        void ShowError(string message, string title = null);
    }
}
