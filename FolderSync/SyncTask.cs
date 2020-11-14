using System;

namespace FolderSync
{
    public class SyncTask
    {
        public string SourceFolder { get; set; }

        public string DestinationFolder { get; set; }

        public bool Mirror { get; set; }

        public string ExcludeFolders { get; set; }

        public string ExcludeFiles { get; set; }

        public bool Execute()
        {
            var settings = new Settings
            {
                SourceFolder = SourceFolder,
                DestinationFolder = DestinationFolder,
                ExcludeFiles = (ExcludeFiles ?? "").Split(';'),
                ExcludeFolders = (ExcludeFolders ?? "").Split(';'),
                Mirror = Mirror
            };

            Console.WriteLine($"Sync Folders {settings.SourceFolder} to {settings.DestinationFolder}");

            Console.WriteLine($"Ignoring folders {string.Join(";", settings.ExcludeFolders)}");
            Console.WriteLine($"Ignoring files {string.Join(";", settings.ExcludeFiles)}");

            var sync = new Sync();

            sync.Perform(settings);

            return true;
        }
    }
}
