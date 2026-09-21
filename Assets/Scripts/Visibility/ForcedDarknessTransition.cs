using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Fallback for when a <see cref="VisibilityGatedRelocator"/> (or, in principle, a
/// <see cref="VisibilityGatedVersionSwitcher"/>) has been waiting too long for the player to look
/// away -- wire this to that component's "waited too long" event. Flickers the scene's lights,
/// cuts them out entirely, forces the pending change while it's dark, then brings the lights back
/// up, so the player never sees the actual pop -- they just see the lights go out for a moment.
///
/// Toggling Light components alone is NOT enough to guarantee darkness: it does nothing to
/// objects lit by baked lightmaps (their lighting is pre-baked into the lightmap texture, not
/// recomputed from live Light intensity), and it does nothing to emissive materials (e.g. the
/// TV's screen), which self-illuminate regardless of scene lights. So the actual "is it dark"
/// guarantee comes from <see cref="blackoutImage"/>, a full-screen UI Image on a Screen Space -
/// Overlay canvas that this fades to opaque black -- that covers the whole view no matter what's
/// lighting or lighting itself underneath it. The Light flicker is kept as an atmospheric layer
/// on top of that guarantee, not a substitute for it.
///
/// Only wired up for the NPC to start with (see the comment on the Entity's Relocator component)
/// -- nothing about this is NPC-specific, so it can be reused for other objects later if needed.
/// </summary>
public class ForcedDarknessTransition : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Lights to flicker/kill. Leave empty to auto-use every Light in the scene at Awake.")]
    private Light[] lights;

    [SerializeField]
    [Tooltip("Full-screen black Image (Screen Space - Overlay canvas) whose alpha this animates. " +
             "This is what actually guarantees darkness -- Light toggling alone can't, since it " +
             "doesn't affect baked lightmaps or emissive materials.")]
    private Image blackoutImage;

    [SerializeField]
    private VisibilityGatedRelocator relocator;

    [SerializeField]
    private VisibilityGatedVersionSwitcher versionSwitcher;

    [SerializeField]
    private float flickerDuration = 2.2f;

    [SerializeField]
    [Tooltip("Random on/off interval range (seconds) during the flicker phase.")]
    private Vector2 flickerIntervalRange = new Vector2(0.04f, 0.15f);

    [SerializeField]
    [Tooltip("How long everything stays fully dark before/after the forced change (each half).")]
    private float blackoutHoldSeconds = 0.5f;

    private bool _running;
    private float[] _originalIntensities;
    private bool[] _originalEnabled;

    private void Awake()
    {
        if (lights == null || lights.Length == 0)
            lights = FindObjectsByType<Light>(FindObjectsSortMode.None);

        if (blackoutImage != null)
        {
            var c = blackoutImage.color;
            c.a = 0f;
            blackoutImage.color = c;
        }
    }

    /// Wire this to VisibilityGatedRelocator.onWaitTooLong (or VisibilityGatedVersionSwitcher, if
    /// it grows an equivalent event later).
    public void TriggerFlickerAndForce()
    {
        if (_running) return;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        _running = true;
        CacheOriginalState();

        float t = 0f;
        while (t < flickerDuration)
        {
            float interval = Random.Range(flickerIntervalRange.x, flickerIntervalRange.y);
            bool flickerOn = Random.value > 0.35f; // biased toward "off" flashes as it worsens
            float fraction = flickerOn ? Random.Range(0.15f, 0.6f) : 0f;
            SetAllLights(fraction);
            SetOverlayAlpha(1f - fraction); // overlay darkens in lockstep so the flicker actually reads as a flicker regardless of lighting mode
            yield return new WaitForSeconds(interval);
            t += interval;
        }

        // Full blackout -- this is the window where the actual teleport/swap happens, hidden.
        // The overlay at alpha=1 is what actually guarantees nothing is visible here, independent
        // of baked lighting or emissive materials.
        SetAllLights(0f);
        SetOverlayAlpha(1f);
        yield return new WaitForSeconds(blackoutHoldSeconds);

        if (relocator != null) relocator.ForceApplyPending();
        if (versionSwitcher != null) versionSwitcher.RequestSwitchTo(versionSwitcher.CurrentVersion); // no-op hook point if this grows a force-apply later

        yield return new WaitForSeconds(blackoutHoldSeconds);

        RestoreOriginalState();
        SetOverlayAlpha(0f);
        _running = false;
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (blackoutImage == null) return;
        var c = blackoutImage.color;
        c.a = Mathf.Clamp01(alpha);
        blackoutImage.color = c;
    }

    private void CacheOriginalState()
    {
        _originalIntensities = new float[lights.Length];
        _originalEnabled = new bool[lights.Length];
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null) continue;
            _originalIntensities[i] = lights[i].intensity;
            _originalEnabled[i] = lights[i].enabled;
        }
    }

    private void SetAllLights(float fractionOfOriginal)
    {
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null) continue;
            lights[i].enabled = fractionOfOriginal > 0f;
            lights[i].intensity = _originalIntensities[i] * fractionOfOriginal;
        }
    }

    private void RestoreOriginalState()
    {
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null) continue;
            lights[i].enabled = _originalEnabled[i];
            lights[i].intensity = _originalIntensities[i];
        }
    }
}
