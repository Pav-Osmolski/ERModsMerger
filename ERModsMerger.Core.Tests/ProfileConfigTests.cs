using ERModsMerger.Core;
using Xunit;

namespace ERModsMerger.Core.Tests;

public class ProfileConfigTests
{
    [Fact]
    public void DefaultWorkingFoldersStayUnderProfileDirectory()
    {
        string root = CreateTempRoot();

        try
        {
            string profileDir = Path.Combine(root, "ERModsMergerConfig");
            ProfileConfig profile = new("Main Profile", profileDir);

            Assert.Equal(Path.Combine(profileDir, "ModsToMerge"), profile.ModsToMergeFolderPath);
            Assert.Equal(Path.Combine(profileDir, "CSVToMerge"), profile.CSVToMergeFolderPath);
            Assert.Equal(Path.Combine(profileDir, "MergedMods"), profile.MergedModsFolderPath);

            Assert.True(Directory.Exists(profile.ModsToMergeFolderPath));
            Assert.True(Directory.Exists(profile.CSVToMergeFolderPath));
            Assert.True(Directory.Exists(profile.MergedModsFolderPath));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void LegacyV150RootFoldersAreMigratedIntoMainProfile()
    {
        string root = CreateTempRoot();

        try
        {
            string profileDir = Path.Combine(root, "ERModsMergerConfig");
            ProfileConfig profile = new("Main Profile", profileDir)
            {
                ModsToMergeFolderPath = "ModsToMerge",
                CSVToMergeFolderPath = "CSVToMerge",
                MergedModsFolderPath = "MergedMods"
            };

            string legacyMods = Path.Combine(root, "ModsToMerge");
            string legacyCsv = Path.Combine(root, "CSVToMerge");
            string legacyMerged = Path.Combine(root, "MergedMods");

            Directory.CreateDirectory(Path.Combine(legacyMods, "ExampleMod"));
            Directory.CreateDirectory(legacyCsv);
            Directory.CreateDirectory(legacyMerged);

            File.WriteAllText(Path.Combine(legacyMods, "ExampleMod", "regulation.bin"), "mod");
            File.WriteAllText(Path.Combine(legacyCsv, "EquipParamWeapon.csv"), "ID,Name");
            File.WriteAllText(Path.Combine(legacyMerged, "regulation.bin"), "merged");

            bool changed = profile.MigrateLegacyRootFolders(profileDir, root);

            Assert.True(changed);
            Assert.Equal(Path.Combine(profileDir, "ModsToMerge"), profile.ModsToMergeFolderPath);
            Assert.Equal(Path.Combine(profileDir, "CSVToMerge"), profile.CSVToMergeFolderPath);
            Assert.Equal(Path.Combine(profileDir, "MergedMods"), profile.MergedModsFolderPath);

            Assert.True(File.Exists(Path.Combine(profile.ModsToMergeFolderPath, "ExampleMod", "regulation.bin")));
            Assert.True(File.Exists(Path.Combine(profile.CSVToMergeFolderPath, "EquipParamWeapon.csv")));
            Assert.True(File.Exists(Path.Combine(profile.MergedModsFolderPath, "regulation.bin")));

            Assert.False(Directory.Exists(legacyMods));
            Assert.False(Directory.Exists(legacyCsv));
            Assert.False(Directory.Exists(legacyMerged));
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    [Fact]
    public void CustomProfilePathsAreNotRewritten()
    {
        string root = CreateTempRoot();

        try
        {
            string profileDir = Path.Combine(root, "ERModsMergerConfig");
            string customMods = Path.Combine(root, "MyCustomMods");

            ProfileConfig profile = new("Main Profile", profileDir)
            {
                ModsToMergeFolderPath = customMods
            };

            bool changed = profile.MigrateLegacyRootFolders(profileDir, root);

            Assert.False(changed);
            Assert.Equal(customMods, profile.ModsToMergeFolderPath);
        }
        finally
        {
            DeleteTempRoot(root);
        }
    }

    private static string CreateTempRoot()
    {
        string path = Path.Combine(Path.GetTempPath(), "ERModsMerger.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTempRoot(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }
}
