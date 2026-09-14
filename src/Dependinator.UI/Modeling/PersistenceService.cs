using Dependinator.Core.Shared;
using Dependinator.UI.Modeling.Dtos;

namespace Dependinator.UI.Modeling;

interface IPersistenceService
{
    Task<Result> WriteAsync(string modelPath, ModelDto model);
    Task<Result<ModelDto>> ReadAsync(string path);
}

[Transient]
class PersistenceService(IFileService fileService) : IPersistenceService
{
    public Task<Result> WriteAsync(string modelPath, ModelDto model)
    {
        return Task.Run(async () =>
        {
            using var _ = Timing.Start($"Wrote model '{modelPath}'");
            await fileService.WriteAsync(modelPath, model);

            return Result.Ok;
        });
    }

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
