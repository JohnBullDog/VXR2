using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector-time checks for the mistakes someone new to this system is most likely to make --
/// these either crash at runtime, fail silently, or produce behavior that doesn't match what the
/// Scene view shows, so surfacing them here beats finding out from a Console error during Play.
/// </summary>
[CustomEditor(typeof(VisibilityGatedVersionSwitcher))]
public class VisibilityGatedVersionSwitcherEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var switcher = (VisibilityGatedVersionSwitcher)target;
        SerializedProperty versionsProp = serializedObject.FindProperty("versions");
        SerializedProperty startingVersionProp = serializedObject.FindProperty("startingVersion");

        int versionCount = versionsProp.arraySize;

        if (versionCount == 0)
        {
            EditorGUILayout.HelpBox(
                "Versions is empty. This switcher won't crash, but it will never do anything: " +
                "Current Version ends up -1 and every RequestSwitchTo(...) call is rejected as " +
                "\"index out of range\" -- assign at least one version GameObject.",
                MessageType.Error);
            return;
        }

        int nullCount = 0;
        int activeCount = 0;
        bool hasDuplicate = false;
        var seen = new HashSet<GameObject>();
        var assetEntries = new List<(int index, GameObject go)>();

        for (int i = 0; i < versionCount; i++)
        {
            var go = versionsProp.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
            if (go == null) { nullCount++; continue; }
            if (!seen.Add(go)) hasDuplicate = true;
            if (go.activeSelf) activeCount++;
            if (EditorUtility.IsPersistent(go)) assetEntries.Add((i, go));
        }

        if (assetEntries.Count > 0)
        {
            var names = string.Join(", ", assetEntries.ConvertAll(e => $"[{e.index}] '{e.go.name}'"));
            EditorGUILayout.HelpBox(
                $"{names} {(assetEntries.Count == 1 ? "is" : "are")} a PREFAB ASSET, not a scene " +
                "instance. SetActive() on it won't affect anything rendered in this scene -- drag " +
                "the scene instance (from the Hierarchy), not the asset (from the Project window), " +
                "into this slot. This is exactly the kind of bug where Renderer.isVisible reads " +
                "False forever while something else entirely stays visible on screen.",
                MessageType.Error);
        }

        if (nullCount > 0)
        {
            EditorGUILayout.HelpBox(
                $"{nullCount} entr{(nullCount == 1 ? "y" : "ies")} in Versions " +
                $"{(nullCount == 1 ? "is" : "are")} unassigned. Switching to that index will " +
                "silently do nothing (null entries are skipped), which usually means the list " +
                "wasn't fully set up.",
                MessageType.Warning);
        }

        if (hasDuplicate)
        {
            EditorGUILayout.HelpBox(
                "The same GameObject appears more than once in Versions. Switching to one of " +
                "its indices will also enable/disable it at the other index, which is almost " +
                "certainly not intended.",
                MessageType.Warning);
        }

        if (activeCount > 1)
        {
            EditorGUILayout.HelpBox(
                $"{activeCount} version GameObjects are active at once right now. Only one " +
                "should be enabled -- Awake() will force this to just Starting Version when " +
                "the game starts, so what you see in the Scene view currently doesn't match " +
                "what will actually run.",
                MessageType.Warning);
        }
        else if (activeCount == 0)
        {
            EditorGUILayout.HelpBox(
                "No version GameObject is currently active. That's fine at edit time -- " +
                "Awake() enables Starting Version on Play -- but nothing from this switcher " +
                "will be visible in the Scene view until then.",
                MessageType.Info);
        }

        int startingVersion = startingVersionProp.intValue;
        if (startingVersion < 0 || startingVersion >= versionCount)
        {
            EditorGUILayout.HelpBox(
                $"Starting Version ({startingVersion}) is out of range (0..{versionCount - 1}) " +
                "and will be clamped at runtime, which may not be the version you intended.",
                MessageType.Warning);
        }

        // Only meaningful for an instance actually placed in a scene -- a prefab asset edited in
        // isolation (Project view / prefab mode outside any scene) has no scene to search.
        if (!EditorUtility.IsPersistent(switcher) && switcher.gameObject.scene.IsValid())
        {
            var cones = FindFirstObjectByType<EyeVisibilityCones>();
            if (cones == null)
            {
                EditorGUILayout.HelpBox(
                    "No EyeVisibilityCones found in this scene. Visibility checks fail open " +
                    "when none exists, so RequestSwitchTo will apply every swap immediately " +
                    "instead of waiting for the player to look away. Add one EyeVisibilityCones " +
                    "component (usually on/near the XR camera rig) if that's not what you want.",
                    MessageType.Warning);
            }
        }
    }
}
