>[!WARNING]
> Unity versions 6.4 and later include Project Auditor by default. For the most recent documentation, refer to [Project Auditor in the Unity Manual](https://docs.unity3d.com/6000.4/Documentation/Manual/project-auditor/project-auditor.html).

# Project Auditor package

Get insights on issues with the scripts, assets, and settings in your project.

Project Auditor is a suite of static analysis tools for Unity projects. It reports insights and issues about the scripts, assets, and code in your project, and groups insights into a report that you can view in the dedicated Project Auditor window.

| **Topic** | **Description** |
| :--- | :--- |
| **[Project Auditor introduction](project-auditor-introduction.md)**|Understand the Project Auditor and the type of data it collects.|
| **[Analyze your project](analyze-project.md)**|Collect data about your project and identify areas to optimize.|
| **[Project issues](project-issues.md)**|Resolve common issues with your project.|
| **[Programming with Project Auditor](project-auditor-programming.md)**|Use the Project Auditor API to customize data collection.|
| **[Project Auditor window reference](project-auditor-window.md)**|Reference for the window controls.|
| **[Project Auditor settings reference](project-auditor-settings-reference.md)**|Reference for the Preferences and Project Settings window.|

## Installation

You can use the following link to open the Unity Editor and install Project Auditor via the Package Manager directly: [Open the Unity Editor and install Project Auditor](com.unity3d.kharma:upmpackage/com.unity.project-auditor)

Alternatively, follow the instructions in the [Add and remove UPM packages or feature sets](xref:um-upm-ui-actions) documentation. You can install the package using the [package registry list](xref:um-upm-ui-install), or by using `com.unity.project-auditor` as the name when [adding a registry package by name](xref:um-upm-ui-quick).

>[!IMPORTANT]
>If you're using Unity 6.4 or later, Project Auditor is included in the Editor installation, and you can open it from **Window** &gt; **Analysis** &gt; **Project Auditor** without installing this package. However, you must first install the [Project Auditor Rules](https://docs.unity3d.com/Packages/com.unity.project-auditor-rules@latest) package. For more information, refer to the Unity User Manual documentation [Analyze your project with Project Auditor](https://docs.unity3d.com/6000.4/Documentation/Manual/project-auditor/analyze-project.html).

## Additional resources

* [Unity Profiler](xref:um-profiler)
* [Optimizing your code for managed memory](xref:um-performance-garbage-collection-best-practices)