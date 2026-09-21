using UnityEngine;

/// <summary>
/// Maintains two invisible cone-shaped visibility volumes, one per eye, used as a "can the
/// player currently see this point/object" test. Nothing here is rendered at runtime -- the
/// cones only exist as math (plus optional Scene-view gizmos for tuning).
///
/// Each eye's cone is built from the active XR camera's live stereo data when available
/// (eye separation from <see cref="Camera.stereoSeparation"/>, which reflects the headset's
/// actual reported IPD; per-eye field of view decoded from
/// <see cref="Camera.GetStereoProjectionMatrix"/>, which reflects the runtime's actual
/// asymmetric per-eye frustum on standalone headsets like Quest 2). When stereo XR data isn't
/// available -- e.g. running in the Editor without a headset attached -- it falls back to the
/// inspector-configured values.
///
/// Because Quest 2 lenses give a rectangular (and slightly asymmetric) per-eye FOV, not a
/// circular one, each cone is built to CIRCUMSCRIBE that rectangle (its half-angle reaches the
/// frustum's farthest corner). That means the cone is always at least as big as what's actually
/// visible, never smaller -- so anything genuinely on-screen is guaranteed to test as "visible",
/// at the cost of also treating a little extra area near the screen's corners as visible too.
/// For this system's purpose (never let a swap happen while something is actually on-screen)
/// erring in that direction is the safe one.
/// </summary>
public class EyeVisibilityCones : MonoBehaviour
{
    public static EyeVisibilityCones Instance { get; private set; }

    [SerializeField]
    [Tooltip("The XR camera to derive eye cones from. Defaults to Camera.main.")]
    private Camera xrCamera;

    [SerializeField]
    [Tooltip("Eye separation (meters) used only when stereo XR data isn't available.")]
    private float fallbackEyeSeparation = 0.063f;

    [SerializeField]
    [Tooltip("Per-eye cone half-angle (degrees) used only when stereo XR data isn't available. " +
             "Approximate placeholder for Quest 2 -- tune against Meta's published per-eye FOV if you need it exact.")]
    private float fallbackHalfAngleDegrees = 55f;

    [SerializeField]
    [Tooltip("How far out each cone extends.")]
    private float maxRange = 50f;

    public struct Cone
    {
        public Vector3 Apex;
        public Vector3 Direction;
        public float HalfAngleRad;
        public float MaxRange;
    }

    public Cone LeftEyeCone { get; private set; }
    public Cone RightEyeCone { get; private set; }

    private void Awake()
    {
        Instance = this;
        if (xrCamera == null) xrCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (xrCamera == null) return;

        float halfSeparation = fallbackEyeSeparation * 0.5f;
        float leftHalfAngle = fallbackHalfAngleDegrees * Mathf.Deg2Rad;
        float rightHalfAngle = fallbackHalfAngleDegrees * Mathf.Deg2Rad;

        if (xrCamera.stereoEnabled)
        {
            halfSeparation = xrCamera.stereoSeparation * 0.5f;
            leftHalfAngle = CircumscribedHalfAngle(xrCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left));
            rightHalfAngle = CircumscribedHalfAngle(xrCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right));
        }

        Transform t = xrCamera.transform;
        Vector3 leftApex = t.position - t.right * halfSeparation;
        Vector3 rightApex = t.position + t.right * halfSeparation;

        LeftEyeCone = new Cone { Apex = leftApex, Direction = t.forward, HalfAngleRad = leftHalfAngle, MaxRange = maxRange };
        RightEyeCone = new Cone { Apex = rightApex, Direction = t.forward, HalfAngleRad = rightHalfAngle, MaxRange = maxRange };
    }

    /// Decodes a Unity off-center stereo projection matrix into the half-angle of the circular
    /// cone that circumscribes that eye's (asymmetric) rectangular frustum.
    private static float CircumscribedHalfAngle(Matrix4x4 proj)
    {
        float tanRight = (1f + proj.m02) / proj.m00;
        float tanLeft = (proj.m02 - 1f) / proj.m00;
        float tanTop = (1f + proj.m12) / proj.m11;
        float tanBottom = (proj.m12 - 1f) / proj.m11;

        float maxTanX = Mathf.Max(Mathf.Abs(tanLeft), Mathf.Abs(tanRight));
        float maxTanY = Mathf.Max(Mathf.Abs(tanBottom), Mathf.Abs(tanTop));
        float cornerTan = Mathf.Sqrt(maxTanX * maxTanX + maxTanY * maxTanY);
        return Mathf.Atan(cornerTan);
    }

    /// True if the sphere is inside either eye's cone -- i.e. the player could plausibly see it.
    public static bool IsSphereVisible(Vector3 center, float radius)
    {
        if (Instance == null) return false; // fail open: never block a swap just because no cone data exists yet.
        return SphereIntersectsCone(Instance.LeftEyeCone, center, radius)
            || SphereIntersectsCone(Instance.RightEyeCone, center, radius);
    }

    public static bool IsBoundsVisible(Bounds bounds)
    {
        return IsSphereVisible(bounds.center, bounds.extents.magnitude);
    }

    private static bool SphereIntersectsCone(Cone cone, Vector3 sphereCenter, float sphereRadius)
    {
        if (cone.Direction == Vector3.zero) return false;

        Vector3 toSphere = sphereCenter - cone.Apex;
        float distAlongAxis = Vector3.Dot(toSphere, cone.Direction);

        if (distAlongAxis < -sphereRadius || distAlongAxis > cone.MaxRange + sphereRadius) return false;

        float distFromAxis = Vector3.Cross(toSphere, cone.Direction).magnitude;
        float angleToCenter = Mathf.Atan2(distFromAxis, Mathf.Max(distAlongAxis, 0.0001f));
        float angularRadius = Mathf.Atan2(sphereRadius, Mathf.Max(toSphere.magnitude, 0.0001f));
        return angleToCenter <= cone.HalfAngleRad + angularRadius;
    }

    // OnDrawGizmos (not OnDrawGizmosSelected) so the cones are visible without having to select
    // this GameObject first -- only populated once the game is actually running (LateUpdate).
    private void OnDrawGizmos()
    {
        DrawGizmoCone(LeftEyeCone, Color.cyan);
        DrawGizmoCone(RightEyeCone, Color.magenta);
    }

    private void DrawGizmoCone(Cone cone, Color color)
    {
        if (cone.Direction == Vector3.zero) return;

        Gizmos.color = color;
        float radiusAtMax = Mathf.Tan(cone.HalfAngleRad) * cone.MaxRange;
        Vector3 farCenter = cone.Apex + cone.Direction * cone.MaxRange;

        Vector3 up = Vector3.Cross(cone.Direction, Vector3.up).normalized;
        if (up == Vector3.zero) up = Vector3.Cross(cone.Direction, Vector3.right).normalized;
        Vector3 right = Vector3.Cross(cone.Direction, up).normalized;

        const int segments = 12;
        Vector3 prevRim = farCenter + right * radiusAtMax;
        for (int i = 1; i <= segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            Vector3 rim = farCenter + (up * Mathf.Sin(a) + right * Mathf.Cos(a)) * radiusAtMax;
            Gizmos.DrawLine(prevRim, rim);
            if (i % 3 == 0) Gizmos.DrawLine(cone.Apex, rim);
            prevRim = rim;
        }
        Gizmos.DrawLine(cone.Apex, farCenter);
    }
}
