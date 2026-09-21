using UnityEngine;

/// Diagnostic only -- not part of the visibility system. Attach directly to the camera you
/// believe is the live tracked XR camera and press Play. Confirms two things fast:
///   1) Is THIS object's transform actually changing every frame (i.e. is tracking reaching it)?
///   2) Does Camera.main resolve to THIS object, or to some other camera?
/// If (1) moves but something else (like EyeVisibilityCones) still looks frozen, the bug is a
/// stale/wrong camera reference elsewhere, not tracking itself.
public class CameraTransformProbe : MonoBehaviour
{
    private Vector3 lastPosition;
    private Quaternion lastRotation;
    private float logTimer;

    private void Start()
    {
        bool isMain = Camera.main != null && Camera.main.gameObject == gameObject;
        Debug.Log($"[CameraProbe] Attached to '{name}' (id {GetEntityId()}). " +
                  $"Camera.main = '{(Camera.main ? Camera.main.name : "null")}' " +
                  $"(id {(Camera.main ? Camera.main.GetEntityId().ToString() : "n/a")}). " +
                  $"Camera.main IS this object: {isMain}");

        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }

    private void LateUpdate()
    {
        bool moved = transform.position != lastPosition || transform.rotation != lastRotation;

        logTimer += Time.deltaTime;
        if (logTimer >= 0.5f)
        {
            logTimer = 0f;
            string mainInfo = Camera.main
                ? $"{Camera.main.name} pos={Camera.main.transform.position:F3}"
                : "null";
            Debug.Log($"[CameraProbe] '{name}' pos={transform.position:F3} " +
                      $"rot={transform.rotation.eulerAngles:F1} movedThisFrame={moved} | Camera.main={mainInfo}");
        }

        lastPosition = transform.position;
        lastRotation = transform.rotation;
    }

    // Visual proof independent of console/logcat. Green = this probe's live transform.
    // Red = Camera.main's transform, only drawn if it's a DIFFERENT object than this one.
    // If red sits still while green moves with your head, Camera.main is grabbing the wrong camera.
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position, 0.05f);
        Gizmos.DrawRay(transform.position, transform.forward * 0.3f);

        if (Camera.main != null && Camera.main.gameObject != gameObject)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(Camera.main.transform.position, 0.05f);
            Gizmos.DrawRay(Camera.main.transform.position, Camera.main.transform.forward * 0.3f);
        }
    }
}
