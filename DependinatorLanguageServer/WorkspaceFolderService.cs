using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Dependinator.Shared.Utils.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace DependinatorLanguageServer;

public class WorkspaceFolderService
{
    const int DefaultMaxDepth = 3;
    const int DefaultMaxFilesPerRoot = 25;
    const long MaxContentBytes = 256 * 1024;
    static readonly HashSet<string> IgnoredDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".svn",
        ".hg",
        "node_modules",
        "bin",
        "obj",
        ".vs",
    };

    readonly object gate = new();
    readonly HashSet<string> roots = new(StringComparer.OrdinalIgnoreCase);

    public void InitializeFrom(InitializeParams initializeParams, CancellationToken ct)
    {
        var rootPaths = new List<string>();

        if (initializeParams.WorkspaceFolders is not null && initializeParams.WorkspaceFolders.Count() > 0)
        {
            rootPaths.AddRange(
                initializeParams
                    .WorkspaceFolders.Select(folder => folder.Uri.GetFileSystemPath())
                    .Where(path => !string.IsNullOrWhiteSpace(path))
            );
        }
        else if (initializeParams.RootUri is not null)
        {
            var path = initializeParams.RootUri.GetFileSystemPath();
            if (!string.IsNullOrWhiteSpace(path))
                rootPaths.Add(path);
        }
        else if (!string.IsNullOrWhiteSpace(initializeParams.RootPath))
        {
            rootPaths.Add(initializeParams.RootPath);
        }

        ReplaceRoots(rootPaths);
        Log.Info("Workspace folders initialized", rootPaths);
        LogSampleFileScan(rootPaths, DefaultMaxDepth, DefaultMaxFilesPerRoot, ct);
    }

    public IReadOnlyCollection<string> GetRoots()
    {
        lock (gate)
        {
            return roots.ToArray();
        }
    }

    public void AddFolders(IEnumerable<WorkspaceFolder> folders, CancellationToken ct)
    {
        var added = GetPaths(folders);
        if (added.Count == 0)
            return;

        lock (gate)
        {
            foreach (var path in added)
                roots.Add(path);
        }

        Log.Info("Workspace folders added", added);
        LogSampleFileScan(added, DefaultMaxDepth, DefaultMaxFilesPerRoot, ct);
    }

    public void RemoveFolders(IEnumerable<WorkspaceFolder> folders)
    {
        var removed = GetPaths(folders);
        if (removed.Count == 0)
            return;

        lock (gate)
        {
            foreach (var path in removed)
                roots.Remove(path);
        }

        Log.Info("Workspace folders removed", removed);
    }

    void ReplaceRoots(IEnumerable<string> rootPaths)
    {
        lock (gate)
        {
            roots.Clear();
            foreach (var path in rootPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
                roots.Add(path);
        }
    }

    static List<string> GetPaths(IEnumerable<WorkspaceFolder> folders)
    {
        return folders
            .Select(folder => folder.Uri.GetFileSystemPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();
    }

    static void LogSampleFileScan(
        IEnumerable<string> rootPaths,
        int maxDepth,
        int maxFilesPerRoot,
        CancellationToken ct
    )
    {
        foreach (var rootPath in rootPaths)
        {
            if (ct.IsCancellationRequested)
                return;

            if (!Directory.Exists(rootPath))
            {
                Log.Warn($"Workspace folder not found: {rootPath}");
                continue;
            }

            var fileCount = 0;
            foreach (var filePath in EnumerateFiles(rootPath, maxDepth, ct))
            {
                if (ct.IsCancellationRequested)
                    return;

                if (fileCount++ >= maxFilesPerRoot)
                    break;

                TryLogFile(filePath);
            }
        }
    }

    static IEnumerable<string> EnumerateFiles(string rootPath, int maxDepth, CancellationToken ct)
    {
        var stack = new Stack<(string path, int depth)>();
        stack.Push((rootPath, 0));

        while (stack.Count > 0)
        {
            if (ct.IsCancellationRequested)
                yield break;

            var (currentPath, depth) = stack.Pop();
            if (depth > maxDepth)
                continue;

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(currentPath);
            }
            catch (Exception ex)
            {
                Log.Warn($"Unable to read files in {currentPath}: {ex.Message}");
                continue;
            }

            foreach (var file in files)
                yield return file;

            if (depth == maxDepth)
                continue;

            IEnumerable<string> directories;
            try
            {
                directories = Directory.EnumerateDirectories(currentPath);
            }
            catch (Exception ex)
            {
                Log.Warn($"Unable to read directories in {currentPath}: {ex.Message}");
                continue;
            }

            foreach (var directory in directories)
            {
                var name = Path.GetFileName(directory);
                if (IgnoredDirectoryNames.Contains(name))
                    continue;

                stack.Push((directory, depth + 1));
            }
        }
    }

    static void TryLogFile(string filePath)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            var size = fileInfo.Exists ? fileInfo.Length : 0;
            var contentLength = TryReadContentLength(filePath, size);

            Log.Info("Workspace file", filePath, size, contentLength);
        }
        catch (Exception ex)
        {
            Log.Warn($"Unable to read file {filePath}: {ex.Message}");
        }
    }

    static int TryReadContentLength(string filePath, long fileSize)
    {
        if (fileSize <= 0 || fileSize > MaxContentBytes)
            return -1;

        try
        {
            var content = File.ReadAllText(filePath);
            return content.Length;
        }
        catch
        {
            return -1;
        }
    }
}
