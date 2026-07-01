using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine;


public class ActivarLinterna : MonoBehaviour 
{ 
    void Start() 
    { 
        XRBaseInteractable interactable = GetComponent<XRBaseInteractable>(); 
        interactable.activated.AddListener(ActivateFlashlight); 
    } 
 
    void ActivateFlashlight(BaseInteractionEventArgs args) 
    { 
        if (args.interactorObject is XRBaseControllerInteractor interactor) 
        { 
            interactor.SendHapticImpulse(0.5f, 1); 
        }         
    } 
} 