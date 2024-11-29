using System.Collections.Generic;
using UnityEngine.U2D;

namespace Unity.ProjectAuditor.Editor.Core
{
    /// <summary>
    /// A context object passed by SpriteAtlasModule to a SpriteAtlasModuleAnalyzer's Analyze() method.
    /// </summary>
    public class SpriteAtlasAnalysisContext : AnalysisContext
    {
        /// <summary>
        /// The name of a sprite atlas asset in the project
        /// </summary>
        public string Name;

        /// <summary>
        /// The spriteatlas asset to be analyzed.
        /// </summary>
        public SpriteAtlas SpriteAtlas;

        /// <summary>
        /// The empty space percentage of the sprite atlas.
        /// </summary>
        public float EmptySpacePercentage;

        /// <summary>
        /// The path to a Sprite Atlas asset in the project.
        /// </summary>
        public string AssetPath;
    }

    /// <summary>
    /// Abstract base class for an Analyzer to be invoked by SpriteAtlasModule
    /// </summary>
    public abstract class SpriteAtlasModuleAnalyzer : ModuleAnalyzer
    {
        /// <summary>
        /// Implement this method to detect Issues, construct ReportItem objects to describe them, and return them.
        /// </summary>
        /// <param name="context">Context object containing information necessary to perform analysis</param>
        /// <returns>An enumerator for a collection of ReportItem objects</returns>
        public abstract IEnumerable<ReportItem> Analyze(SpriteAtlasAnalysisContext context);
    }
}
