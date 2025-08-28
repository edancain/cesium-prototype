using UnityEngine;

public class OrbitCameraController : MonoBehaviour
{
    [Header("Orbital Controls")]
    public float orbitSpeed = 2f;
    public float zoomSpeed = 10f;
    public float minZoomDistance = 100f;
    public float maxZoomDistance = 10000f;
    public float smoothTime = 0.3f;
    public float panSpeed = 10f;

    [Header("Current State")]
    public bool isOrbiting = false;
    public Transform orbitTarget;
    public float currentOrbitDistance = 1000f;

    // Private orbital state
    private float orbitYaw = 0f;
    private float orbitPitch = 45f;
    private Vector3 velocity;
    private bool isTransitioning = false;
    private Vector3 orbitCenter; // Allow offset from target

    void Start()
    {
        Debug.Log("OrbitCameraController initialized");
    }

    void Update()
    {
        if (isOrbiting && orbitTarget != null)
        {
            HandleOrbitControls();
        }
    }

    void HandleOrbitControls()
    {
        // Update orbit center (can be offset from target)
        Vector3 targetPos = orbitTarget.position;

        // Mouse orbital controls
        if (Input.GetMouseButton(0)) // Left mouse button for orbit
        {
            float mouseX = Input.GetAxis("Mouse X") * orbitSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * orbitSpeed;

            orbitYaw += mouseX;
            orbitPitch -= mouseY;
            
            // Clamp pitch to prevent flipping
            orbitPitch = Mathf.Clamp(orbitPitch, -80f, 80f);
        }

        // Zoom controls
        float scroll = Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
        currentOrbitDistance -= scroll;
        currentOrbitDistance = Mathf.Clamp(currentOrbitDistance, minZoomDistance, maxZoomDistance);

        // WASD for panning the orbit center
        Vector3 panOffset = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) panOffset += transform.forward * panSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.S)) panOffset -= transform.forward * panSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.A)) panOffset -= transform.right * panSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.D)) panOffset += transform.right * panSpeed * Time.deltaTime;

        // Apply panning to orbit center
        orbitCenter += panOffset;

        // Calculate orbital position around the orbit center
        Vector3 orbitPosition = CalculateOrbitPosition(targetPos + orbitCenter, orbitYaw, orbitPitch, currentOrbitDistance);

        // Smooth camera movement
        if (isTransitioning)
        {
            transform.position = Vector3.SmoothDamp(transform.position, orbitPosition, ref velocity, smoothTime);
            if (Vector3.Distance(transform.position, orbitPosition) < 1f)
            {
                isTransitioning = false;
            }
        }
        else
        {
            transform.position = orbitPosition;
        }

        // Always look at target (not orbit center)
        transform.LookAt(targetPos);

        // ESC to release orbit mode
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ClearOrbitTarget();
        }
    }

    Vector3 CalculateOrbitPosition(Vector3 center, float yawAngle, float pitchAngle, float distance)
    {
        // Convert angles to radians
        float yawRad = yawAngle * Mathf.Deg2Rad;
        float pitchRad = pitchAngle * Mathf.Deg2Rad;

        // Calculate orbital position using spherical coordinates
        float x = center.x + distance * Mathf.Cos(pitchRad) * Mathf.Sin(yawRad);
        float y = center.y + distance * Mathf.Sin(pitchRad);
        float z = center.z + distance * Mathf.Cos(pitchRad) * Mathf.Cos(yawRad);

        return new Vector3(x, y, z);
    }

    // Public methods for UI to control orbital mode
    public void SetOrbitTarget(Transform target, float initialDistance = 1000f)
    {
        orbitTarget = target;
        currentOrbitDistance = initialDistance;
        isOrbiting = true;
        isTransitioning = true;
        orbitCenter = Vector3.zero; // Reset orbit center offset

        if (target != null)
        {
            // Initialize orbital angles based on current camera direction to target
            Vector3 directionToTarget = (target.position - transform.position).normalized;
            orbitYaw = Mathf.Atan2(directionToTarget.x, directionToTarget.z) * Mathf.Rad2Deg;
            orbitPitch = Mathf.Asin(directionToTarget.y) * Mathf.Rad2Deg;
            
            Debug.Log($"OrbitCamera set to track {target.name} at distance {initialDistance}m");
        }
    }

    public void ClearOrbitTarget()
    {
        orbitTarget = null;
        isOrbiting = false;
        isTransitioning = false;
        orbitCenter = Vector3.zero;
        
        Debug.Log("OrbitCamera released from target");
    }

    public bool IsOrbitingTarget()
    {
        return isOrbiting && orbitTarget != null;
    }

    public Transform GetOrbitTarget()
    {
        return orbitTarget;
    }

    void OnValidate()
    {
        // Ensure reasonable limits
        minZoomDistance = Mathf.Clamp(minZoomDistance, 10f, 1000f);
        maxZoomDistance = Mathf.Clamp(maxZoomDistance, 500f, 50000f);
    }
}