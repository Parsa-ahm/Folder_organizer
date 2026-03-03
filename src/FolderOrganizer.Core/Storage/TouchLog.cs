using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FolderOrganizer.Core.Storage;

/// <summary>
/// Tracks every path the app has modified, enabling "Clear All" from the settings app.
/// </summary>
public class TouchLog
{
    private readonly string _logPath;

    public TouchLog(string appDataPath)
    {
        _logPath = Path.Combine(appDataPath, "touched.log");
    }

    public void Record(string path)
    {
        try
        {
            File.AppendAllText(_logPath, path + Environment.NewLine);
        }
        catch { }
    }

    public IEnumerable<string> GetAllTouchedPaths()
    {
        try
        {
            if (!File.Exists(_logPath))
                return Enumerable.Empty<string>();

            return File.ReadAllLines(_logPath)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct();
        }
        catch
        {
            return Enumerable.Empty<string>();
        }
    }

    public void Clear()
    {
        try { File.Delete(_logPath); } catch { }
    }
}
