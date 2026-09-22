using System;
using System.Collections.Generic;
using System.IO;
using Unity.ProjectAuditor.Editor.Core;
using UnityEngine;

namespace Unity.ProjectAuditor.Editor.Modules
{
    /// <summary>
    /// Holds the relevant contents of the "combined manifest" JSON files (one per Major.Minor editor line, e.g. "6000.1").
    /// </summary>
    public class PackageManifestDatabase
    {
        // TEMP: combined manifests are hosted in the rules package, one file per Major.Minor editor
        // line, until Unity hosts and updates this data in a central location.
        const string k_CombinedManifestsFolder = "com.unity.editor-manifests.combined";

        internal struct PackageVersionInfo
        {
            public string Version;
            public string MinimumVersion;
            public string DeprecatedMessage;
            public bool RemovedOnProjectUpgrade;

            // Presence of a "deprecated" message marks the package as deprecated.
            public bool Deprecated => !string.IsNullOrEmpty(DeprecatedMessage);
        }

        internal class EditorVersionManifest
        {
            // Package name -> version info, taken from the latest patch (last entry in "versions").
            public Dictionary<string, PackageVersionInfo> Packages;

            // Union of every package name that appears in any patch's "removed" list.
            public List<string> RemovedPackages;
        }

        /// <summary>
        /// Package status at a given version.
        /// </summary>
        public enum PackageManifestStatus
        {
            /// <summary>
            /// The package is active.
            /// </summary>
            Active,
            /// <summary>
            /// The package has been deprecated.
            /// </summary>
            Deprecated,
            /// <summary>
            /// The package has been removed.
            /// </summary>
            Removed
        }

        /// <summary>
        /// Package info at a given version.
        /// </summary>
        public class PackageManifestInfo
        {
            /// <summary>
            /// Package status.
            /// </summary>
            public PackageManifestStatus Status { get; internal set; }

            /// <summary>
            /// Package version.
            /// </summary>
            public string Version { get; internal set; }

            /// <summary>
            /// Package minimum version.
            /// </summary>
            public string MinimumVersion { get; internal set; }

            /// <summary>
            /// The deprecation message, if the package is deprecated. Null otherwise.
            /// </summary>
            public string DeprecatedMessage { get; internal set; }

            /// <summary>
            /// True if the package will be removed from the project automatically when the project is upgraded to this editor version.
            /// </summary>
            public bool RemovedOnProjectUpgrade { get; internal set; }
        }

#pragma warning disable CS0649
        [Serializable]
        private sealed class SerializedPackage
        {
            public string name;
            public string version;
            public string minimumVersion;
            public string deprecated;
            public bool removeOnProjectUpgrade;
        }

        [Serializable]
        private sealed class SerializedManifest
        {
            public string version;
            public SerializedPackage[] packages;
            public string[] removed;
        }

        // Overrides apply to the whole editor version line (not to a specific patch), and can name
        // packages that never appear in any patch's "packages" list.
        [Serializable]
        private sealed class SerializedOverride
        {
            public string name;
            public string deprecated;
        }

        [Serializable]
        private sealed class SerializedFile
        {
            public string editorVersionPrefix;
            public SerializedManifest[] manifests;
            public SerializedOverride[] overrides;
        }
#pragma warning restore CS0649

        // keyed by Major.Minor ("6000.1"), matching ObsoleteLibrary.UnityVersions
        readonly Dictionary<string, EditorVersionManifest> m_Manifests = new Dictionary<string, EditorVersionManifest>();

        static PackageManifestDatabase s_Instance;

        /// <summary>
        /// Lazily-created, cached database of package version info.
        /// </summary>
        internal static PackageManifestDatabase Instance
        {
            get
            {
                if (s_Instance == null)
                {
                    s_Instance = new PackageManifestDatabase();
                    s_Instance.Load();
                }

                return s_Instance;
            }
        }

        internal PackageManifestDatabase()
        {
        }

        internal void Load()
        {
            if (!ProjectAuditorRulesPackage.IsInstalled)
                return;

            var manifestsFolder = Path.Combine(ProjectAuditor.s_RulesDataPath, k_CombinedManifestsFolder);

            // Only load manifests for versions we might report upgrade issues for; there's no need to
            // parse data for versions ObsoleteLibrary has already filtered out.
            foreach (var editorVersionPrefix in ObsoleteLibrary.UnityVersions)
            {
                var filename = Path.Combine(manifestsFolder, editorVersionPrefix + ".json");
                if (!File.Exists(filename))
                    continue;

                Parse(File.ReadAllText(filename));
            }
        }

