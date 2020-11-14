using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FolderSync
{
    public class Settings
    {
        public string SourceFolder { get; set; }

        public string DestinationFolder { get; set; }

        public string[] ExcludeFolders { get; set; } = { };

        public string[] ExcludeFiles { get; set; } = { };

        public bool Mirror { get; set; }

        public bool Verbose { get; set; }

        internal Regex[] _excludeFiles;

        internal Regex[] _excludeFolders;

        static Regex ToRegex(string pattern)
        {
            return new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
        }

        public void Compile()
        {
            _excludeFiles = ExcludeFiles.Select(ToRegex).ToArray();
            _excludeFolders = ExcludeFolders.Select(ToRegex).ToArray();
        }

        public bool Parse(string[] args)
        {
            if (args.Length < 2)
            {
                return false;
            }

            SourceFolder = args[0];
            DestinationFolder = args[1];

            if (args.Length > 2)
            {
                for (var i = 2; i < args.Length; i += 2)
                {
                    if (args.Length < i + 1)
                        return false;

                    if (string.Equals(args[i], "/xf", StringComparison.OrdinalIgnoreCase))
                    {
                        ExcludeFiles = args[i + 1].Split(';');
                    }
                    else if (string.Equals(args[i], "/xd", StringComparison.OrdinalIgnoreCase))
                    {
                        ExcludeFolders = args[i + 1].Split(';');
                    }
                    else if (string.Equals(args[i], "/mirror", StringComparison.OrdinalIgnoreCase))
                    {
                        Mirror = true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    public class Sync
    {
        public void Perform(Settings settings)
        {
            settings.Compile();

            if (!Directory.Exists(settings.SourceFolder))
            {
                if (settings.Verbose)
                    Console.WriteLine($"Source folder {settings.SourceFolder} not found.");

                return;
            }

            var op = new FolderOperation
            {
                Source = Path.GetFullPath(settings.SourceFolder),
                Target = Path.GetFullPath(settings.DestinationFolder)
            };

            op.Perform(settings).Wait();
        }
    }

    interface IOperation
    {
        Task Perform(Settings settings);
    }

    class FolderOperation : IOperation
    {
        public string Source { get; set; }

        public string Target { get; set; }

        public IEnumerable<IOperation> Feed(Settings settings)
        {
            return CopyFiles(settings).Concat(CopyFolders(settings));
        }

        public async Task Perform(Settings settings)
        {
            if (!Directory.Exists(Target))
            {
                Console.WriteLine($"+{Target}");
                Directory.CreateDirectory(Target);
            }

            var tasks = Feed(settings).Select(x => x.Perform(settings));

            await Task.WhenAll(tasks);
        }

        private IDictionary<string, string> ToRelative(IEnumerable<string> items, Regex[] excludes)
        {
            return items
                .Where(x => !excludes.Any(r => r.IsMatch(Path.GetFileName(x))))
                .ToDictionary(Path.GetFileName, x => x);
        }

        private IEnumerable<IOperation> CopyFolders(Settings settings)
        {
            if (Directory.Exists(Source))
            {
                var sourceFolders = ToRelative(Directory.EnumerateDirectories(Source), settings._excludeFolders);
                var targetFolders = ToRelative(Directory.EnumerateDirectories(Target), settings._excludeFolders);

                foreach (var source in sourceFolders)
                {
                    var targetFullPath = Path.Combine(Target, source.Key);

                    if (settings.Verbose)
                        Console.WriteLine($"Comparing {Source} -> {Target}");

                    yield return new FolderOperation
                    {
                        Source = source.Value,
                        Target = targetFullPath
                    };

                    targetFolders.Remove(source.Key);
                }

                if (settings.Mirror)
                {
                    foreach (var obsoleteFolderPath in targetFolders.Values)
                    {
                        yield return new FolderDelete
                        {
                            Path = obsoleteFolderPath
                        };
                    }
                }
            }
        }

        private IEnumerable<IOperation> CopyFiles(Settings settings)
        {
            var sourceFiles = ToRelative(Directory.EnumerateFiles(Source), settings._excludeFiles);
            var targetFiles = ToRelative(Directory.EnumerateFiles(Target), settings._excludeFiles);

            foreach (var source in sourceFiles)
            {
                var targetFullPath = Path.Combine(Target, source.Key);

                if (settings.Verbose)
                    Console.WriteLine($"Comparing {source.Value} -> {targetFullPath}");

                if (!targetFiles.ContainsKey(source.Key) || File.GetLastWriteTime(source.Value) > File.GetLastWriteTime(targetFullPath))
                {
                    yield return new FileCopy
                    {
                        Source = source.Value,
                        Target = targetFullPath
                    };
                }

                targetFiles.Remove(source.Key);
            }

            if (settings.Mirror)
            {
                foreach (var obsoleteFile in targetFiles.Values)
                {
                    yield return new FileDelete
                    {
                        Path = obsoleteFile
                    };
                }
            }
        }
    }

    class FileCopy : IOperation
    {
        public string Source { get; set; }

        public string Target { get; set; }

        public async Task Perform(Settings settings)
        {
            await Task.Run(() =>
            {
                Console.WriteLine($"={Source} -> {Target}");

                File.Copy(Source, Target, true);

                File.SetLastWriteTime(Target, File.GetLastWriteTime(Source));
            });
        }
    }

    class FileDelete : IOperation
    {
        public string Path { get; set; }

        public async Task Perform(Settings settings)
        {
            await Task.Run(() =>
            {
                Console.WriteLine($"-{Path}");

                File.Delete(Path);
            });
        }
    }

    class FolderDelete : IOperation
    {
        public string Path { get; set; }

        public async Task Perform(Settings settings)
        {
            await Task.Run(() =>
            {
                Console.WriteLine($"-{Path}");

                Directory.Delete(Path, true);
            });
        }
    }
}
