using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Unity.ProjectAuditor.Editor.Core;
using Unity.ProjectAuditor.Editor.Utils;
using UnityEditor;
using UnityEditor.Playables;
using UnityEngine;

namespace Unity.ProjectAuditor.Editor
{
    /// <summary>
    /// Provides numeric parameters with arbitrary names and defaults, with optional overrides for individual target build platforms.
    /// </summary>
    /// <remarks>
    /// When deciding whether to report a diagnostic issue, some Analyzers need to compare a value extracted from the project with an arbitrary threshold value:
    /// For example, when reporting that the project contains textures larger than some specified size.
    /// DiagnosticParams are saved in the ProjectAuditorSettings asset, which should be included in the project's version control repository.
    /// By default, every <see cref="AnalysisParams"/> is constructed with a copy of the global DiagnosticParams for use during analysis.
    /// Individual parameters can be overridden on different platforms (for example, to set different texture size thresholds on different target platforms).
    /// DiagnosticParams will return the parameter value that corresponds to the target platform, or the default parameter if there is no override for the platform.
    /// </remarks>
    [Serializable]
    public sealed class DiagnosticParams : ISerializationCallbackReceiver
    {
        #region PlatformParams
        [Serializable]
        internal class PlatformParams
        {
            [JsonIgnore]
            public BuildTargetGroup PlatformGroup;

            [JsonProperty("platformgroup")]
            string PlatformString
            {
                get => PlatformGroup.ToString();
                set => PlatformGroup = (BuildTargetGroup)Enum.Parse(typeof(BuildTargetGroup), value);
            }

            // A string-keyed Dictionary is not a particularly efficient data structure. However:
            // - We need strings for serialization, so we can't just hash strings to make keys then throw the string away
            // - We want DiagnosticParams to be arbitrarily-definable by any future module without modifying core code, which rules out an enum
            // It can stay. For now.
            [JsonProperty("params")]
            Dictionary<string, int> m_Params = new Dictionary<string, int>();

            internal int ParamsCount => (m_Params == null) ? 0 : m_Params.Count;

            // Can't use KeyValuePair<string, int> because Unity won't serialize generic types. So we'll make a concrete type.
            [Serializable]
            internal struct ParamKeyValue
            {
                public string Key;
                public int Value;

                public ParamKeyValue(string key, int value)
                {
                    Key = key;
                    Value = value;
                }
            }

            [NonReorderable][JsonIgnore][SerializeField]
            List<ParamKeyValue> m_SerializedParams = new List<ParamKeyValue>();

            public void DoGUI()
            {
                String[] keys = m_Params.Keys.ToArray();
                float maxWidth = 0.0f;
                foreach (var key in keys)
                {
                    float width = EditorStyles.label.CalcSize(new GUIContent(ProjectAuditorSettings.instance.DiagnosticParams.GetUserFriendlyName(key))).x;
                    if (width > maxWidth)
                        maxWidth = width;
                }

                EditorGUI.indentLevel++;
                var plaformString = PlatformString == "NoTarget"
                    ? "Default"
                    : Utils.Formatting.GetModernBuildTargetName(PlatformGroup);
                EditorGUILayout.LabelField(plaformString, EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                foreach (var key in keys)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(EditorGUIUtility.TrTextContent(ProjectAuditorSettings.instance.DiagnosticParams.GetUserFriendlyName(key), ProjectAuditorSettings.instance.DiagnosticParams.GetTooltip(key)), GUILayout.MinWidth(maxWidth + 40), GUILayout.ExpandWidth(false));
                    m_Params[key] = EditorGUILayout.IntField(m_Params[key], GUILayout.ExpandWidth(true));
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;

				GUILayout.Space(10);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
				if (GUILayout.Button("Reset to defaults", GUILayout.Width(200)))
                {
                    if (EditorUtility.DisplayDialog("Are you sure?", "Resetting the settings to defaults will lose your current settings, are you sure you wish to continue?", "Yes", "No"))
                    {
                        foreach (var key in keys)
                        {
                            m_Params[key] = ProjectAuditorSettings.instance.DiagnosticParams.GetDefault(key);
                        }
                        // Remove focus from IntFields to make sure they update their displayed value
                        GUIUtility.keyboardControl = 0;
                    }
                }
                EditorGUILayout.EndHorizontal();

                EditorGUI.indentLevel--;
            }

            public PlatformParams()
            {
            }

            public PlatformParams(BuildTargetGroup platformGroup) : this()
            {
                PlatformGroup = platformGroup;
            }

            public PlatformParams(PlatformParams copyFrom) : this()
            {
                PlatformGroup = copyFrom.PlatformGroup;

                foreach (var key in copyFrom.m_Params.Keys)
                {
                    SetParameter(key, copyFrom.m_Params[key]);
                }
            }

            public bool TryGetParameter(string paramName, out int paramValue)
            {
                return m_Params.TryGetValue(paramName, out paramValue);
            }

            public void SetParameter(string paramName, int paramValue)
            {
                m_Params[paramName] = paramValue;
            }

            internal void RemoveParameter(string paramName)
            {
                m_Params.Remove(paramName);
            }

            public void PreSerialize()
            {
                m_SerializedParams.Clear();
                foreach (var key in m_Params.Keys)
                {
                    m_SerializedParams.Add(new ParamKeyValue(key, m_Params[key]));
                }
            }

            public void PostDeserialize()
            {
                m_Params.Clear();
                foreach (var kvp in m_SerializedParams)
                {
                    m_Params[kvp.Key] = kvp.Value;
                }

                m_SerializedParams.Clear();
            }

            // For testing purposes only
            internal IEnumerable<string> GetKeys()
            {
                return m_Params.Keys;
            }
        }
        #endregion

