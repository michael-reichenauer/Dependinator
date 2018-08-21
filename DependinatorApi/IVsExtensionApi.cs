namespace DependinatorApi
{
    /// <summary>
    ///     Api published by the Dependinator Visual Studio extension.
    ///     Called by the Dependinator.exe, when triggering actions like Activate and ShowFile
    /// </summary>
    public interface IVsExtensionApi
    {
        /// <summary>
        ///     Activate the studio main window to bring ti to the front.
        /// </summary>
        void Activate();


        /// <summary>
        ///     Show the specified file in the studio
        /// </summary>
        void ShowFile(string filePath, int lineNumber);
    }
}
