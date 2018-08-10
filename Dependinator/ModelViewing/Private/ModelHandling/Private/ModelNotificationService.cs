using System;
using Dependinator.Utils.Dependencies;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
    [SingleInstance]
    internal class ModelNotificationService : IModelNotificationService, IModelNotifications
    {
        public void TriggerNotification()
        {
            ModelUpdated?.Invoke(this, EventArgs.Empty);
        }


        public event EventHandler ModelUpdated;
    }
}