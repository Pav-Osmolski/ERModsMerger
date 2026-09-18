using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace ERModsMerger.Core
{
    public static class EmbeddedResourcesExtractor
    {
        /// <summary>
        /// Extracts the embedded Assets archive to the app-data folder, then overlays
        /// loose versioned ParamDefs and regulation baselines from the repository.
        /// </summary>
        public static void ExtractAssets()
        {
            string folderPath = ModsMergerConfig.LoadedConfig.AppDataFolderPath;
            Directory.CreateDirectory(folderPath);

            Assembly assembly = Assembly.GetAssembly(typeof(ERModsMerger.Core.ModsMerger))!;
            string[] resourceNames = assembly.GetManifestResourceNames();

            foreach (string resourceName in resourceNames)
            {
                if (!resourceName.Contains("ERModsMerger.Core.ERModsMergerAssets.Assets.zip", StringComparison.Ordinal))
                    continue;

                using Stream stream = assembly.GetManifestResourceStream(resourceName)!;
                ZipFile.ExtractToDirectory(stream, folderPath, true);
                break;
            }

            ExtractParamDefOverrides(assembly, resourceNames, folderPath);
            ExtractRegulationBaselines(assembly, resourceNames, folderPath);
        }

        private static void ExtractParamDefOverrides(Assembly assembly, string[] resourceNames, string folderPath)
        {
            const string marker = ".ERModsMergerParamDefOverrides.";
            string paramDefsPath = Path.Combine(folderPath, "ParamDefs");
            Directory.CreateDirectory(paramDefsPath);

            foreach (string resourceName in resourceNames)
            {
                int markerIndex = resourceName.IndexOf(marker, StringComparison.Ordinal);
                if (markerIndex < 0 || !resourceName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    continue;

                string fileName = resourceName[(markerIndex + marker.Length)..];
                CopyResource(assembly, resourceName, Path.Combine(paramDefsPath, fileName));
            }
        }

        private static void ExtractRegulationBaselines(Assembly assembly, string[] resourceNames, string folderPath)
        {
            const string marker = ".ERModsMergerRegulations.";
            const string suffix = ".regulation.bin";
            string regulationsPath = Path.Combine(folderPath, "Regulations");
            Directory.CreateDirectory(regulationsPath);

            foreach (string resourceName in resourceNames)
            {
                int markerIndex = resourceName.IndexOf(marker, StringComparison.Ordinal);
                if (markerIndex < 0 || !resourceName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string version = resourceName[
                    (markerIndex + marker.Length)..
                    (resourceName.Length - suffix.Length)];

                if (string.IsNullOrWhiteSpace(version))
                    continue;

                string targetDirectory = Path.Combine(regulationsPath, version);
                Directory.CreateDirectory(targetDirectory);
                CopyResource(assembly, resourceName, Path.Combine(targetDirectory, "regulation.bin"));
            }
        }

        private static void CopyResource(Assembly assembly, string resourceName, string destinationPath)
        {
            using Stream source = assembly.GetManifestResourceStream(resourceName)!;
            using FileStream destination = File.Create(destinationPath);
            source.CopyTo(destination);
        }
    }
}
