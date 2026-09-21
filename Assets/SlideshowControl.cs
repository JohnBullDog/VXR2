// Jack Roberson
// jtr554
// Slideshow script for tv in office
// September 16th, 2026

using UnityEngine;
using UnityEngine.UI; // to use UI elements for the image slideshow

public class SlideshowControl : MonoBehaviour
{
    // make variables for the slideshow, i.e. images, sprite, how long the image shows, which image is being shown, and probably a timer to keep track

    // public variables
    public Image slideImg; // optional: only renders if this object is under a Canvas
    public Sprite[] slides; // array of sprites for the slideshow
    public float slideDuration = 3f; // how long each image shows

    [Tooltip("The mesh renderer whose material actually shows the slides (the TV's physical screen surface). Defaults to this object's own MeshRenderer.")]
    public MeshRenderer screenRenderer;
    [Tooltip("Which material slot on screenRenderer is the screen. -1 auto-detects the first slot whose material name contains \"Screen\".")]
    public int screenMaterialIndex = -1;
    [Tooltip("Also light up the screen material's emission with the slide image, so it reads as a glowing screen instead of a flat lit surface.")]
    public bool driveEmission = true;
    public float emissionIntensity = 1.2f;

    // private variables
    private int currSlide = 0; // which image is being shown
    private float timer; // timer to keep track of time, need to initialize it to the start or update functions so that the first image shows for the full duration --> Update function
    private Material _screenMaterialInstance;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    // The start function initializes the slideshow by setting the first image
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (screenRenderer == null) screenRenderer = GetComponent<MeshRenderer>();
        if (screenRenderer != null && screenMaterialIndex < 0)
        {
            var mats = screenRenderer.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] != null && mats[i].name.ToLowerInvariant().Contains("screen")) { screenMaterialIndex = i; break; }
            }
        }
        if (screenRenderer != null && screenMaterialIndex >= 0 && screenMaterialIndex < screenRenderer.sharedMaterials.Length)
        {
            var instances = screenRenderer.materials; // instantiates per-renderer copies, safe to mutate
            _screenMaterialInstance = instances[screenMaterialIndex];
        }

        // check if the slides array is not empty, if not , set the first image to show
        if (slides.Length > 0) { ApplySlide(slides[0]); }
    }

    // Update function to handle the slideshow timing and image switching, checking that there are slides, and showing them
    // Update is called once per frame
    void Update()
    {
        if (slides.Length == 0) { return; } // if there are no slides, return

        // init timer to delta time
        timer += Time.deltaTime;

        // see if the timer has reached the slide duration, if so, switch to the next image and reset the timer
        if( timer >= slideDuration)
        {
            timer = 0f; // reset the timer
            currSlide = (currSlide + 1) % slides.Length; // switch to the next image, wrap around if at the end
            ApplySlide(slides[currSlide]); // set the new image to show
        }
    }

    private void ApplySlide(Sprite slide)
    {
        if (slide == null) return;

        if (slideImg != null) slideImg.sprite = slide; // only visible if this ends up under a Canvas

        if (_screenMaterialInstance != null)
        {
            var tex = slide.texture;
            if (_screenMaterialInstance.HasProperty(BaseMapId)) _screenMaterialInstance.SetTexture(BaseMapId, tex);
            if (_screenMaterialInstance.HasProperty(MainTexId)) _screenMaterialInstance.SetTexture(MainTexId, tex);
            if (_screenMaterialInstance.HasProperty(BaseColorId)) _screenMaterialInstance.SetColor(BaseColorId, Color.white);

            if (driveEmission && _screenMaterialInstance.HasProperty(EmissionMapId))
            {
                _screenMaterialInstance.EnableKeyword("_EMISSION");
                _screenMaterialInstance.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                _screenMaterialInstance.SetTexture(EmissionMapId, tex);
                if (_screenMaterialInstance.HasProperty(EmissionColorId))
                    _screenMaterialInstance.SetColor(EmissionColorId, Color.white * emissionIntensity);
            }
        }
    }
}
