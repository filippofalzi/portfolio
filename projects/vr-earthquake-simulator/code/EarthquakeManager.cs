using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.UI;
public class EarthquakeManager : MonoBehaviour
{
    [Header("Input & Vibration")]
    public InputActionProperty velocityAction;
    public Transform interactor;

    [Header("Target Objects")]
    public Transform roomContainer;
    public Light ceilingLight;

    [Header("Physics (Spring-Damper Model)")]
    public float stiffness = 100f;
    public float damping = 10f;
    public float multiplier = 0.5f;

    // Variables for Kinematics
    private Vector3 displacement;
    private Vector3 velocity;
    private Vector3 originalPos;
    private float originalLightIntensity;
    private float intensity;
    private Vector3 lastPosition;


    [Header("Comfort Settings")]
    public MeshRenderer vignetteRenderer;
    public float intensityThreshold = 0.5f; 
    public float sensitivity = 100f;
    
    // Set the default vignette value
    private float smoothedAperture = 1.0f; // Default

    [Header("Audio effect ")]
    public AudioSource earthquakeAudio;

    [Header(" Interaction messages")]
    public GameObject instructionText;

    // Variables for Richter text feedback
    public Text richterText;
    private float textRichter;

    void Start()
    {
        if (velocityAction.action != null) velocityAction.action.Enable();
        if (roomContainer != null) originalPos = roomContainer.localPosition;
        if (ceilingLight != null) originalLightIntensity = ceilingLight.intensity;
        if (interactor != null) lastPosition = interactor.transform.position;
     }

    void Update()
    {
        // 1. Get input from device
        Vector3 handVel = velocityAction.action.ReadValue<Vector3>();

        // Check origin:
        // 1) From XR Simulator
        if (handVel.sqrMagnitude < 0.0001f && interactor != null)
        {
            Vector3 currentPosition = interactor.transform.position;
            handVel = (currentPosition - lastPosition) / Time.deltaTime;
            lastPosition = currentPosition;
        }

        // 2) From XR Controller 
        else if (interactor != null)
        {
            lastPosition = interactor.transform.position;
        }

        Debug.Log("Intensity: " + intensity.ToString("F6"));

        // Module of Velocity (speed)
        intensity = handVel.magnitude;

        // 2. Solving spring-Damper Physics with Eulero (Vectorial Form of Second Order Differential Eq.)
        Vector3 externalForce = handVel * multiplier;
        Vector3 acceleration = externalForce - (damping * velocity) - (stiffness * displacement);

        velocity += acceleration * Time.deltaTime;
        displacement += velocity * Time.deltaTime;

        // 3. Apply movement to the room
        roomContainer.localPosition = originalPos + displacement;

        // 4. Adjust comfort Logic --> Vignette control
        if (vignetteRenderer == null) return;

        float Aperture = 1.0f; // Default

        if (intensity > 0.2f)
        {
            float vignette = Mathf.Clamp01((intensity) * sensitivity);
            Aperture = Mathf.Lerp(1.0f, 0.25f, vignette);
        }

        smoothedAperture = Mathf.Lerp(smoothedAperture, Aperture, Time.deltaTime * 2f);
        vignetteRenderer.material.SetFloat("_Aperture", smoothedAperture);

        // 5. Feedback light --> creating light flickering that varies with earthquake intensity
        if (ceilingLight != null)
        {
            if (intensity > intensityThreshold)
            {
                float power = intensity * 10f;
                float flicker = Random.Range(-power, power);
                ceilingLight.intensity = Mathf.Clamp(originalLightIntensity + flicker, 0f, originalLightIntensity * 2.5f);
            }
            else
            {
                ceilingLight.intensity = Mathf.Lerp(ceilingLight.intensity, originalLightIntensity, Time.deltaTime * 5f);
            }
        }

        // 6. Adjust Audio for earthquake --> moduling volume using eartquake intensity
        if (earthquakeAudio != null)
        {
            if (intensity > intensityThreshold)
            {
                if (!earthquakeAudio.isPlaying) earthquakeAudio.Play();

                earthquakeAudio.volume = Mathf.Clamp01(intensity * 0.5f);
            }
            else
            {
                earthquakeAudio.volume = Mathf.Lerp(earthquakeAudio.volume, 0, Time.deltaTime * 2f);
                if (earthquakeAudio.volume < 0.01f) earthquakeAudio.Stop();
            }
        }

        // 7. Richter value for UI
        if (richterText != null)
        {
            float magnitudo = Mathf.Pow(intensity, 0.3f) * 9.0f;
            Debug.Log("MAgnitudo: " + magnitudo.ToString("F6"));
            float magnitudoRichter = Mathf.Clamp(magnitudo, 0f, 9.0f);
            textRichter = Mathf.Lerp(textRichter, magnitudoRichter, Time.deltaTime * 8f);

            // Change color for high values of the scale
            if (textRichter >= 7.0f)
            {
                richterText.color = Color.red; 
            }
            else
            {
                richterText.color = Color.white; 
            }
        
        richterText.text = "Richter scale: " + textRichter.ToString("F1");
        }

        // 8. Haptic feedback (Debug not possible)
        if (intensity > intensityThreshold)
        {
            float hapticForce = Mathf.Clamp01(intensity * 0.8f);
            SendVibration(hapticForce);
        }
    }

    // Function for haptic vibrational feedback
    void SendVibration(float amplitude)
    {
        if (interactor != null)
        {
            XRBaseController controller = interactor.GetComponentInParent<XRBaseController>();

            if (controller != null)
            {
                controller.SendHapticImpulse(Mathf.Clamp01(amplitude), 0.1f);
            }
        }
    }

    // Function to hide text
    public void HideInstructions()
    {
        if (instructionText != null)
        {
            instructionText.SetActive(false);
        }
    }
}