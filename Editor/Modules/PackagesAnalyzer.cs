using System;
using System.Collections.Generic;
using Unity.ProjectAuditor.Editor.Core;
using UnityEditor.PackageManager;

namespace Unity.ProjectAuditor.Editor.Modules
{
    [MigratedToRulesPackage(2)]
    internal class PackagesAnalyzer : PackagesModuleAnalyzer
    {
        internal const string PAP0001 = nameof(PAP0001);
        internal const string PAP0002 = nameof(PAP0002);
        internal const string PAP0003 = nameof(PAP0003);

        static readonly Descriptor k_RecommendPackageUpgrade = new Descriptor(
            PAP0001,
            "Newer recommended package version",
            Areas.Quality,
            "A newer recommended version of this package is available.",
            "Update the package via Package Manager."
        )
        {
            MessageFormat = "Package '{0}' could be updated from version '{1}' to '{2}'",
            DefaultSeverity = Severity.Minor
        };

        static readonly Descriptor k_RecommendPackagePreView = new Descriptor(
            PAP0002,
            "Experimental/Preview packages",
            Areas.Quality,
            "Experimental or Preview packages are in the early stages of development and not yet ready for production.",
            "Experimental packages should only be used for testing purposes and to give feedback to Unity."
        )
        {
            MessageFormat = "Package '{0}' version '{1}' is a preview/experimental version"
        };

        internal static readonly Descriptor k_ModifiedPackageDescriptor = new Descriptor(
            PAP0003,
            "Modified Package",
            Areas.Quality | Areas.Upgrade,
            "Using modified versions of Unity packages prevents easy updates to newer versions. Unity expects to be able to update these packages in lockstep with Editor versions. The modified version may not be compatible with a newer version of Unity.",
            "Consider whether the package really needs to be customized."
            )
        {
            MessageFormat = "Using modified package '{0}'",
            DefaultSeverity = Severity.Major
        };

        public override void Initialize(Action<Descriptor> registerDescriptor)
        {
            registerDescriptor(k_RecommendPackageUpgrade);
            registerDescriptor(k_RecommendPackagePreView);
            registerDescriptor(k_ModifiedPackageDescriptor);
        }

        public override IEnumerable<ReportItem> Analyze(PackageAnalysisContext context)
        {
            var package = context.PackageInfo;

            // first check if any package is preview or experimental
            if (package.version.Contains("pre") || package.version.Contains("exp"))
            {
                yield return context.CreateIssue(IssueCategory.ProjectSetting, k_RecommendPackagePreView.Id, package.name, package.version)
                    .WithLocation(package.assetPath);
            }

            // if not preview or experimental, check anyway if there is a recommended version available
            var recommendedVersionString = package.versions.recommended;
            if (!string.IsNullOrEmpty(package.version) && !string.IsNullOrEmpty(recommendedVersionString))
            {
                if (!recommendedVersionString.Equals(package.version))
                {
                    yield return context.CreateIssue(IssueCategory.ProjectSetting, k_RecommendPackageUpgrade.Id, package.name, package.version, recommendedVersionString)
                        .WithLocation(package.assetPath);
                }
            }

            // custom/modified packages are high risk for upgrades because Unity expects to update them in lockstep with Editor versions
            if (package.source == PackageSource.Embedded || package.source == PackageSource.Local || package.source == PackageSource.LocalTarball)
            {
                // Modified package (Local but exists on Registry)
                if (package.versions != null && !string.IsNullOrEmpty(package.versions.latest) && ObsoleteLibrary.UnityVersions.Length > 1)
                {
                    yield return context.CreateIssue(IssueCategory.ProjectSetting, k_ModifiedPackageDescriptor.Id, package.name)
                        .WithLocation(package.assetPath)
                        .WithUpgradeProperties(new[] { ObsoleteLibrary.UnityVersions[1], null, null });
                }
                // Custom package (Local and unknown to Registry, no higher risk than normal project code)
            }
        }
    }
}
