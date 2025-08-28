using UnityEngine;
using System.Linq;

public class CameraController : MonoBehaviour
{
    [Header("Initial Positioning")]
    public float initialHeight = 1000f;
    public bool autoPositionOnStart = true;

    [Header("Manual Controls")]
    public KeyCode moveUpKey = KeyCode.Q;
    public KeyCode moveDownKey = KeyCode.E;
    public KeyCode moveForwardKey = KeyCode.W;
    public KeyCode moveBackKey = KeyCode.S;
    public KeyCode moveLeftKey = KeyCode.A;
    public KeyCode moveRightKey = KeyCode.D;

    [Header("Movement Settings")]
    public float moveSpeed = 100f;
    public float fastMoveSpeed = 500f;
    public float mouseSensitivity = 2f;

    private AircraftManager aircraftManager;
    private bool isInitialized = false;

    void Start()
    {
        aircraftManager = FindObjectOfType<AircraftManager>();

        if (autoPositionOnStart)
        {
            // Try to position over first aircraft, but wait a moment for aircraft to be created
            Invoke(nameof(AutoPositionCamera), 2f);
        }
    }

    void AutoPositionCamera()
    {
        if (aircraftManager == null)
        {
            Debug.LogWarning("CameraController: No AircraftManager found, using default position");
            SetDefaultPosition();
            return;
        }

        var aircraft = aircraftManager.GetActiveAircraft();
        if (aircraft.Count == 0)
        {
            Debug.LogWarning("CameraController: No aircraft found, using default position");
            SetDefaultPosition();
            return;
        }

        // Position camera over the first aircraft (likely ANZ123 in Christchurch)
        var firstAircraft = aircraft.Values.First();
        if (firstAircraft != null && firstAircraft.globeAnchor != null)
        {
            var pos = firstAircraft.globeAnchor.longitudeLatitudeHeight;

            // Position camera above the aircraft
            Vector3 cameraPos = new Vector3((float)pos.x, initialHeight, (float)pos.y);
            transform.position = cameraPos;

            // Look down at the aircraft location
            Vector3 lookAtPos = new Vector3((float)pos.x, 0f, (float)pos.y);
            transform.LookAt(lookAtPos);

            Debug.Log($"CameraController: Positioned camera over {firstAircraft.callsign} at {pos.x:F4}, {pos.y:F4}");
            isInitialized = true;
        }
        else
        {
            Debug.LogWarning("CameraController: First aircraft has no globe anchor, using default position");
            SetDefaultPosition();
        }
    }

    void SetDefaultPosition()
    {
        // Default to Christchurch coordinates
        transform.position = new Vector3(172.547f, initialHeight, -43.475f);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f); // Look straight down
        Debug.Log("CameraController: Set to default position over Christchurch");
        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized) return;

        HandleKeyboardMovement();
        HandleMouseLook();
    }

    void HandleKeyboardMovement()
    {
        float speed = Input.GetKey(KeyCode.LeftShift) ? fastMoveSpeed : moveSpeed;
        float deltaTime = Time.deltaTime;

        Vector3 moveDirection = Vector3.zero;

        // Vertical movement (world space)
        if (Input.GetKey(moveUpKey))
            moveDirection.y += 1f;
        if (Input.GetKey(moveDownKey))
            moveDirection.y -= 1f;

        // Horizontal movement (relative to camera facing)
        if (Input.GetKey(moveForwardKey))
            moveDirection += transform.forward;
        if (Input.GetKey(moveBackKey))
            moveDirection -= transform.forward;
        if (Input.GetKey(moveLeftKey))
            moveDirection -= transform.right;
        if (Input.GetKey(moveRightKey))
            moveDirection += transform.right;

        // Apply movement
        if (moveDirection != Vector3.zero)
        {
            transform.position += moveDirection.normalized * speed * deltaTime;
        }

        // Quick reset to view all aircraft
        if (Input.GetKeyDown(KeyCode.R))
        {
            AutoPositionCamera();
        }
    }

    void HandleMouseLook()
    {
        if (Input.GetMouseButton(1)) // Right mouse button to look around
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(-mouseY, mouseX, 0f);
        }
    }

    // Public method to fly to specific coordinates
    public void FlyToPosition(double longitude, double latitude, float height = 0f)
    {
        if (height == 0f) height = initialHeight;

        Vector3 targetPos = new Vector3((float)longitude, height, (float)latitude);
        transform.position = targetPos;

        // Look down at the target location
        Vector3 lookAtPos = new Vector3((float)longitude, 0f, (float)latitude);
        transform.LookAt(lookAtPos);

        Debug.Log($"CameraController: Flew to position {longitude:F4}, {latitude:F4} at height {height}");
    }

    void OnGUI()
    {
        if (!isInitialized) return;

        // Show controls in bottom-right corner
        GUI.Box(new Rect(Screen.width - 250, Screen.height - 120, 240, 110), "Camera Controls");
        GUI.Label(new Rect(Screen.width - 240, Screen.height - 100, 230, 20), "WASD: Move | Q/E: Up/Down");
        GUI.Label(new Rect(Screen.width - 240, Screen.height - 80, 230, 20), "Hold Shift: Fast move");
        GUI.Label(new Rect(Screen.width - 240, Screen.height - 60, 230, 20), "Right mouse: Look around");
        GUI.Label(new Rect(Screen.width - 240, Screen.height - 40, 230, 20), "R: Reset to aircraft view");

        // Show current position
        Vector3 pos = transform.position;
        GUI.Label(new Rect(Screen.width - 240, Screen.height - 20, 230, 20),
                 $"Pos: {pos.x:F1}, {pos.y:F0}, {pos.z:F1}");
    }
}