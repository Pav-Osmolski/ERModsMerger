using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace ERModsMerger.Core
{
    public static class EmbeddedResourcesExtractor
    {
        /// <summary>
        /// Extracts the embedded Assets archive to the app-data folder, then overlays
        /// any versioned ParamDef resources that must supersede the archive contents.
        /// </summary>
        public static void ExtractAssets()
        {
            string folderPath = ModsMergerConfig.LoadedConfig.AppDataFolderPath;
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            Assembly assembly = Assembly.GetAssembly(typeof(ERModsMerger.Core.ModsMerger))!;
            string[] resourceNames = assembly.GetManifestResourceNames();

            foreach (string resourceName in resourceNames)
            {
                if (!resourceName.Contains("ERModsMerger.Core.ERModsMergerAssets.Assets.zip"))
                    continue;

                using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
                ZipFile.ExtractToDirectory(stream, folderPath, true);
                break;
            }

            const string overrideMarker = ".ERModsMergerParamDefOverrides.";
            string paramDefsPath = Path.Combine(folderPath, "ParamDefs");
            Directory.CreateDirectory(paramDefsPath);

            foreach (string resourceName in resourceNames)
            {
                int markerIndex = resourceName.IndexOf(overrideMarker, System.StringComparison.Ordinal);
                if (markerIndex < 0 || !resourceName.EndsWith(".xml", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                string fileName = resourceName[(markerIndex + overrideMarker.Length)..];
                string destinationPath = Path.Combine(paramDefsPath, fileName);

                using Stream source = assembly.GetManifestResourceStream(resourceName)!;
                using FileStream destination = File.Create(destinationPath);
                source.CopyTo(destination);
            }
        }
    }
}
