using UnityEngine;

public class SimpleCameraController : MonoBehaviour
{
    public float speed = 10f;
    public float mouseSensitivity = 2f;

    float rotationX = 0f;
    float rotationY = 0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Keyboard movement
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 move = transform.forward * v + transform.right * h;
        transform.position += move * speed * Time.deltaTime;

        // Vertical movement
        if (Input.GetKey(KeyCode.E)) transform.position += Vector3.up * speed * Time.deltaTime;
        if (Input.GetKey(KeyCode.Q)) transform.position += Vector3.down * speed * Time.deltaTime;

        // Mouse rotation
        rotationX += Input.GetAxis("Mouse X") * mouseSensitivity * 100f * Time.deltaTime;
        rotationY -= Input.GetAxis("Mouse Y") * mouseSensitivity * 100f * Time.deltaTime;

        rotationY = Mathf.Clamp(rotationY, -90f, 90f);

        transform.rotation = Quaternion.Euler(rotationY, rotationX, 0f);
    }
}