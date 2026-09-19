using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace ERModsMerger.Core
{
    public static class EmbeddedResourcesExtractor
    {
        /// <summary>
        /// Extracts the generated Assets.zip shipped beside the application.
        /// Older embedded-resource packages remain supported as a fallback.
        /// </summary>
        public static void ExtractAssets()
        {
            string folderPath = ModsMergerConfig.LoadedConfig!.AppDataFolderPath;
            Directory.CreateDirectory(folderPath);

            string externalArchive = Path.Combine(AppContext.BaseDirectory, "Assets.zip");
            if (File.Exists(externalArchive))
            {
                ZipFile.ExtractToDirectory(externalArchive, folderPath, true);
                return;
            }

            Assembly assembly = Assembly.GetAssembly(typeof(ModsMerger))
                ?? throw new InvalidOperationException("Could not resolve ERModsMerger.Core assembly.");

            string? resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name =>
                    name.Contains(
                        "ERModsMerger.Core.ERModsMergerAssets.Assets.zip",
                        StringComparison.Ordinal));

            if (resourceName == null)
            {
                throw new FileNotFoundException(
                    "Assets.zip was not found beside the application and no embedded fallback exists.",
                    externalArchive);
            }

            using Stream stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidDataException($"Embedded asset resource could not be opened: {resourceName}");

            ZipFile.ExtractToDirectory(stream, folderPath, true);
        }
    }
}