        [NonReorderable][JsonProperty("paramsStack")][SerializeField]
        internal List<PlatformParams> m_ParamsStack = new List<PlatformParams>();

        [JsonProperty][SerializeField]
        internal int CurrentParamsIndex;

        /// <summary>
        /// Default constructor
        /// </summary>
        public DiagnosticParams()
        {
            // We treat BuildTargetGroup.Unknown as the default value fallback if there isn't a platform-specific override
            m_ParamsStack.Add(new PlatformParams(BuildTargetGroup.Unknown));
        }

        private void InitPlatformParams()
        {
            var buildTargets = Enum.GetValues(typeof(BuildTarget)).Cast<BuildTarget>();
            var supportedBuildTargets = buildTargets.Where(bt =>
                BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(bt), bt)).ToList();
            var supportedGroups  = new HashSet<BuildTargetGroup>();
            foreach (var buildTarget in supportedBuildTargets)
                supportedGroups.Add(BuildPipeline.GetBuildTargetGroup(buildTarget));
            var supportedBTGroups = supportedGroups.ToList();
            supportedBTGroups.Sort((t1, t2) =>
                String.Compare(t1.ToString(), t2.ToString(), StringComparison.Ordinal));

            // Add at the beginning of the list, after sorting the other options
            supportedBTGroups.Insert(0, BuildTargetGroup.Unknown);

            foreach (var target in supportedBTGroups)
            {
                bool found = false;
                foreach (var platformParams in m_ParamsStack)
                {
                    if (platformParams.PlatformGroup == target)
                        found = true;
                }

                if (!found)
                {
                    PlatformParams newPlatform = new PlatformParams(m_ParamsStack[0]);
                    newPlatform.PlatformGroup = target;
                    m_ParamsStack.Add(newPlatform);
                }
            }
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="copyFrom">The DiagnosticParams object to copy from.</param>
        public DiagnosticParams(DiagnosticParams copyFrom)
        {
            foreach (var platformParams in copyFrom.m_ParamsStack)
            {
                m_ParamsStack.Add(new PlatformParams(platformParams));
            }
        }

        /// <summary>
        /// Draw project settings GUI for a specific BuildTargetGroup.
        /// </summary>
        /// <param name="btg">The BuildTargetGroup to draw the project settings UI for.</param>
        public void DoGUI(BuildTargetGroup btg)
        {
            foreach (var platformParams in m_ParamsStack)
            {
                if (platformParams.PlatformGroup == btg)
                {
                    platformParams.DoGUI();
                    return;
                }
            }

            InitPlatformParams();
        }

