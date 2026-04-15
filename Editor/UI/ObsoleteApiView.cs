using System.Collections.Generic;
using Unity.ProjectAuditor.Editor.UI.Framework;
using UnityEditor;
using System.Linq;
using UnityEngine;
using Unity.ProjectAuditor.Editor.Core;
using Unity.ProjectAuditor.Editor.Modules;
using System;
using Unity.ProjectAuditor.Editor.Utils;

namespace Unity.ProjectAuditor.Editor.UI
{
    class ObsoleteApiView : AnalysisView
    {
        public override string Description => "A list of obsolete API in all Unity versions.";
        public static string InfoTitle => $@"This view shows all obsolete API across all Unity versions.";

        Vector2 m_DetailsScrollPos;
        Vector2 m_RecommendationScrollPos;

        public ObsoleteApiView(ViewManager viewManager) : base(viewManager)
        {
        }

        public override void AddIssues(IEnumerable<ReportItem> allIssues)
        {
            // This view does not show issues from the report.
            // It shows obsolete API loaded from a database file in the Rules Package
            if (m_Issues.Count == 0 && ProjectAuditorRulesPackage.IsInstalled)
                base.AddIssues(ObsoleteLibrary.LibraryList);
        }

        protected override IReadOnlyCollection<ReportItem> GetIssuesToExport()
        {
            return m_Issues;
        }

        protected override void DrawInfo()
        {
            EditorGUILayout.LabelField(InfoTitle);
        }

        public override void DrawDetails(ReportItem[] selectedIssues)
        {
            var selectedDescriptors = selectedIssues.Select(i => i.GetCustomProperty(0)).Distinct().ToArray();

            ReportItem issue = null;
            if (selectedDescriptors.Length > 0)
                issue = selectedIssues[0];

            string selectedText = k_NoSelectionText;
            string recommendationText = k_NoSelectionText;
            if (selectedDescriptors.Length > 1)
            {
                selectedText = k_MultipleSelectionText;
                recommendationText = k_MultipleSelectionText;
            }
            else if (selectedDescriptors.Length == 1)
            {
                selectedText = issue.Description;
                recommendationText = issue.GetCustomProperty(ObsoleteApiProperty.Recommendation);
            }

            var numSelectedIDs = selectedDescriptors.Length;
            bool anySelectedIDs = numSelectedIDs > 0;

            EditorGUILayout.BeginVertical(GUILayout.Width(LayoutSize.FoldoutWidth));

            DrawDetailsHeader(DiagnosticView.Contents.Details,
                (selectedDescriptors.Length > 0) ? selectedText : null,
                anySelectedIDs);

            DrawDetailsContent(selectedText);

            GUILayout.Space(8);
            ChartUtil.DrawLine(m_2D);
            GUILayout.Space(8);

            DrawDetailsHeader(DiagnosticView.Contents.Recommendation,
                (selectedDescriptors.Length > 0) ? recommendationText : null,
                anySelectedIDs);

            DrawDetailsContent(recommendationText, ref m_RecommendationScrollPos);

            EditorGUILayout.EndVertical();
        }

        void DrawDetailsHeader(GUIContent title, string copyText, bool anySelectedIDs)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(title, SharedStyles.BoldLabel);
                if (anySelectedIDs)
                {
                    if (GUILayout.Button(DiagnosticView.Contents.CopyToClipboard, SharedStyles.TabButton,
                        GUILayout.Width(LayoutSize.CopyToClipboardButtonSize),
                        GUILayout.Height(LayoutSize.CopyToClipboardButtonSize)))
                    {
                        EditorInterop.CopyToClipboard(Formatting.StripRichTextTags(copyText));
                    }
                }
            }
        }

        void DrawDetailsContent(string text)
        {
            DrawDetailsContent(text, ref m_DetailsScrollPos);
        }

        void DrawDetailsContent(string text, ref Vector2 scrollPos)
        {
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));

            GUILayout.TextArea(text, SharedStyles.TextAreaWithDynamicSize, GUILayout.MaxHeight(LayoutSize.FoldoutMaxHeight));
            
            EditorGUILayout.EndScrollView();
        }
    }
}
