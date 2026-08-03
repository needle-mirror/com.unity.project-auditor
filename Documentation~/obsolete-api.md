>[!NOTE]
> This documentation is for the Project Auditor package, compatible with Unity 6.3 and earlier. Unity versions 6.4 and later include Project Auditor built-in by default. You can open it from **Window** &gt; **Analysis** &gt; **Project Auditor**. For the documentation on the built-in Project Auditor included in Unity 6.4 and later, refer to the Unity User Manual documentation [Analyze your project with Project Auditor](https://docs.unity3d.com/6000.4/Documentation/Manual/project-auditor/analyze-project.html).

# Identify obsolete API between Unity versions

Some Unity API becomes obsolete between versions of the Unity Editor. When you migrate a project to a more recent Editor version, obsolete API can block compilation, or warn you that an API is nearing the end of support.

To identify obsolete API:

1. In the Project Auditor window, select the [**Code** view](code-view-reference).
2. In the **Areas** filter, select the **Upgrade** checkbox.
3. In the **Show** filter, select the **Upgrade Recommendations** checkbox.
4. In the **Target version** dropdown, select the Unity version you want to upgrade this project to.

Obsolete APIs appear in the **Issue** list. To learn how to upgrade a specific obsolete API, select the warning in the issues list and review the **Recommendation** section for next steps.

## Additional resources

* [Run Project Auditor from the command line](run-from-command-line)
* [`Report` API documentation](ScriptRef:Unity.ProjectAuditor.Editor.Report)