        internal void Parse(string json)
        {
            if (string.IsNullOrEmpty(json))
                return;

            var file = JsonUtility.FromJson<SerializedFile>(json);
            if (file == null || string.IsNullOrEmpty(file.editorVersionPrefix) || file.manifests == null)
                return;

            // Only store data for versions in ObsoleteLibrary.UnityVersions (already filtered to
            // versions newer than the running editor).
            if (Array.IndexOf(ObsoleteLibrary.UnityVersions, file.editorVersionPrefix) < 0)
                return;

            var manifest = new EditorVersionManifest
            {
                Packages = new Dictionary<string, PackageVersionInfo>(),
                RemovedPackages = new List<string>()
            };

            // Each patch manifest is a delta: the base patch lists the full package set and every later
            // patch lists only the packages that changed. Replay them in ascending patch order (later
            // patch wins) to reconstruct the effective latest state.
            var orderedManifests = new List<SerializedManifest>(file.manifests);
            orderedManifests.Sort((a, b) => CompareEditorVersions(a?.version, b?.version));

            foreach (var patchManifest in orderedManifests)
            {
                if (patchManifest == null)
                    continue;

                if (patchManifest.packages != null)
                {
                    foreach (var package in patchManifest.packages)
                    {
                        if (package == null || string.IsNullOrEmpty(package.name))
                            continue;

                        // Delta entries can be partial (only the changed field is present), so merge into the accumulated record.
                        manifest.Packages.TryGetValue(package.name, out var info);

                        if (!string.IsNullOrEmpty(package.version))
                            info.Version = package.version;
                        if (!string.IsNullOrEmpty(package.minimumVersion))
                            info.MinimumVersion = package.minimumVersion;
                        if (!string.IsNullOrEmpty(package.deprecated))
                            info.DeprecatedMessage = package.deprecated;
                        if (package.removeOnProjectUpgrade)
                            info.RemovedOnProjectUpgrade = true;

                        manifest.Packages[package.name] = info;
                    }
                }

                // "removed" entries appear only in the exact patch a package was removed in. Drop the
                // package from the effective set and record it in the per-version removed list.
                if (patchManifest.removed != null)
                {
                    foreach (var removedName in patchManifest.removed)
                    {
                        if (string.IsNullOrEmpty(removedName))
                            continue;

                        manifest.Packages.Remove(removedName);

                        if (!manifest.RemovedPackages.Contains(removedName))
                            manifest.RemovedPackages.Add(removedName);
                    }
                }
            }

            // Overrides apply on top of the merged patch data, and can deprecate packages that never
            // appear in any patch's "packages" list (e.g. packages removed from discovery entirely).
            if (file.overrides != null)
            {
                foreach (var over in file.overrides)
                {
                    if (over == null || string.IsNullOrEmpty(over.name) || string.IsNullOrEmpty(over.deprecated))
                        continue;

                    if (manifest.RemovedPackages.Contains(over.name))
                        continue;

                    manifest.Packages.TryGetValue(over.name, out var info);
                    info.DeprecatedMessage = over.deprecated;
                    manifest.Packages[over.name] = info;
                }
            }

            m_Manifests[file.editorVersionPrefix] = manifest;
        }

        struct UnityVersionCopy : IEquatable<UnityVersionCopy>, IComparable<UnityVersionCopy>, IComparable
        {
            public enum UnityReleaseType
            {
                kAlphaRelease,
                kBetaRelease,
                kPublicRelease,
                kChinaPublicRelease,
                kPatchRelease,
                kExperimentalRelease,
                kNumUnityReleaseTypes
            }

            public bool IsInitialized { get; }

            public int Major { get; }

            public int Minor { get; }

            public int Revision { get; }

            public UnityReleaseType ReleaseType { get; }

            public int IncrementalVersion { get; }

            public string Suffix { get; }

            public UnityVersionCopy(int major, int minor = 0, int revision = 0, UnityReleaseType releaseType = UnityReleaseType.kPublicRelease, int incrementalVersion = 0, string suffix = "")
            {
                Major = major;
                Minor = minor;
                Revision = revision;
                ReleaseType = releaseType;
                IncrementalVersion = incrementalVersion;
                Suffix = suffix;
                IsInitialized = true;
            }

