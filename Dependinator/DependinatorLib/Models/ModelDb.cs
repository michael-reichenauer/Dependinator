namespace Dependinator.Models;

interface IModelDb
{
    Model GetModel();
}


[Singleton]
class ModelDb : IModelDb
{
    readonly ModelBase model = new();

    public Model GetModel() => new(model);
}


