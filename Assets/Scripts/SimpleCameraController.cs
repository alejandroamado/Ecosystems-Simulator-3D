using UnityEngine;

public class SimpleCameraController : MonoBehaviour
{
    public GridManager gridManager;

    public float moveSpeed = 15f;
    public float rotationSpeed = 8f;

    private Vector3 minPosition;
    private Vector3 maxPosition;

    private float yaw = 0f;
    private float pitch = 0f;

    void Start()
    {
        minPosition = new Vector3(-30f, 1f, -30f);
        maxPosition = new Vector3(gridManager.width + 30, 60f, gridManager.height + 30);
    }

    void Update()
    {
        // Determinar velocidad actual (Shift para ir más rápido)
        float currentSpeed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            currentSpeed *= 2f;
        }

        // Movimiento con teclado
        float horizontal = Input.GetAxis("Horizontal"); // A/D
        float vertical = Input.GetAxis("Vertical");     // W/S
        float upDown = 0f;

        if (Input.GetKey(KeyCode.E)) upDown = 1f;
        if (Input.GetKey(KeyCode.Q)) upDown = -1f;

        Vector3 movement = new Vector3(horizontal, upDown, vertical);
        transform.Translate(movement * currentSpeed * Time.deltaTime, Space.Self);

        // Limitar posición
        Vector3 clampedPosition = transform.position;
        clampedPosition.x = Mathf.Clamp(clampedPosition.x, minPosition.x, maxPosition.x);
        clampedPosition.y = Mathf.Clamp(clampedPosition.y, minPosition.y, maxPosition.y);
        clampedPosition.z = Mathf.Clamp(clampedPosition.z, minPosition.z, maxPosition.z);
        transform.position = clampedPosition;

        // Rotación con ratón (botón derecho)
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * rotationSpeed;
            pitch -= Input.GetAxis("Mouse Y") * rotationSpeed;
            pitch = Mathf.Clamp(pitch, -89f, 89f);
            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }
    }
}
