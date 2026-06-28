using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UISystem.Runtime;

namespace UISystem.Editor
{
    public static class UISystemValidator
    {
        private enum Severity
        {
            Info,
            Warning,
            Error
        }

        private readonly struct Issue
        {
            public Issue(Severity severity, string message, Object context)
            {
                Severity = severity;
                Message = message;
                Context = context;
            }

            public Severity Severity { get; }
            public string Message { get; }
            public Object Context { get; }
        }

        [MenuItem("Tools/UI System/Validate Open Scenes", false, 30)]
        public static void ValidateOpenScenes()
        {
            var issues = new List<Issue>();
            ValidateEventSystems(issues);
            ValidateManagers(issues);

            WriteIssues(issues);
            ShowSummary(issues);
        }

        private static void ValidateEventSystems(List<Issue> issues)
        {
            var eventSystems = Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            if (eventSystems.Length == 0)
            {
                var managers = Object.FindObjectsByType<UIManager>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

                bool hasSelfHealingManager = false;
                for (int i = 0; i < managers.Length; i++)
                {
                    if (managers[i] != null && managers[i].CreateEventSystemIfMissing)
                    {
                        hasSelfHealingManager = true;
                        break;
                    }
                }

                if (!hasSelfHealingManager)
                    issues.Add(new Issue(Severity.Warning, "No EventSystem was found in the open scenes.", null));

                return;
            }

            if (eventSystems.Length > 1)
            {
                issues.Add(new Issue(
                    Severity.Warning,
                    $"Multiple EventSystems found in open scenes: {eventSystems.Length}.",
                    eventSystems[0]
                ));
            }
        }

        private static void ValidateManagers(List<Issue> issues)
        {
            var managers = Object.FindObjectsByType<UIManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            if (managers.Length == 0)
            {
                issues.Add(new Issue(Severity.Info, "No UIManager was found in the open scenes.", null));
                return;
            }

            for (int i = 0; i < managers.Length; i++)
            {
                var manager = managers[i];
                if (manager == null)
                    continue;

                ValidateManagerLayers(manager, issues);
                ValidateViews(manager, issues);
            }
        }

        private static void ValidateManagerLayers(UIManager manager, List<Issue> issues)
        {
            AddMissingLayerIssue(manager, UILayerType.Background, manager.BackgroundLayer, issues);
            AddMissingLayerIssue(manager, UILayerType.HUD, manager.HudLayer, issues);
            AddMissingLayerIssue(manager, UILayerType.MainPanel, manager.MainPanelLayer, issues);
            AddMissingLayerIssue(manager, UILayerType.Popup, manager.PopupLayer, issues);
            AddMissingLayerIssue(manager, UILayerType.Toast, manager.ToastLayer, issues);
            AddMissingLayerIssue(manager, UILayerType.System, manager.SystemLayer, issues);
        }

        private static void AddMissingLayerIssue(
            UIManager manager,
            UILayerType layerType,
            Transform layer,
            List<Issue> issues
        )
        {
            if (layer != null)
                return;

            issues.Add(new Issue(
                Severity.Warning,
                $"{manager.name} is missing UI layer: {layerType}.",
                manager
            ));
        }

        private static void ValidateViews(UIManager manager, List<Issue> issues)
        {
            var views = manager.GetComponentsInChildren<UIView>(true);
            var viewIds = new Dictionary<string, UIView>();

            for (int i = 0; i < views.Length; i++)
            {
                var view = views[i];
                if (view == null)
                    continue;

                if (view.GetComponent<CanvasGroup>() == null)
                {
                    issues.Add(new Issue(
                        Severity.Error,
                        $"{view.name} is missing CanvasGroup.",
                        view
                    ));
                }

                string viewId = view.ViewId;
                if (string.IsNullOrWhiteSpace(viewId))
                {
                    issues.Add(new Issue(
                        Severity.Warning,
                        $"{view.name} has an empty ViewId. Runtime will fall back to the GameObject name.",
                        view
                    ));
                    continue;
                }

                if (viewIds.TryGetValue(viewId, out var existingView))
                {
                    issues.Add(new Issue(
                        Severity.Error,
                        $"Duplicate UIView id '{viewId}' under {manager.name}: {existingView.name} and {view.name}.",
                        view
                    ));
                    continue;
                }

                viewIds.Add(viewId, view);
            }
        }

        private static void WriteIssues(List<Issue> issues)
        {
            if (issues.Count == 0)
            {
                Debug.Log("[UISystemValidator] No issues found in open scenes.");
                return;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                Issue issue = issues[i];
                string message = $"[UISystemValidator] {issue.Message}";
                switch (issue.Severity)
                {
                    case Severity.Error:
                        Debug.LogError(message, issue.Context);
                        break;
                    case Severity.Warning:
                        Debug.LogWarning(message, issue.Context);
                        break;
                    default:
                        Debug.Log(message, issue.Context);
                        break;
                }
            }
        }

        private static void ShowSummary(List<Issue> issues)
        {
            int warnings = 0;
            int errors = 0;
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity == Severity.Warning)
                    warnings++;
                else if (issues[i].Severity == Severity.Error)
                    errors++;
            }

            var builder = new StringBuilder();
            builder.AppendLine("UISystem validation complete.");
            builder.AppendLine($"Errors: {errors}");
            builder.AppendLine($"Warnings: {warnings}");
            builder.AppendLine($"Total: {issues.Count}");

            if (issues.Count > 0)
                builder.AppendLine("See Console for details.");

            EditorUtility.DisplayDialog("UISystem Validator", builder.ToString(), "OK");
        }
    }
}