            public static int Compare(UnityVersionCopy versionA, UnityVersionCopy versionB)
            {
                return versionA.CompareTo(versionB);
            }

            public static bool operator ==(UnityVersionCopy left, UnityVersionCopy right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(UnityVersionCopy left, UnityVersionCopy right)
            {
                return !(left == right);
            }

            public static bool operator >(UnityVersionCopy left, UnityVersionCopy right)
            {
                return Compare(left, right) > 0;
            }

            public static bool operator >=(UnityVersionCopy left, UnityVersionCopy right)
            {
                return left == right || left > right;
            }

            public static bool operator <(UnityVersionCopy left, UnityVersionCopy right)
            {
                return Compare(left, right) < 0;
            }

            public static bool operator <=(UnityVersionCopy left, UnityVersionCopy right)
            {
                return left == right || left < right;
            }

            public int CompareTo(object obj)
            {
                return CompareTo((UnityVersionCopy)obj);
            }

            public int CompareTo(UnityVersionCopy other)
            {
                int num = Major.CompareTo(other.Major);
                if (num != 0)
                {
                    return num;
                }

                num = Minor.CompareTo(other.Minor);
                if (num != 0)
                {
                    return num;
                }

                num = Revision.CompareTo(other.Revision);
                if (num != 0)
                {
                    return num;
                }

                num = CompareReleaseType(ReleaseType, other.ReleaseType);
                if (num != 0)
                {
                    return num;
                }

                num = IncrementalVersion.CompareTo(other.IncrementalVersion);
                if (num != 0)
                {
                    return num;
                }

                return 0;
            }

            private static int CompareReleaseType(UnityReleaseType current, UnityReleaseType other)
            {
                UnityReleaseType unityReleaseType = ((current != UnityReleaseType.kChinaPublicRelease) ? current : UnityReleaseType.kPublicRelease);
                UnityReleaseType unityReleaseType2 = ((other != UnityReleaseType.kChinaPublicRelease) ? other : UnityReleaseType.kPublicRelease);
                return unityReleaseType - unityReleaseType2;
            }

            public bool Equals(UnityVersionCopy other)
            {
                return Major == other.Major && Minor == other.Minor && Revision == other.Revision && CompareReleaseType(ReleaseType, other.ReleaseType) == 0 && IncrementalVersion == other.IncrementalVersion;
            }

            public override bool Equals(object other)
            {
                if (other == null)
                {
                    return false;
                }

                return other.GetType() == GetType() && Equals((UnityVersionCopy)other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int result = Major.GetHashCode();
                    result = result * 31 + Minor.GetHashCode();
                    result = result * 31 + Revision.GetHashCode();
                    result = result * 31 + GetReleaseTypeHashCode(ReleaseType);
                    result = result * 31 + IncrementalVersion.GetHashCode();
                    // We do not compare the suffix, so we do not hash it either
                    return result;
                }
            }

            private static int GetReleaseTypeHashCode(UnityReleaseType releaseType)
            {
                var rt = releaseType != UnityReleaseType.kChinaPublicRelease ? releaseType : UnityReleaseType.kPublicRelease;
                return rt.GetHashCode();
            }
        }

        // Compares Unity editor version strings, e.g. "6000.1.17f1" vs "6000.1.8f1".
        // Utility.CompareVersions targets package SemVer and doesn't understand the "17f1" patch suffix.
        static int CompareEditorVersions(string lhs, string rhs)
        {
            TryParse(lhs, out var left);
            TryParse(rhs, out var right);
            return (left ?? default).CompareTo(right ?? default);
        }

        // Copied from Unity source
        static bool TryParse(string version, out UnityVersionCopy? result, bool strict = false)
        {
            int cursor = 0;
            int versionComponent = 0;
            int versionComponent2 = 0;
            int versionComponent3 = 0;
            UnityVersionCopy.UnityReleaseType releaseType = UnityVersionCopy.UnityReleaseType.kPublicRelease;
            int versionComponent4 = 0;
            string suffix = "";
            bool flag = true;
            try
            {
                flag = cursor < version.Length && TryReadVersionNumber(version, ref cursor, ref versionComponent) && (cursor == version.Length || TrySkipVersionSeparator(version, ref cursor)) && (cursor == version.Length || TryReadVersionNumber(version, ref cursor, ref versionComponent2)) && (cursor == version.Length || TrySkipVersionSeparator(version, ref cursor)) && (cursor == version.Length || TryReadVersionNumber(version, ref cursor, ref versionComponent3)) && (cursor == version.Length || TryReadVersionReleaseType(version, ref cursor, ref releaseType)) && (cursor == version.Length || releaseType == UnityVersionCopy.UnityReleaseType.kExperimentalRelease || TryReadVersionNumber(version, ref cursor, ref versionComponent4));
                if (flag && cursor < version.Length)
                {
                    if (releaseType == UnityVersionCopy.UnityReleaseType.kExperimentalRelease || !strict)
                    {
                        suffix = version.Substring(cursor);
                    }
                    else
                    {
                        flag = false;
                    }
                }
            }
            catch (Exception)
            {
                result = null;
                return false;
            }

            if (!flag)
            {
                result = null;
                return false;
            }

            result = new UnityVersionCopy(versionComponent, versionComponent2, versionComponent3, releaseType, versionComponent4, suffix);
            return true;
        }

