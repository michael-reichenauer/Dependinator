namespace Dependinator.Modeling.Commands;

abstract class Command
{
    static readonly TimeSpan compositeTimeout = TimeSpan.FromMilliseconds(500);

    public virtual DateTime TimeStamp => DateTime.UtcNow;
    public virtual string Type => this.GetType().Name;

    public abstract void Execute(IModel model);
    public abstract void Revert(IModel model);

    public bool CanCombineWith(Command other)
    {
        return Type == other.Type && TimeStamp - other.TimeStamp < compositeTimeout;
    }
}
