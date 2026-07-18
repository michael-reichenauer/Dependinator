using Dependinator.Core.Shared;

namespace Dependinator.UI.App;

// Validates names for new manually designed models. The name is used as the model path,
// so it must not look like a parseable solution path and must not collide with an existing
// model (compared by cloud blob key, which also prevents case/whitespace-only variants).
static class NewModelNameValidator
{
    public static string? Validate(string name, IEnumerable<string> existingPaths)
    {
        string trimmedName = name.Trim();
        if (trimmedName == "")
            return "Name is required";
        if (trimmedName.Contains('/') || trimmedName.Contains('\\'))
            return "Name cannot contain '/' or '\\'";
        if (ModelPaths.IsParseable(trimmedName))
            return "Name cannot end with '.sln'";
        if (!TryCreateKey(trimmedName, out string? nameKey))
            return "Name is not valid";

        foreach (string existingPath in existingPaths)
        {
            if (TryCreateKey(existingPath, out string? existingKey) && existingKey == nameKey)
                return $"A model named '{Path.GetFileName(existingPath)}' already exists";
        }

        return null;
    }

    static bool TryCreateKey(string path, out string? key)
    {
        try
        {
            key = global::Shared.CloudModelPath.CreateKey(path);
            return true;
        }
        catch (ArgumentException)
        {
            key = null;
            return false;
        }
    }
}
