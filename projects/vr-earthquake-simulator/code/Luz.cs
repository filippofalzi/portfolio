using UnityEngine;
using UnityEngine.UI;
public class UIBehaviour : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private Light light;
    void Start()
    {
        slider.onValueChanged.AddListener((v) => light.intensity = v);
    }
} 

