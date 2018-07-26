using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence
{
    internal interface IPersistenceService
    {
        void Serialize(IReadOnlyList<IDataItem> items, string path);

        Task<R> TryDeserialize(string path, DataItemsCallback dataItemsCallback);
    }


    internal class MissingDataFileException : Exception
    {
        public MissingDataFileException(string msg) : base(msg)
        {
        }
    }


    internal class InvalidDataFileException : Exception
    {
        public InvalidDataFileException(string msg, Exception inner = null) : base(msg, inner)
        {
        }
    }
}
