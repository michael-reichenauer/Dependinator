using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dependinator.Common.SettingsHandling;
using Dependinator.Utils.Dependencies;


namespace Dependinator.Common.ModelMetadataFolders.Private
{
    [SingleInstance]
    internal class RecentModelsService : IRecentModelsService
    {
        private readonly IJumpListService jumpListService;
        private readonly ISettingsService settingsService;


        public RecentModelsService(
            ISettingsService settingsService,
            IJumpListService jumpListService)
        {
            this.settingsService = settingsService;
            this.jumpListService = jumpListService;
        }


        public event EventHandler Changed;


        public void AddModelPaths(string modelFilePath)
        {
            if (!File.Exists(modelFilePath))
            {
                return;
            }

            AddToResentPathInProgramSettings(modelFilePath);

            jumpListService.AddPath(modelFilePath);
            Changed?.Invoke(this, EventArgs.Empty);
        }


        public IReadOnlyList<string> GetModelPaths()
        {
            ProgramSettings settings = settingsService.Get<ProgramSettings>();

            return settings.ResentModelPaths.Where(File.Exists).ToList();
        }


        public void RemoveModelPath(string modelFilePath)
        {
            List<string> resentPaths = settingsService.Get<ProgramSettings>().ResentModelPaths.ToList();
            int index = resentPaths.FindIndex(path => path.IsSameIc(modelFilePath));

            if (index != -1)
            {
                resentPaths.RemoveAt(index);
            }

            settingsService.Edit<ProgramSettings>(s => { s.ResentModelPaths = resentPaths; });
        }


        private void AddToResentPathInProgramSettings(string modelFilePath)
        {
            ProgramSettings settings = settingsService.Get<ProgramSettings>();

            List<string> resentPaths = settings.ResentModelPaths
                .Where(File.Exists).ToList();
            int index = resentPaths.FindIndex(path => path.IsSameIc(modelFilePath));

            if (index != -1)
            {
                resentPaths.RemoveAt(index);
            }

            resentPaths.Insert(0, modelFilePath);

            if (resentPaths.Count > 10)
            {
                resentPaths.RemoveRange(10, resentPaths.Count - 10);
            }

            settingsService.Edit<ProgramSettings>(s => { s.ResentModelPaths = resentPaths; });
        }
    }
}
