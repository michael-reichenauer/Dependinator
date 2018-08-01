using System;


namespace Dependinator.Common.ModelMetadataFolders
{
    public class FileEventArgs : EventArgs
    {
        public FileEventArgs(DateTime dateTime)
        {
            DateTime = dateTime;
        }


        public DateTime DateTime { get; }
    }
}
