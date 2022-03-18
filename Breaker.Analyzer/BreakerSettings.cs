using System.IO;
using System.Text.Json;

namespace Breaker.Analyzer;

public class BreakerSettings
{
    public string GitRef { get; set; }

    public static BreakerSettings Parse(string filePath)
        => JsonSerializer.Deserialize<BreakerSettings>(File.ReadAllText(filePath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
}