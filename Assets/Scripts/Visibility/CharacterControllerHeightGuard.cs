using UnityEngine;

/// <summary>
/// XRI's CharacterControllerBodyManipulator recomputes CharacterController.height/center every
/// frame directly from the live camera-relative tracked height, with no validation
/// (Runtime/Locomotion/CharacterControllerBodyManipulator.cs: "characterController.height =
/// xrOrigin.CameraInOriginSpaceHeight - bodyGroundPosition.y"). If that live camera data is ever
/// bad for a frame -- input device connecting mid-session, a timing glitch during startup,
/// anything -- height can go zero/negative, which produces an invalid capsule collider and the
/// rig falls through the floor. This has been observed happening a few seconds into Play, after
/// looking fine initially, which points at a live recompute glitch rather than bad spawn data.
///
/// Runs after XRI's own locomotion update (very late execution order + LateUpdate) and clamps
/// height/center back to something valid, preserving the capsule's ground contact point so the
/// clamp doesn't itself introduce a visible pop.
/// </summary>
[DefaultExecutionOrder(32000)]
[RequireComponent(typeof(CharacterController))]
public class CharacterControllerHeightGuard : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Never let the controller's height go below this, regardless of what live tracking data computes.")]
    private float minHeight = 0.5f;

    [SerializeField]
    [Tooltip("Never let the controller's height exceed this, in case tracking data spikes high instead of low.")]
    private float maxHeight = 2.5f;

    private CharacterController _cc;

    private void Awake()
    {
        _cc = GetComponent<CharacterController>();
    }

    private void LateUpdate()
    {
        float height = _cc.height;
        if (height >= minHeight && height <= maxHeight) return;

        // Preserve where the capsule's bottom currently is (its ground contact point) so clamping
        // height doesn't teleport the visible/collidable body up or down.
        Vector3 center = _cc.center;
        float bottom = center.y - height * 0.5f;
        float clamped = Mathf.Clamp(height, minHeight, maxHeight);

        _cc.height = clamped;
        center.y = bottom + clamped * 0.5f;
        _cc.center = center;

        Debug.LogWarning($"[CharacterControllerHeightGuard] {name}: CharacterController.height was {height:F3} " +
                          $"(invalid/out of range) -- clamped to {clamped:F3}. This means the live camera-relative " +
                          "height feeding XRI's body manipulator went bad; check headset tracking data if this recurs.", this);
    }
}
