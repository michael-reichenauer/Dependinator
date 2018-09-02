namespace Dependinator.Common
{
    public class ModelPaths
    {
        public string ModelPath { get; }

        public string WorkFolderPath { get; }


        public ModelPaths(string modelPath, string workFolderPath)
        {
            ModelPath = modelPath;
            WorkFolderPath = workFolderPath;
        }


        public override string ToString() => ModelPath;
    }
}
