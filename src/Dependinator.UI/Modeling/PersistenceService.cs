using Dependinator.Core.Shared;
using Dependinator.UI.Modeling.Dtos;

namespace Dependinator.UI.Modeling;

interface IPersistenceService
{
    Task<Result> WriteAsync(string modelPath, ModelDto model);
    Task<Result<ModelDto>> ReadAsync(string path);
    Task<Result> WriteViewAsync(string modelPath, ViewDto view);
    Task<Result<ViewDto>> ReadViewAsync(string modelPath);
}

[Transient]
class PersistenceService(IFileService fileService) : IPersistenceService
{
    public Task<Result> WriteAsync(string modelPath, ModelDto model)
    {
        return Task.Run(async () =>
        {
            using var _ = Timing.Start($"Wrote model '{modelPath}'");
            return await fileService.WriteAsync(modelPath, model);
        });
    }

    public Task<Result> WriteViewAsync(string modelPath, ViewDto view) =>
        fileService.WriteAsync(ModelPaths.ViewPath(modelPath), view);

    public Task<Result<ViewDto>> ReadViewAsync(string modelPath) =>
        fileService.ReadAsync<ViewDto>(ModelPaths.ViewPath(modelPath));

    public Task<Result<ModelDto>> ReadAsync(string modelPath)
    {
        return Task.Run<Result<ModelDto>>(async () =>
        {
            using var _ = Timing.Start($"Read model '{modelPath}'");
            var readResult = await fileService.ReadAsync<ModelDto>(modelPath);
            if (readResult is not ModelDto model)
                return readResult.Error;
            if (model.FormatVersion != ModelDto.CurrentFormatVersion)
            {
                var error = new Error(
                    $"Cached model format version {model.FormatVersion} != {ModelDto.CurrentFormatVersion} (current)"
                );
                Log.Error(error.Message);
                return error;
            }

            return model;
        });
    }
}
