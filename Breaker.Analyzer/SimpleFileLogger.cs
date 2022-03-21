using System;
using System.IO;

namespace Breaker.Analyzer;

public class SimpleFileLogger
{
    private readonly string filePath;

    public SimpleFileLogger(string filePath)
    {
        this.filePath = filePath;
        var directoryPath = Path.GetDirectoryName(this.filePath);
        if (directoryPath is not null && !Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);
    }

    public void Log(string message)
    {
        File.AppendAllText(this.filePath, $@"[{DateTime.Now}] {message}{Environment.NewLine}");
    }
}