        private static bool TryReadVersionNumber(string version, ref int cursor, ref int versionComponent)
        {
            string text = ConsumeVersionComponentFromString(version, ref cursor, (char x) => !char.IsDigit(x));
            if (!string.IsNullOrEmpty(text))
            {
                return int.TryParse(text, out versionComponent);
            }

            return false;
        }

        private static bool TrySkipVersionSeparator(string version, ref int cursor)
        {
            if (cursor < version.Length && version[cursor] == '.')
            {
                cursor++;
                return cursor < version.Length;
            }

            return false;
        }

        private static bool TryReadVersionReleaseType(string version, ref int cursor, ref UnityVersionCopy.UnityReleaseType releaseType)
        {
            if (cursor < version.Length && IsAllowedUnityReleaseTypeIdentifier(version[cursor]) && TryParseUnityReleaseType(version[cursor], out releaseType))
            {
                cursor++;
                return true;
            }

            return false;
        }

        private static string ConsumeVersionComponentFromString(string value, ref int cursor, Func<char, bool> isEnd)
        {
            int num = 0;
            for (int i = cursor; i < value.Length && !isEnd(value[i]); i++)
            {
                num++;
            }

            int startIndex = cursor;
            cursor += num;
            return value.Substring(startIndex, num);
        }

        private static readonly char[] k_ValidReleaseTypeSymbols = new char[6] { 'a', 'b', 'f', 'c', 'p', 'x' };

        private static bool IsAllowedUnityReleaseTypeIdentifier(char c)
        {
            for (int i = 0; i < k_ValidReleaseTypeSymbols.Length; i++)
            {
                if (k_ValidReleaseTypeSymbols[i] == c)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryParseUnityReleaseType(char releaseType, out UnityVersionCopy.UnityReleaseType result)
        {
            for (int i = 0; i < 6; i++)
            {
                if (releaseType == k_ValidReleaseTypeSymbols[i])
                {
                    result = (UnityVersionCopy.UnityReleaseType)i;
                    return true;
                }
            }

            result = UnityVersionCopy.UnityReleaseType.kNumUnityReleaseTypes;
            return false;
        }

        /// <summary>
        /// Queries the package version database.
        /// </summary>
        /// <param name="editorVersionPrefix">The Unity version to query. Usually a future version, to detect what will change during an upgrade.</param>
        /// <param name="packageName">The package to query.</param>
        /// <param name="manifest">The information about the package in the desired version.</param>
        /// <returns>Returns true if the database contains information about the supplied package.</returns>
        public bool TryGetManifestInfo(string editorVersionPrefix, string packageName, out PackageManifestInfo manifest)
        {
            if (!m_Manifests.TryGetValue(editorVersionPrefix, out var versionManifest))
            {
                manifest = null;
                return false;
            }

            if (versionManifest.RemovedPackages.Contains(packageName))
            {
                manifest = new PackageManifestInfo { Status = PackageManifestStatus.Removed };
                return true;
            }
            else if (versionManifest.Packages.TryGetValue(packageName, out var info))
            {
                manifest = new PackageManifestInfo();

                if (info.Deprecated)
                    manifest.Status = PackageManifestStatus.Deprecated;
                else
                    manifest.Status = PackageManifestStatus.Active;

                manifest.Version = info.Version;
                manifest.MinimumVersion = info.MinimumVersion;
                manifest.DeprecatedMessage = info.DeprecatedMessage;
                manifest.RemovedOnProjectUpgrade = info.RemovedOnProjectUpgrade;
                return true;
            }
            else
            {
                manifest = null;
                return false;
            }
        }
    }
}
