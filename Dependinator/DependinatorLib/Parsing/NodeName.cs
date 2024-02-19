namespace Dependinator.Parsing;

internal class NodeName : Equatable<NodeName>
{
    record DisplayParts(string ShortName, string LongName);

    public static NodeName Root = From("");
    static readonly char[] PartsSeparators = "./".ToCharArray();
    readonly Lazy<DisplayParts> displayParts;

    readonly Lazy<NodeName> parentName;


    NodeName(string fullName)
    {
        FullName = fullName;

        parentName = new Lazy<NodeName>(GetParentName);
        displayParts = new Lazy<DisplayParts>(GetDisplayParts);

        IsEqualWhenSame(fullName);
    }


    public string FullName { get; }
    public NodeName ParentName => parentName.Value;
    public string DisplayShortName => displayParts.Value.ShortName;
    public string DisplayLongName => displayParts.Value.LongName;


    public static NodeName From(string fullName)
    {
        return new NodeName(fullName);
    }


    public bool IsSame(string nameText) => nameText == FullName;

    public override string ToString() => this != Root ? FullName : "<root>";

    // public static implicit operator NodeName(DataNodeName dataName) => new NodeName((string)dataName);
    // public static implicit operator DataNodeName(NodeName nodeName) => (DataNodeName)nodeName.FullName;


    NodeName GetParentName()
    {
        // Split full name in name and parent name,
        int index = FullName.LastIndexOfAny(PartsSeparators);

        return index > -1 ? From(FullName[..index]) : Root;
    }


    DisplayParts GetDisplayParts()
    {
        string name = "";

        string[] parts = FullName.Split(PartsSeparators);

        string namePart = parts[parts.Length - 1];
        int index = namePart.IndexOf('(');
        if (index > -1)
        {
            name = namePart.Substring(0, index);
        }
        else
        {
            name = namePart;
        }

        name = ToNiceText(name);


        var subParts = FullName.StartsWith("$") ? parts : parts.Skip(1);
        string fullName = string.Join(".", subParts
            .Where(part => !part.StartsWith("?")));

        if (string.IsNullOrEmpty(fullName))
        {
            fullName = ToNiceText(FullName);
        }
        else
        {
            fullName = ToNiceText(fullName);
            fullName = ToNiceParameters(fullName);
        }

        return new DisplayParts(name, fullName);
    }


    static string ToNiceParameters(string fullName)
    {
        int index1 = fullName.IndexOf('(');
        int index2 = fullName.IndexOf(')');

        if (index1 > -1 && index2 > index1 + 1)
        {
            string parameters = fullName.Substring(index1 + 1, index2 - index1 - 1);
            string[] parametersParts = parameters.Split(",".ToCharArray());

            // Simplify parameter types to just get last part of each type
            parameters = string.Join(",", parametersParts
                .Select(part => part.Split(".".ToCharArray()).Last()));

            fullName = $"{fullName.Substring(0, index1)}({parameters})";
        }

        return fullName;
    }


    static string ToNiceText(string name)
    {
        return name.Replace("*", ".")
            .Replace("#", ".")
            .Replace("?", "")
            .Replace("$", "")
            .Replace("%", "")
            .Replace("/", ".")
            .Replace("`1", "<T>")
            .Replace("`2", "<T,T>")
            .Replace("`3", "<T,T,T>")
            .Replace("`4", "<T,T,T,T>")
            .Replace("`5", "<T,T,T,T,T>")
            .Replace("op_Equality", "==")
            .Replace("op_Inequality", "!=");
    }
}

