namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing.Private.Parsers
{
    public class LinkData
    {
        public string Source { get; }
        public string Target { get; }
        public string TargetType { get; }


        public LinkData(string source, string target, string targetType)
        {
            Source = source;
            Target = target;
            TargetType = targetType;
        }


        public override string ToString() => $"{Source}->{Target}";
    }
}
