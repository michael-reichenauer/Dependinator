using Microsoft.AspNetCore.Components;

namespace Dependinator.UI.Shared;

interface IUIComponent
{
    ElementReference Ref { get; }
}
