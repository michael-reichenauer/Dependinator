namespace DependinatorApi
{
    /// <summary>
    ///     Api published by the Dependinator.exe.
    ///     Called by other instances of Dependinator.exe or by the Dependinator Visual Studio extension.
    /// </summary>
    public interface IDependinatorApi
    {
        /// <summary>
        ///     Activate the studio main window to bring ti to the front.
        /// </summary>
        void Activate(string[] args);


        /// <summary>
        ///     Show the node that correspond to the specified file
        /// </summary>
        void ShowNodeForFile(string filePath, int lineNumber);
    }
}
