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
    private float _steadyStateLogTimer;

    public int CurrentVersion => _current;
    public bool HasPendingSwitch => _pending.HasValue;

    private void Awake()
    {
        Debug.Log($"[VGVS-DEBUG] {name}: Awake() -- selfActiveInHierarchy={gameObject.activeInHierarchy} " +
                  $"versions.Length={(versions == null ? -1 : versions.Length)} startingVersion={startingVersion}", this);
        _current = Mathf.Clamp(startingVersion, 0, versions.Length - 1);
        ApplyImmediate(_current);
    }

    private void OnEnable()
    {
        Debug.Log($"[VGVS-DEBUG] {name}: OnEnable() -- component/GameObject just became enabled/active.", this);
    }

    private void OnDisable()
    {
        Debug.Log($"[VGVS-DEBUG] {name}: OnDisable() -- component/GameObject just became disabled/inactive. " +
                  $"Update() will NOT run again until this is externally re-enabled.", this);
    }

    /// Queues a switch to the given version. Applies immediately if the object is already
    /// outside both eye cones; otherwise waits until it leaves them.
    public void RequestSwitchTo(int versionIndex)
    {
        Debug.Log($"[VGVS-DEBUG] {name}: RequestSwitchTo({versionIndex}) ENTER -- current={_current} pending={_pending} " +
                  $"selfActiveInHierarchy={gameObject.activeInHierarchy} enabled={enabled}", this);

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

        // Defensive enforcement: whatever else might flip a version's active state outside this
        // component's own control, force mutual exclusivity back to the current version every
        // frame. Cheap (just SetActive calls, which are no-ops when already in the right state)
        // and guarantees only one version is ever actually active, regardless of the cause.
        for (int i = 0; i < versions.Length; i++)
        {
            if (versions[i] != null && versions[i].activeSelf != (i == _current))
            {
                Debug.LogWarning($"[VGVS-DEBUG] {name}: versions[{i}] '{versions[i].name}' activeSelf drifted to " +
                                  $"{versions[i].activeSelf} while current={_current} -- forcing it back. Something " +
                                  $"outside this switcher changed it.", versions[i]);
                versions[i].SetActive(i == _current);
            }
        }

        // Steady-state ground-truth dump, independent of pending switches, so we can catch
        // activeSelf/activeInHierarchy/Renderer.isVisible drifting out of sync with each other
        // even when nothing is currently being requested.
        _steadyStateLogTimer += Time.deltaTime;
        if (_steadyStateLogTimer >= 1f)
        {
            _steadyStateLogTimer = 0f;
            LogFullState("steady-state");
        }
    }

    private void TryApplyPending()
    {
        if (!_pending.HasValue) return;

        // Must check both the outgoing AND incoming version's bounds -- if only the current
        // version's bounds are checked, a pending version with different geometry/extents can
        // already be in view even while the current one is safely offscreen, and the swap would
        // make it pop in visibly.
        Bounds bounds = ComputeBounds(_current);
        if (versions[_pending.Value] != null)
            bounds.Encapsulate(ComputeBounds(_pending.Value));

        bool stillVisible = EyeVisibilityCones.IsSphereVisible(bounds.center, bounds.extents.magnitude + visibilityMargin);
        Debug.Log($"[VGVS-DEBUG] {name}: TryApplyPending pending={_pending} current={_current} " +
                  $"boundsCenter={bounds.center:F2} boundsRadius={bounds.extents.magnitude + visibilityMargin:F2} " +
                  $"stillVisible={stillVisible} selfActive={gameObject.activeInHierarchy}", this);

        if (stillVisible)
            return; // either version still on-screen -- keep waiting.

        ApplyImmediate(_pending.Value);
        _pending = null;
    }

    private void ApplyImmediate(int index)
    {
        for (int i = 0; i < versions.Length; i++)
            if (versions[i] != null) versions[i].SetActive(i == index);

        Debug.Log($"{name}: switched to version {index}.", this);

        for (int i = 0; i < versions.Length; i++)
        {
            if (versions[i] == gameObject && i != index)
            {
                Debug.LogWarning($"[VGVS-DEBUG] {name}: switching to version {index} deactivates this switcher's OWN " +
                                  $"GameObject (versions[{i}] is the host) -- Update()/future pending switches will stop " +
                                  $"firing until something reactivates it directly.", this);
            }
        }

        _current = index;
        LogFullState("after ApplyImmediate");
        onVersionApplied?.Invoke(index);
    }

    /// Ground-truth dump: for each version, compares the GameObject-level active flags against
    /// Renderer.isVisible (Unity's actual "is this drawing pixels to a camera right now" signal,
    /// independent of activeInHierarchy). If activeInHierarchy says false but isVisible says true
    /// anywhere, that's proof something is rendering this geometry through a path that doesn't
    /// respect this object's active state (e.g. a static/combined batch, or a duplicate renderer
    /// elsewhere sharing the same mesh/material).
    private void LogFullState(string context)
    {
        for (int i = 0; i < versions.Length; i++)
        {
            GameObject v = versions[i];
            if (v == null)
            {
                Debug.Log($"[VGVS-DEBUG] ({context}) {name}: versions[{i}] is null.", this);
                continue;
            }

            Renderer[] renderers = v.GetComponentsInChildren<Renderer>(true);
            int visibleCount = 0;
            string rendererDetail = "";
            foreach (Renderer r in renderers)
            {
                if (r.isVisible) visibleCount++;
                rendererDetail += $"\n      - '{r.name}' enabled={r.enabled} isVisible={r.isVisible} " +
                                   $"goActiveInHierarchy={r.gameObject.activeInHierarchy}";
            }

            bool mismatch = !v.activeInHierarchy && visibleCount > 0;
            string tag = mismatch ? "MISMATCH -- RENDERING WHILE activeInHierarchy=FALSE" : "ok";
            Debug.Log($"[VGVS-DEBUG] ({context}) {name}: versions[{i}] '{v.name}' current={(i == _current)} " +
                      $"activeSelf={v.activeSelf} activeInHierarchy={v.activeInHierarchy} " +
                      $"renderers={renderers.Length} visibleRenderers={visibleCount} [{tag}]{rendererDetail}", v);
        }
    }

    private Bounds ComputeBounds(int versionIndex)
    {
        Renderer[] source = boundsSource != null && boundsSource.Length > 0
            ? boundsSource
            : versions[versionIndex] != null
                // includeInactive: true -- every non-current version is SetActive(false), so the
                // default (active-only) search would find nothing and silently fall back to a
                // tiny placeholder bounds at this object's own position.
                ? versions[versionIndex].GetComponentsInChildren<Renderer>(true)
                : Array.Empty<Renderer>();

        if (source.Length == 0) return new Bounds(transform.position, Vector3.one * 0.25f);

        Bounds bounds = source[0].bounds;
        for (int i = 1; i < source.Length; i++) bounds.Encapsulate(source[i].bounds);
        return bounds;
    }
}
