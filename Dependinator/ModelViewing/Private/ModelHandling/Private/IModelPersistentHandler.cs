using System.Threading.Tasks;


namespace Dependinator.ModelViewing.Private.ModelHandling.Private
{
    internal interface IModelPersistentHandler
    {
        Task SaveIfModifiedAsync();

        void TriggerDataModified();
    }
}