        /// <summary>
        /// Sets the target analysis platform. When retrieving parameters, DiagnosticParams will first check the values specific to this platform.
        /// </summary>
        /// <param name="platform">Target platform for analysis.</param>
        public void SetAnalysisPlatform(BuildTarget platform)
        {
            EnsureDefaults();

            for (int i = 0; i < m_ParamsStack.Count; ++i)
            {
                if (m_ParamsStack[i].PlatformGroup == BuildPipeline.GetBuildTargetGroup(platform))
                {
                    CurrentParamsIndex = i;
                    return;
                }
            }

            // We didn't find this platform in the platform stack yet, so let's create it.
            m_ParamsStack.Add(new PlatformParams(BuildPipeline.GetBuildTargetGroup(platform)));
            CurrentParamsIndex = m_ParamsStack.Count - 1;
        }

        /// <summary>
        /// Register a parameter by declaring its name and default value. Parameters are registered on the "default" platform, so are available for retrieval on every target platform.
        /// </summary>
        /// <param name="paramName">Parameter name.</param>
        /// <param name="userFriendlyName">A user friendly name to show in project settings.</param>
        /// <param name="tooltip">Text to show on a tooltip in project settings.</param>
        /// <param name="defaultValue">The default value this parameter will have, unless it has already been register.</param>
        public void RegisterParameter(string paramName, string userFriendlyName, string tooltip, int defaultValue)
        {
            m_UserFriendlyNames[paramName] = userFriendlyName;
            m_Tooltips[paramName] = tooltip;
            // Does this check mean that parameter default values can't be automatically changed if they change in future versions of the package?
            // Yep. Nothing is perfect. This is better than the risk of over-writing values that users may have tweaked.
            if (!m_ParamsStack[0].TryGetParameter(paramName, out var paramValue))
            {
                // We didn't find the parameter in the defaults. So add it.
                m_ParamsStack[0].SetParameter(paramName, defaultValue);
            }
        }

        /// <summary>
        /// Get the value of a named parameter. The parameter must have previously been registered with the RegisterParameter method.
        /// </summary>
        /// <param name="paramName">Parameter name to look up.</param>
        /// <returns>The parameter value for the currently-set analysis platform.</returns>
        public int GetParameter(string paramName)
        {
            int paramValue;

            // Try the params for the current analysis platform
            if (CurrentParamsIndex > 0 && CurrentParamsIndex < m_ParamsStack.Count)
            {
                if (m_ParamsStack[CurrentParamsIndex].TryGetParameter(paramName, out paramValue))
                    return paramValue;
            }

            // Try the default
            if (m_ParamsStack[0].TryGetParameter(paramName, out paramValue))
                return paramValue;

            // We didn't find the parameter in the rules.
            throw new Exception($"Diagnostic parameter '{paramName}' not found. Check that it is properly registered");
        }

        /// <summary>
        /// Set the value of a named parameter for a given analysis platform.
        /// </summary>
        /// <param name="paramName">Parameter name to set.</param>
        /// <param name="userFriendlyName">A user friendly name to show in project settings.</param>
        /// <param name="tooltip">Text to show on a tooltip in project settings.</param>
        /// <param name="value">Value to set the parameter to.</param>
        /// <param name="platform">Analysis target platform for which to set the value. Defaults to BuildTarget.NoTarget which sets the value for the default platform.</param>
        public void SetParameter(string paramName, string userFriendlyName, string tooltip, int value, BuildTargetGroup platform = BuildTargetGroup.Unknown)
        {
            foreach (var platformParams in m_ParamsStack)
            {
                if (platformParams.PlatformGroup == platform)
                {
                    platformParams.SetParameter(paramName, value);
                    return;
                }
            }

            m_UserFriendlyNames[paramName] = userFriendlyName;
            m_Tooltips[paramName] = tooltip;

            var newParams = new PlatformParams(platform);
            newParams.SetParameter(paramName, value);
            m_ParamsStack.Add(newParams);
        }

        /// <summary>
        /// Unity calls this method automatically before serialization.
        /// </summary>
        public void OnBeforeSerialize()
        {
            EnsureDefaults();

            foreach (var platformParams in m_ParamsStack)
            {
                platformParams.PreSerialize();
            }
        }

        /// <summary>
        /// Unity calls this method automatically after serialization.
        /// </summary>
        public void OnAfterDeserialize()
        {
            foreach (var platformParams in m_ParamsStack)
            {
                platformParams.PostDeserialize();
            }

            EnsureDefaults();
        }


