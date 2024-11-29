using System.Collections.Generic;
using System.IO;
using Unity.ProjectAuditor.Editor.Core;
using Unity.ProjectAuditor.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace Unity.ProjectAuditor.Editor.Modules
{
    internal enum TextureProperty
    {
        Shape,
        ImporterType,
        Format,
        TextureCompression,
        MipMapEnabled,
        Readable,
        Resolution,
        SizeOnDisk,
        StreamingMipMap,
        Num
    }

    class TextureModule : ModuleWithAnalyzers<TextureModuleAnalyzer>
    {
        static readonly IssueLayout k_TextureLayout = new IssueLayout
        {
            Category = IssueCategory.Texture,
            Properties = new[]
            {
                new PropertyDefinition { Type = PropertyType.Description, Format = PropertyFormat.String, Name = "Name", LongName = "Texture Name", MaxAutoWidth = 500 },
                new PropertyDefinition { Type = PropertyTypeUtil.FromCustom(TextureProperty.Shape), Format = PropertyFormat.String, Name = "Shape", LongName = "Texture Shape" },
                new PropertyDefinition { Type = PropertyTypeUtil.FromCustom(TextureProperty.ImporterType), Format = PropertyFormat.String, Name = "Importer Type", LongName = "Texture Importer Type" },
                new PropertyDefinition { Type = PropertyTypeUtil.FromCustom(TextureProperty.Format), Format = PropertyFormat.String, Name = "Format", LongName = "Texture Format" },
                new PropertyDefinition { Type = PropertyTypeUtil.FromCustom(TextureProperty.TextureCompression), Format = PropertyFormat.String, Name = "Compression", LongName = "Texture Compression" },
                new PropertyDefinition { Type = PropertyTypeUtil.FromCustom(TextureProperty.MipMapEnabled), Format = PropertyFormat.Bool, Name = "MipMaps", LongName = "Texture MipMaps Enabled" },
                new PropertyDefinition { Type = PropertyTypeUtil.FromCustom(TextureProperty.Readable), Format = PropertyFormat.Bool, Name = "Readable", LongName = "Readable" },
                new PropertyDefinition { Type = PropertyTypeUtil.FromCustom(TextureProperty.Resolution), Format = PropertyFormat.String, Name = "Resolution", LongName = "Texture Resolution" },
                new PropertyDefinition { Type = PropertyTypeUtil.FromCustom(TextureProperty.SizeOnDisk), Format = PropertyFormat.Bytes, Name = "Size", LongName = "Texture Size" },
                new PropertyDefinition { Type = PropertyTypeUtil.FromCustom(TextureProperty.StreamingMipMap), Format = PropertyFormat.Bool, Name = "Streaming", LongName = "Mipmaps Streaming" },
                new PropertyDefinition { Type = PropertyType.Path, Name = "Path", MaxAutoWidth = 500 }
            }
        };

        public override string Name => "Textures";

        public override IReadOnlyCollection<IssueLayout> SupportedLayouts => new IssueLayout[]
        {
            k_TextureLayout,
            AssetsModule.k_IssueLayout
        };

        public override void Initialize()
        {
            base.Initialize();

            ProjectIssueExtensions.AddCustomComparer(IssueCategory.Texture, PropertyTypeUtil.FromCustom(TextureProperty.Resolution),
                (a, b) =>
                {
                    var strValsA = a.GetProperty(PropertyTypeUtil.FromCustom(TextureProperty.Resolution)).Split('x');
                    var strValsB = b.GetProperty(PropertyTypeUtil.FromCustom(TextureProperty.Resolution)).Split('x');

                    var aX = int.Parse(strValsA[0]);
                    var aY = int.Parse(strValsA[1]);
                    var aMult = aX * aY;

                    var bX = int.Parse(strValsB[0]);
                    var bY = int.Parse(strValsB[1]);
                    var bMult = bX * bY;

                    // Sort by total pixels first
                    var retVal = aMult < bMult ? -1 : aMult > bMult ? 1 : 0;

                    // If equal, sort by X value
                    if (retVal == 0)
                    {
                        retVal = aX < bX ? -1 : aX > bX ? 1 : 0;
                    }
                    return retVal;
                });
        }

        public override AnalysisResult Audit(AnalysisParams analysisParams, IProgress progress = null)
        {
            var analyzers = GetCompatibleAnalyzers(analysisParams);

            var platformString = analysisParams.PlatformAsString;

            var context = new TextureAnalysisContext
            {
                // Importer set in loop
                // ImporterPlatformSettings set in loop
                // Texture set in loop
                Params = analysisParams
            };

            var assetPaths = GetAssetPathsByFilter("t:texture, a:assets", context);

            progress?.Start("Finding Textures", "Search in Progress...", assetPaths.Length);

            var issues = new List<ReportItem>();

            foreach (var assetPath in assetPaths)
            {
                if (progress?.IsCancelled ?? false)
                    return AnalysisResult.Cancelled;

                var textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (textureImporter == null)
                {
                    continue; // skip render textures
                }

                context.Importer = textureImporter;
                context.ImporterPlatformSettings = textureImporter.GetPlatformTextureSettings(platformString);
                context.Texture = AssetDatabase.LoadAssetAtPath<Texture>(assetPath);

                if (string.IsNullOrEmpty(context.Texture.name))
                    context.Name = Path.GetFileNameWithoutExtension(assetPath);
                else
                    context.Name = context.Texture.name;

#if PA_CAN_USE_COMPUTEMIPCHAINSIZE
                var format = (TextureFormat)context.ImporterPlatformSettings.format;
                if (context.ImporterPlatformSettings.format == TextureImporterFormat.Automatic)
                {
                    format = (TextureFormat)context.Importer.GetAutomaticFormat(context.Params.PlatformAsString);
                }

                context.Size = UnityEngine.Experimental.Rendering.GraphicsFormatUtility.ComputeMipChainSize(context.Texture.width, context.Texture.height, TextureUtils.GetTextureDepth(context.Texture), format, context.Texture.mipmapCount);
#else
                // This is not the correct size but we don't have access to the appropriate functionality on older versions to do much better without a lot more work.
                context.Size = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(context.Texture);
#endif
                var resolution = context.Texture.width + "x" + context.Texture.height;

                issues.Add(context.CreateInsight(IssueCategory.Texture, context.Texture.name)
                    .WithCustomProperties(
                        new object[(int)TextureProperty.Num]
                        {
                            context.Importer.textureShape,
                            context.Importer.textureType,
                            context.ImporterPlatformSettings.format,
                            context.ImporterPlatformSettings.textureCompression,
                            context.Importer.mipmapEnabled,
                            context.Importer.isReadable,
                            resolution,
                            context.Size,
                            context.Importer.streamingMipmaps
                        })
                    .WithLocation(new Location(assetPath)));

                foreach (var analyzer in analyzers)
                {
                    analysisParams.OnIncomingIssues(analyzer.Analyze(context));
                }

                progress?.Advance();
            }

            if (issues.Count > 0)
                context.Params.OnIncomingIssues(issues);

            progress?.Clear();

            return AnalysisResult.Success;
        }
    }
}
