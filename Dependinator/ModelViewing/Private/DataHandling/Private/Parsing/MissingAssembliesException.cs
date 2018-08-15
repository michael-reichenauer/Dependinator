using System;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Parsing
{
    internal class MissingAssembliesException : Exception
    {
        public MissingAssembliesException(string msg) : base(msg)
        {
        }
    }
}