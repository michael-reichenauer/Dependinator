namespace DependinatorCore;

public sealed class CoreExample
{
    public string GetGreeting(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Hello from DependinatorCore.";
        }

        return $"Hello {name} from DependinatorCore.";
    }
}
