
namespace Unity.ProjectAuditor.Editor.Utils
{
    internal static class Utility
    {
        internal static int VersionToInt(string version)
        {
            var parts = version.Split('.');
            return int.Parse(parts[0]) * 100 + int.Parse(parts[1]); // Just any integer that can be used for comparison
        }
    }
}
