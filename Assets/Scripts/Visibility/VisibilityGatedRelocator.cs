using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Same idea as <see cref="VisibilityGatedVersionSwitcher"/> but for a persistent character
/// instead of a set of alternate meshes: holds several waypoints and teleports this object's
/// root transform to the requested one, but only once it's outside both eyes'
/// <see cref="EyeVisibilityCones"/>, so the jump never happens while the player is looking at it.
///
/// If the player keeps looking long enough that a requested move has been blocked for
/// <see cref="maxWaitSeconds"/>, this fires <see cref="onWaitTooLong"/> instead of forcing the
/// move itself -- wire that to something like <see cref="ForcedDarknessTransition"/> to kill the
/// lights and call <see cref="ForceApplyPending"/> once it's dark, rather than have the character
/// visibly teleport in front of the player.
/// </summary>
public class VisibilityGatedRelocator : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Ordered positions this object can be relocated to. RequestRelocateTo(index) moves here once safe.")]
    private Transform[] waypoints;

    [SerializeField]
    [Tooltip("Renderers used for the visibility check. Leave empty to auto-compute from this object's own renderers each check.")]
    private Renderer[] boundsSource;

    [SerializeField]
    private int startingWaypoint;

    [SerializeField]
    private float visibilityMargin = 0.3f;

    [SerializeField]
    [Tooltip("If a requested relocate has been blocked (player kept it in view) this long, stop waiting and fire onWaitTooLong instead of applying it directly.")]
    private float maxWaitSeconds = 8f;

    [Serializable]
    public class WaypointAppliedEvent : UnityEvent<int> { }

    public WaypointAppliedEvent onRelocated;
    public UnityEvent onWaitTooLong;

    private int _current;
    private int? _pending;
    private float _pendingWaitTimer;

    public int CurrentWaypoint => _current;
    public bool HasPendingRelocate => _pending.HasValue;

    private void Awake()
    {
        _current = Mathf.Clamp(startingWaypoint, 0, waypoints.Length - 1);
        if (waypoints.Length > 0 && waypoints[_current] != null)
        {
            transform.position = waypoints[_current].position;
            transform.rotation = waypoints[_current].rotation;
        }
    }

    /// Queues a relocate to the given waypoint. Applies immediately if already outside both eye
    /// cones; otherwise waits -- checking every frame -- until it's safe, same as the version
    /// switcher. Fires onWaitTooLong (without applying) if that takes longer than maxWaitSeconds.
    public void RequestRelocateTo(int waypointIndex)
    {
        if (waypointIndex < 0 || waypointIndex >= waypoints.Length || waypoints[waypointIndex] == null)
        {
            Debug.LogWarning($"{name}: RequestRelocateTo({waypointIndex}) ignored -- index out of range or unassigned.", this);
            return;
        }
        if (waypointIndex == _current)
        {
            _pending = null;
            return;
        }

        _pending = waypointIndex;
        _pendingWaitTimer = 0f;
    }

    private void Update()
    {
        if (!_pending.HasValue) return;

        _pendingWaitTimer += Time.deltaTime;

        Bounds bounds = ComputeBounds();
        bool stillVisible = EyeVisibilityCones.IsSphereVisible(bounds.center, bounds.extents.magnitude + visibilityMargin);

        if (!stillVisible)
        {
            ApplyImmediate(_pending.Value);
            return;
        }

        if (_pendingWaitTimer >= maxWaitSeconds)
        {
            // Stop polling every frame once we've handed off -- whatever's listening (e.g. a
            // darkness transition) is responsible for calling ForceApplyPending().
            onWaitTooLong?.Invoke();
            _pendingWaitTimer = float.NegativeInfinity;
        }
    }

    /// Applies the pending relocate immediately regardless of visibility. Meant to be called once
    /// something else (e.g. ForcedDarknessTransition) has made it safe to do so (lights out).
    public void ForceApplyPending()
    {
        if (_pending.HasValue) ApplyImmediate(_pending.Value);
    }

    private void ApplyImmediate(int index)
    {
        transform.position = waypoints[index].position;
        transform.rotation = waypoints[index].rotation;
        _current = index;
        _pending = null;
        _pendingWaitTimer = 0f;
        onRelocated?.Invoke(index);
    }

    private Bounds ComputeBounds()
    {
        Renderer[] source = boundsSource != null && boundsSource.Length > 0
            ? boundsSource
            : GetComponentsInChildren<Renderer>(true);

        if (source.Length == 0) return new Bounds(transform.position, Vector3.one * 0.5f);

        Bounds bounds = source[0].bounds;
        for (int i = 1; i < source.Length; i++) bounds.Encapsulate(source[i].bounds);
        return bounds;
    }
}
