using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fans a single RequestSwitchTo call out to every listed switcher, so one Signal reaction
/// (or any other trigger) can drive a whole set of objects at once. Each switcher still applies
/// its own visibility gating independently -- this only centralizes the trigger, not the wait.
/// </summary>
public class VersionSwitchGroup : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Every switcher that should respond when this group is told to switch.")]
    private List<VisibilityGatedVersionSwitcher> switchers = new List<VisibilityGatedVersionSwitcher>();

    public void RequestSwitchTo(int versionIndex)
    {
        Debug.Log($"{name}: forwarding RequestSwitchTo({versionIndex}) to {switchers.Count} switcher(s).", this);
        foreach (VisibilityGatedVersionSwitcher switcher in switchers)
            if (switcher != null) switcher.RequestSwitchTo(versionIndex);
    }
}
