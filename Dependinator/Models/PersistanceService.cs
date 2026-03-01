using Dependinator.Core.Shared;

namespace Dependinator.Models;

interface IPersistenceService
{
    Task<R> WriteAsync(string modelPath, ModelDto model);
    Task<R<ModelDto>> ReadAsync(string path);
}

[Transient]
class PersistenceService(IFileService fileService) : IPersistenceService
{
    public Task<R> WriteAsync(string modelPath, ModelDto model)
    {
        return Task.Run(async () =>
        {
            // using var _ = Timing.Start($"Wrote model '{modelPath}'");
            await fileService.WriteAsync(modelPath, model);

            return R.Ok;
        });
    }

    public Task<R<ModelDto>> ReadAsync(string modelPath)
    {
        return Task.Run<R<ModelDto>>(async () =>
        {
            // using var _ = Timing.Start($"Read model '{modelPath}'");
            if (!Try(out var model, out var e2, await fileService.ReadAsync<ModelDto>(modelPath)))
                return e2;
            if (model.FormatVersion != ModelDto.CurrentFormatVersion)
            {
                var error = R.Error(
                    $"Cached model format version {model.FormatVersion} != {ModelDto.CurrentFormatVersion} (current)"
                );
                Log.Error(error.ErrorMessage);
                return error;
            }

            return model;
        });
    }
}
