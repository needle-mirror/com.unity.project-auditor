using System;

namespace Unity.ProjectAuditor.Editor.Core
{
    /// <summary>
    /// For building an asset dependency tree.
    /// </summary>
    public class AssetDependencyNode : DependencyNode
    {
        /// <summary>
        /// Gets the node's "raw" name
        /// </summary>
        /// <returns>The node's name</returns>
        public override string GetName()
        {
            return Location.Filename;
        }

        /// <summary>
        /// Gets the node's "pretty" name, suitable for UI display
        /// </summary>
        /// <returns>The node's prettified name</returns>
        public override string GetPrettyName()
        {
            return Location.Path;
        }

        /// <summary>
        /// Gets whether this node represents a performance-critical issue
        /// </summary>
        /// <returns>True if the issue is performance critical. Otherwise, returns false.</returns>
        public override bool IsPerfCritical()
        {
            return false;
        }
    }
}
