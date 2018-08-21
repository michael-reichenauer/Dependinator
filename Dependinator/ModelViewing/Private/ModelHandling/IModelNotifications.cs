using System;


namespace Dependinator.ModelViewing.Private.ModelHandling
{
    internal interface IModelNotifications
    {
        event EventHandler ModelUpdated;
    }
}
