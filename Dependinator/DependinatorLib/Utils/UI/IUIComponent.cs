using Microsoft.AspNetCore.Components;

namespace Dependinator.Utils.UI;

interface IUIComponent
{
    ElementReference Ref { get; }
    Task TriggerStateHasChangedAsync();

}
