using ERModsMerger.Core;
using Xunit;

namespace ERModsMerger.Core.Tests;

public class ProfileConfigTests
{
    [Fact]
    public void DefaultWorkingFoldersStayUnderProfileDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "ERModsMerger.Tests", Guid.NewGuid().ToString("N"));

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
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }
    }
}
