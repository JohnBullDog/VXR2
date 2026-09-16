using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Holds several "versions" of the same object (each one a GameObject subtree, enabled/disabled
/// as a unit) and switches which version is active -- but only once none of it is currently
/// inside either eye's <see cref="EyeVisibilityCones"/>, so the swap never pops visibly while
/// the player is looking at it. Call <see cref="RequestSwitchTo"/> from whatever decides when a
/// version change should happen (a trigger volume, a timer, a gameplay event, etc.) -- this
/// component only owns the "wait until it's actually safe, then apply it instantly" part.
/// </summary>
public class VisibilityGatedVersionSwitcher : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Each entry is everything that belongs to one version of this object.")]
    private GameObject[] versions;

    [SerializeField]
    [Tooltip("Renderers used for the visibility check. Leave empty to auto-compute from the current version's renderers each check.")]
    private Renderer[] boundsSource;

    [SerializeField]
    private int startingVersion;

    [SerializeField]
    [Tooltip("Extra margin (meters) added to the bounds radius, so a swap waits until the object is a bit further out of view.")]
    private float visibilityMargin = 0.1f;

    [Serializable]
    public class VersionAppliedEvent : UnityEvent<int> { }

    public VersionAppliedEvent onVersionApplied;

    private int _current;
    private int? _pending;

    public int CurrentVersion => _current;
    public bool HasPendingSwitch => _pending.HasValue;

    private void Awake()
    {
        _current = Mathf.Clamp(startingVersion, 0, versions.Length - 1);
        ApplyImmediate(_current);
    }

    /// Queues a switch to the given version. Applies immediately if the object is already
    /// outside both eye cones; otherwise waits until it leaves them.
    public void RequestSwitchTo(int versionIndex)
    {
        if (versionIndex < 0 || versionIndex >= versions.Length)
        {
            Debug.LogWarning($"{name}: RequestSwitchTo({versionIndex}) ignored -- index out of range (0..{versions.Length - 1}).", this);
            return;
        }
        if (versionIndex == _current)
        {
            Debug.Log($"{name}: RequestSwitchTo({versionIndex}) received but already on version {versionIndex} -- no-op.", this);
            _pending = null;
            return;
        }

        _pending = versionIndex;
        Debug.Log($"{name}: RequestSwitchTo({versionIndex}) received -- will apply once out of view.", this);
        TryApplyPending();
    }

    private void Update()
    {
        if (_pending.HasValue) TryApplyPending();
    }

    private void TryApplyPending()
    {
        if (!_pending.HasValue) return;

        Bounds bounds = ComputeBounds();
        if (EyeVisibilityCones.IsSphereVisible(bounds.center, bounds.extents.magnitude + visibilityMargin))
            return; // still on-screen -- keep waiting.

        ApplyImmediate(_pending.Value);
        _pending = null;
    }

    private void ApplyImmediate(int index)
    {
        for (int i = 0; i < versions.Length; i++)
            if (versions[i] != null) versions[i].SetActive(i == index);

        Debug.Log($"{name}: switched to version {index}.", this);
        _current = index;
        onVersionApplied?.Invoke(index);
    }

    private Bounds ComputeBounds()
    {
        Renderer[] source = boundsSource != null && boundsSource.Length > 0
            ? boundsSource
            : versions[_current] != null
                ? versions[_current].GetComponentsInChildren<Renderer>()
                : Array.Empty<Renderer>();

        if (source.Length == 0) return new Bounds(transform.position, Vector3.one * 0.25f);

        Bounds bounds = source[0].bounds;
        for (int i = 1; i < source.Length; i++) bounds.Encapsulate(source[i].bounds);
        return bounds;
    }
}
