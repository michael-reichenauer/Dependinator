using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dependinator.ModelViewing.Private.DataHandling.Dtos;
using Dependinator.Utils.ErrorHandling;


namespace Dependinator.ModelViewing.Private.DataHandling.Private.Persistence
{
    internal interface IPersistenceService
    {
        Task<R> TryDeserialize(string path, DataItemsCallback dataItemsCallback);

        Task SaveAsync(IReadOnlyList<IDataItem> items);
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
