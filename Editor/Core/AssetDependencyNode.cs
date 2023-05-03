using System;

namespace Unity.ProjectAuditor.Editor.Core
{
    internal class AssetDependencyNode : DependencyNode
    {
        public override string GetName()
        {
            return location.Filename;
        }

        public override string GetPrettyName()
        {
            return location.Path;
        }

        public override bool IsPerfCritical()
        {
            return false;
        }
    }
}
