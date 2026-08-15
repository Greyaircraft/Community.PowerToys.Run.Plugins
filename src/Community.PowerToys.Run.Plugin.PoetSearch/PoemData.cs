#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Community.PowerToys.Run.Plugin.PoetSearch
{
    /// <summary>
    /// A single poem entry.
    /// </summary>
    public sealed class Poem
    {
        /// <summary>Poem title (or 词牌名 for 宋词).</summary>
        [System.Text.Json.Serialization.JsonPropertyName("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>Poem author.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("author")]
        public string Author { get; set; } = string.Empty;

        /// <summary>Poem lines/paragraphs.</summary>
        [System.Text.Json.Serialization.JsonPropertyName("paragraphs")]
        public List<string> Paragraphs { get; set; } = new();

        /// <summary>Source collection name (e.g. 全唐诗, 宋词).</summary>
        [System.Text.Json.Serialization.JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        /// <summary>Full text with all paragraphs joined (precomputed at load time for speed).</summary>
        public string FullText { get; private set; } = string.Empty;

        /// <summary>Called after deserialization to precompute the full text once.</summary>
        public void Prepare()
        {
            FullText = string.Join("\n", Paragraphs);
        }
    }

    /// <summary>
    /// Loads the poem database from the embedded poems.json and provides search.
    /// </summary>
    public sealed class PoemData
    {
        private readonly List<Poem> _poems = new();
        private string? _pluginDirectory;

        /// <summary>Number of loaded poems.</summary>
        public int Count => _poems.Count;

        /// <summary>Whether the poem database was loaded successfully.</summary>
        public bool Loaded => _poems.Count > 0;

        /// <summary>
        /// Sets the plugin deployment directory (from PluginInitContext.CurrentPluginMetadata.PluginDirectory).
        /// </summary>
        public void SetPluginDirectory(string? dir)
        {
            _pluginDirectory = dir;
        }

        /// <summary>
        /// Locates the poem database file (poems.json or poems.json.gz) in the plugin folder
        /// or nearby locations. PowerToys loads plugins via Assembly.Load(byte[]), so
        /// Assembly.Location may be empty -- all known locations are tried.
        /// </summary>
        /// <returns>Full path to the database file, or null if not found.</returns>
        public static string? LocateDatabase()
        {
            // Candidates in priority order. PowerToys loads plugins via Assembly.Load(byte[]),
            // so Assembly.Location may be empty -- try all known locations.
            var candidates = new List<string>();

            // 1. Same folder as this DLL (plugin deployment folder) -- .json or .json.gz
            var dllDir = Path.GetDirectoryName(typeof(PoemData).Assembly.Location);
            if (!string.IsNullOrEmpty(dllDir))
            {
                candidates.Add(Path.Combine(dllDir, "poems.json"));
                candidates.Add(Path.Combine(dllDir, "poems.json.gz"));
            }

            // 2. AppContext.BaseDirectory (usually the PowerToys main dir when byte-loaded)
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "poems.json"));
            candidates.Add(Path.Combine(AppContext.BaseDirectory, "poems.json.gz"));

            // 3. Parent of base dir (e.g. ...\PowerToys Run\Plugins\PoetSearch\ vs base ...\PowerToys Run)
            try
            {
                var baseDir = Path.GetFullPath(AppContext.BaseDirectory);
                var parentDir = Directory.GetParent(baseDir);
                if (parentDir != null)
                {
                    candidates.Add(Path.Combine(parentDir.FullName, "PoetSearch", "poems.json"));
                    candidates.Add(Path.Combine(parentDir.FullName, "PoetSearch", "poems.json.gz"));
                }
            }
            catch (Exception)
            {
                // ignore
            }

            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Loads the poem database from a JSON (or gzip-compressed JSON) file.
        /// </summary>
        /// <param name="path">Explicit file path; when null the database is auto-located.</param>
        /// <returns>True when at least one poem was loaded.</returns>
        public bool Load(string? path = null)
        {
            var log = Path.Combine(Path.GetTempPath(), "poetsearch_debug.log");
            void Log(string msg)
            {
                try { File.AppendAllText(log, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\r\n"); } catch { }
            }

            try
            {
                Log($"Load() called. path='{path}' _pluginDirectory='{_pluginDirectory}'");
                Log($"Assembly.Location='{typeof(PoemData).Assembly.Location}'");
                Log($"AppContext.BaseDirectory='{AppContext.BaseDirectory}'");
            }
            catch { }

            var dbPath = path ?? LocateDatabase();
            if (string.IsNullOrEmpty(dbPath) && !string.IsNullOrEmpty(_pluginDirectory))
            {
                dbPath = Path.Combine(_pluginDirectory, "poems.json");
                if (!File.Exists(dbPath))
                {
                    dbPath = Path.Combine(_pluginDirectory, "poems.json.gz");
                }
                try { Log($"Falling back to plugin dir: {dbPath}"); } catch { }
            }
            if (string.IsNullOrEmpty(dbPath))
            {
                dbPath = LocateDatabase();
            }
            try { Log($"dbPath='{dbPath}' exists={!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath)}"); } catch { }
            if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
            {
                return false;
            }

            try
            {
                string json;
                if (dbPath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                {
                    using var gz = new System.IO.Compression.GZipStream(
                        File.OpenRead(dbPath), System.IO.Compression.CompressionMode.Decompress);
                    using var reader = new StreamReader(gz, System.Text.Encoding.UTF8);
                    json = reader.ReadToEnd();
                }
                else
                {
                    json = File.ReadAllText(dbPath);
                }
                var items = JsonSerializer.Deserialize<List<Poem>>(json);
                if (items is null)
                {
                    Log($"Deserialize returned null");
                    return false;
                }

                _poems.Clear();
                _poems.AddRange(items);
                foreach (var poem in _poems)
                {
                    poem.Prepare();
                }
                Log($"SUCCESS: loaded {_poems.Count} poems from {dbPath}");
                return _poems.Count > 0;
            }
            catch (Exception ex)
            {
                Log($"EXCEPTION: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Search by title, author, or content. Returns up to <paramref name="maxResults"/> results,
        /// best matches first.
        /// </summary>
        public List<Poem> Search(string query, int maxResults = 8)
        {
            var q = query.Trim();
            if (string.IsNullOrEmpty(q))
            {
                return new List<Poem>();
            }

            // Special command: random poem
            if (q.Equals("随机", StringComparison.Ordinal) ||
                q.Equals("random", StringComparison.OrdinalIgnoreCase))
            {
                var rng = new Random();
                return new List<Poem> { _poems[rng.Next(_poems.Count)] };
            }

            var results = new List<(Poem Poem, int Score)>();

            // Two-phase search: first scan cheap fields (title/author). If enough hits, skip
            // the expensive full-text scan for 78k poems.
            foreach (var poem in _poems)
            {
                int score = 0;

                // Title match is strongest
                if (poem.Title.Contains(q, StringComparison.Ordinal))
                {
                    score += 100;
                    if (poem.Title.Equals(q, StringComparison.Ordinal))
                    {
                        score += 50; // exact title match
                    }
                }

                // Author match
                if (poem.Author.Contains(q, StringComparison.Ordinal))
                {
                    score += 60;
                    if (poem.Author.Equals(q, StringComparison.Ordinal))
                    {
                        score += 30;
                    }
                }

                if (score > 0)
                {
                    results.Add((poem, score));
                }
            }

            // Only scan full text when title/author hits are few (e.g. searching a phrase).
            if (results.Count < maxResults * 4)
            {
                foreach (var poem in _poems)
                {
                    if (poem.FullText.Contains(q, StringComparison.Ordinal))
                    {
                        results.Add((poem, 30));
                    }
                }
            }

            return results
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Poem.Title, StringComparer.Ordinal)
                .Take(maxResults)
                .Select(r => r.Poem)
                .ToList();
        }
    }
}
