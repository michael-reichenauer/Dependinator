using Microsoft.AspNetCore.Components;

namespace Dependinator.Shared;

interface IUIComponent
{
    ElementReference Ref { get; }
}
