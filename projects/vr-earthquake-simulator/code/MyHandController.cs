using UnityEngine;
    using UnityEngine.InputSystem;

public class MyHandController : MonoBehaviour
{
    [SerializeField] InputActionReference actionGrip;
    [SerializeField] InputActionReference actionTrigger;

    private Animator handAnimator;

    void Start()
    {
        handAnimator = GetComponent<Animator>();
        actionGrip.action.Enable();
        actionTrigger.action.Enable();
    }
    void Update()
    {
        // Legge continuamente il valore del grip e del trigger
        float gripValue = actionGrip.action.ReadValue<float>();
        float triggerValue = actionTrigger.action.ReadValue<float>();

        // Aggiorna l'animatore
        handAnimator.SetFloat("Grip", gripValue);
        handAnimator.SetFloat("Trigger", triggerValue);
    }

    private void GripPress(InputAction.CallbackContext obj)
    {
        handAnimator.SetFloat("Grip", obj.ReadValue<float>());
    }

    private void TriggerPress(InputAction.CallbackContext obj)
    {
        handAnimator.SetFloat("Trigger", obj.ReadValue<float>());
    }
}
