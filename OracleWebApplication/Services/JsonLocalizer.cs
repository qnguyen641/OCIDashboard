using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace OracleWebApplication.Services;

/// <summary>
/// Simple JSON-file-based localiser. Reads wwwroot/locales/{culture}.json
/// and returns translated strings by key. Falls back to en.json.
/// </summary>
public class JsonLocalizer
{
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _cache = new();
    private readonly string _localesPath;

    public JsonLocalizer(IWebHostEnvironment env)
    {
        _localesPath = Path.Combine(env.WebRootPath, "locales");
    }

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        var culture = CultureInfo.CurrentUICulture.Name; // e.g. "zh-CN" or "en"
        var dict = LoadDictionary(culture);

        if (dict.TryGetValue(key, out var value))
            return value;

        // fallback to base language (e.g. "zh-CN" → "zh")
        if (culture.Contains('-'))
        {
            var baseCulture = culture.Split('-')[0];
            dict = LoadDictionary(baseCulture);
            if (dict.TryGetValue(key, out value))
                return value;
        }

        // fallback to English
        if (culture != "en")
        {
            dict = LoadDictionary("en");
            if (dict.TryGetValue(key, out value))
                return value;
        }

        return key; // return the key itself as last resort
    }

    public string Get(string key, params object[] args)
    {
        var template = Get(key);
        return string.Format(template, args);
    }

    private Dictionary<string, string> LoadDictionary(string culture)
    {
        return _cache.GetOrAdd(culture, c =>
        {
            var filePath = Path.Combine(_localesPath, $"{c}.json");
            if (!File.Exists(filePath))
                return new Dictionary<string, string>();

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>();
        });
    }
}
