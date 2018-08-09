using System.Threading.Tasks;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
    internal interface IModelPersistentHandler
    {
        bool IsChangeMonitored { get; set; }
        Task SaveIfModifiedAsync();
        void TriggerDataModified();
    }
}
