using Dependinator.Utils.UI;


namespace Dependinator.Common.ProgressHandling
{
    internal interface IBusyIndicatorProvider
    {
        BusyIndicator Busy { get; }
    }
}
