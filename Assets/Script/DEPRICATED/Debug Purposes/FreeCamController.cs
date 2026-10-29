using UnityEngine;

public class FreeCamController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 10f;
    public float shiftMultiplier = 2.5f;

    [Header("Rotation Settings")]
    public float lookSensitivity = 2f;
    public float rollSpeed = 50f;

    private float pitch = 0f;
    private float yaw = 0f;
    private float roll = 0f;

    void Start()
    {
        Vector3 angles = transform.eulerAngles;
        pitch = angles.x;
        yaw = angles.y;
        roll = angles.z;
    }

    void Update()
    {
        

        // Pitch & Yaw (Tahan Klik Kanan + Pergerakan Mouse)
        if (Input.GetMouseButton(1))
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            yaw += Input.GetAxis("Mouse X") * lookSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * lookSensitivity;
            pitch = Mathf.Clamp(pitch, -89f, 89f); // Mencegah kamera terbalik
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // Roll (Tombol Z & C)
        if (Input.GetKey(KeyCode.Z)) roll += rollSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.C)) roll -= rollSpeed * Time.deltaTime;

        // Terapkan Rotasi
        transform.rotation = Quaternion.Euler(pitch, yaw, roll);

        // Penyesuaian Kecepatan (Shift untuk Sprint)
        float currentSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
        {
            currentSpeed *= shiftMultiplier;
        }

        // Perhitungan Arah Pergerakan
        Vector3 moveDirection = Vector3.zero;

        // WASD Movement
        if (Input.GetKey(KeyCode.W)) moveDirection += transform.forward;
        if (Input.GetKey(KeyCode.S)) moveDirection -= transform.forward;
        if (Input.GetKey(KeyCode.D)) moveDirection += transform.right;
        if (Input.GetKey(KeyCode.A)) moveDirection -= transform.right;

        // EQ Elevation
        if (Input.GetKey(KeyCode.E)) moveDirection += transform.up;
        if (Input.GetKey(KeyCode.Q)) moveDirection -= transform.up;

        // Terapkan Pergerakan
        if (moveDirection != Vector3.zero)
        {
            transform.position += moveDirection.normalized * currentSpeed * Time.deltaTime;
        }
    }
}