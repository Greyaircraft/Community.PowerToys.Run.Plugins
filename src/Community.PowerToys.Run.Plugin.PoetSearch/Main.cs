#nullable enable
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.PoetSearch
{
    /// <summary>
    /// PoetSearch - search classic Chinese poetry right inside PowerToys Run.
    /// Data: chinese-poetry (MIT), 唐诗三百首 + 千家诗.
    /// </summary>
    public sealed class Main : IPlugin, ISettingProvider, IDisposable
    {
        private readonly PoemData _data = new();
        private PluginInitContext? _context;
        private string _iconPath = string.Empty;

        /// <summary>Plugin display name shown in PowerToys Run.</summary>
        public string Name => "PoetSearch";

        /// <summary>Unique plugin ID.</summary>
        public static string PluginID => "7A3B9C2D4E5F60718293A4B5C6D7E8F9";

        /// <summary>Plugin description shown in PowerToys settings.</summary>
        public string Description => "搜索中国古诗词 · Search classic Chinese poetry (唐诗三百首, 千家诗)";

        /// <summary>Initializes the plugin, loads the poem database.</summary>
        /// <param name="context">Plugin initialization context.</param>
        public void Init(PluginInitContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(_context.API.GetCurrentTheme());

            // PowerToys loads plugins via Assembly.Load(byte[]), so Assembly.Location is empty.
            // Use the plugin metadata directory to locate poems.json reliably.
            var log = Path.Combine(Path.GetTempPath(), "poetsearch_debug.log");
            try
            {
                File.AppendAllText(log, $"[{DateTime.Now:HH:mm:ss.fff}] Init() called. metadata-null={context.CurrentPluginMetadata is null}\r\n");
                var dir = context.CurrentPluginMetadata?.PluginDirectory;
                File.AppendAllText(log, $"[{DateTime.Now:HH:mm:ss.fff}] PluginDirectory='{dir}'\r\n");
                if (!string.IsNullOrEmpty(dir))
                {
                    _data.SetPluginDirectory(dir);
                }
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(log, $"[{DateTime.Now:HH:mm:ss.fff}] metadata exception: {ex}\r\n"); } catch { }
            }

            _data.Load();
        }

        /// <summary>Searches the poem database for the given query.</summary>
        /// <param name="query">The PowerToys Run query (after the action keyword).</param>
        /// <returns>List of matching results.</returns>
        public List<Result> Query(Query query)
        {
            var search = (query.Search ?? string.Empty).Trim();

            // Empty query: show hint
            if (string.IsNullOrEmpty(search))
            {
                return new List<Result>
                {
                    BuildInfoResult(
                        "输入关键词搜索古诗词",
                        "例: poet 静夜思 · poet 李白 · poet 明月 · poet 随机  |  回车复制全诗")
                };
            }

            if (!_data.Loaded)
            {
                return new List<Result>
                {
                    BuildInfoResult("诗词数据库未加载", "请确认 poems.json 与插件在同一目录")
                };
            }

            var poems = _data.Search(search, maxResults: 8);

            if (poems.Count == 0)
            {
                return new List<Result>
                {
                    BuildInfoResult($"未找到与「{search}」相关的诗词", "换个关键词试试，例如：李白、明月、静夜思")
                };
            }

            return poems.Select(p => BuildPoemResult(p)).ToList();
        }

        private Result BuildPoemResult(Poem poem)
        {
            var subtitle = $"{poem.Author} · {poem.Source} · {poem.Paragraphs.Count}句";

            // Show first line as a preview subtitle when there's room
            var preview = poem.Paragraphs.FirstOrDefault() ?? string.Empty;

            return new Result
            {
                Title = $"{poem.Title} — {poem.Author}",
                SubTitle = string.IsNullOrEmpty(preview) ? subtitle : $"{subtitle} | {preview}",
                IcoPath = _iconPath,
                Score = 100,
                Action = _ =>
                {
                    // Copy full poem to clipboard
                    var text = $"{poem.Title} · {poem.Author}\n{poem.FullText}";
                    try
                    {
                        Clipboard.SetText(text);
                    }
                    catch
                    {
                        // Clipboard can fail silently (e.g. in session 0)
                    }
                    return true;
                }
            };
        }

        private Result BuildInfoResult(string title, string subtitle)
        {
            return new Result
            {
                Title = title,
                SubTitle = subtitle,
                IcoPath = _iconPath,
                Score = 0
            };
        }

        private void OnThemeChanged(Theme oldtheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        private void UpdateIconPath(Theme theme)
        {
            if (_context is null || _context.CurrentPluginMetadata is null)
            {
                return;
            }

            var iconFile = theme == Theme.Light || theme == Theme.HighContrastWhite
                ? "poet.light.png"
                : "poet.dark.png";

            _iconPath = Path.Combine(
                _context.CurrentPluginMetadata.PluginDirectory,
                "Images",
                iconFile);
        }

        /// <summary>No custom settings panel is provided.</summary>
        /// <returns>Null.</returns>
        public Control? CreateSettingPanel() => null;

        /// <summary>No additional options are provided.</summary>
        public IEnumerable<PluginAdditionalOption> AdditionalOptions => Enumerable.Empty<PluginAdditionalOption>();

        /// <summary>No settings to update.</summary>
        /// <param name="settings">Settings (unused).</param>
        public void UpdateSettings(PowerLauncherPluginSettings settings) { }

        /// <summary>Unsubscribes from theme change events.</summary>
        public void Dispose()
        {
            if (_context is not null)
            {
                _context.API.ThemeChanged -= OnThemeChanged;
            }
        }
    }
}