        Dictionary<string, string> m_UserFriendlyNames = new Dictionary<string, string>();
        Dictionary<string, string> m_Tooltips = new Dictionary<string, string>();
        /// <summary>
        /// Get the user friendly name for this parameter.
        /// </summary>
        /// <param name="paramKey">Parameter key to look up.</param>
        /// <returns>The user friendly name if the paramKey can be found, or the paramKey itself if not.</returns>
        public string GetUserFriendlyName(string paramKey)
        {
            if (m_UserFriendlyNames.ContainsKey(paramKey))
                return m_UserFriendlyNames[paramKey];
            else
                return paramKey;
        }

        /// <summary>
        /// Get the tooltip for this parameter.
        /// </summary>
        /// <param name="paramKey">Parameter key to look up.</param>
        /// <returns>The tooltip if the paramKey can be found, or null if not.</returns>
        public string GetTooltip(string paramKey)
        {
            if (m_Tooltips.ContainsKey(paramKey))
                return m_Tooltips[paramKey];
            else
                return null;
        }

        /// <summary>
        /// Get the default value for this parameter.
        /// </summary>
        /// <param name="paramKey">Parameter key to look up.</param>
        /// <returns>The default value for the key if it can be found, or 0 if not.</returns>
        public int GetDefault(string paramKey)
        {
            if (m_ParamsStack[0].TryGetParameter(paramKey, out var paramValue))
            {
                return paramValue;
            }

            return 0;
        }

        internal void RegisterParameters()
        {
            m_ParamsStack[0].PlatformGroup = BuildTargetGroup.Unknown;
            foreach (var type in TypeCache.GetTypesDerivedFrom(typeof(ModuleAnalyzer)))
            {
                if (type.IsAbstract)
                    continue;

                // create a temporary analyzer instance
                var instance = Activator.CreateInstance(type) as ModuleAnalyzer;
                instance.RegisterParameters(this);
            }
        }

        void EnsureDefaults()
        {
            if (m_ParamsStack == null || m_ParamsStack.Count == 0)
            {
                m_ParamsStack = new List<PlatformParams>();
                m_ParamsStack.Add(new PlatformParams(BuildTargetGroup.Unknown));
            }

            if (m_ParamsStack[0].ParamsCount == 0)
            {
                RegisterParameters();
            }

            if (m_ParamsStack.Count > 1)
            {
                // When adding a new DiagnosticParameter, the default will be registered, but any further serialized
                // PlatformParams need to have some manual intervention to apply the addition.
                // All keys should exist for all PlatformParams, so we just need to work out the difference once.
                var keysDefault = m_ParamsStack[0].GetKeys();
                var keysFirstNonDefault = m_ParamsStack[1].GetKeys();

                var keysThatNeedAdding = keysDefault.Except(keysFirstNonDefault).ToList();
                if (keysThatNeedAdding.Any())
                {
                    for (var i = 1; i < m_ParamsStack.Count; ++i)
                    {
                        foreach (var key in keysThatNeedAdding)
                            m_ParamsStack[i].SetParameter(key, ProjectAuditorSettings.instance.DiagnosticParams.GetDefault(key));
                    }
                }
            }

            // Next, we need to remove any params that are no longer registered, but remain in serialized data.
            // Make use of the fact that tooltips aren't serialized to see what should stay.
            if (m_Tooltips.Any())
            {
                var keysThatNeedRemoving = m_ParamsStack[0].GetKeys().Except(m_Tooltips.Keys).ToList();
                var numKeysToRemove = keysThatNeedRemoving.Count;

                if (numKeysToRemove > 0)
                {
                    foreach (var platformParams in m_ParamsStack)
                    {
                        foreach (var key in keysThatNeedRemoving)
                            platformParams.RemoveParameter(key);
                    }
                }
            }
        }

        // For testing purposes only
        internal void ClearAllParameters()
        {
            m_ParamsStack.Clear();
            m_ParamsStack.Add(new PlatformParams(BuildTargetGroup.Unknown));
        }

        // For testing purposes only
        internal int CountParameters()
        {
            var foundParams = new HashSet<string>();

            foreach (var platformParams in m_ParamsStack)
            {
                foreach (var key in platformParams.GetKeys())
                {
                    foundParams.Add(key);
                }
            }

            return foundParams.Count;
        }
    }
}
