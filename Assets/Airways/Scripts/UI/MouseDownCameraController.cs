using UnityEngine;
using CesiumForUnity;

public class MouseDownCameraController : MonoBehaviour
{
    [Header("Mouse Control Settings")]
    public bool requireMouseDown = true;
    public bool leftMousePressed = false;
    public bool leftMouseButton = true;
    public bool middleMouseButton = true;
    public bool rightMouseButton = false;

    public bool test = false;

    private CesiumCameraController cesiumController;
    private bool originalRotationEnabled;

    void Start()
    {
        cesiumController = GetComponent<CesiumCameraController>();
        if (cesiumController != null)
        {
            originalRotationEnabled = cesiumController.enableRotation;

            // Disable rotation by default if requireMouseDown is enabled
            if (requireMouseDown)
            {
                cesiumController.enableRotation = false;
            }
        }
    }

    void Update()
    {
        if (cesiumController == null || !requireMouseDown) return;
        //cesiumController.enableRotation = false;

        // Check if any specified mouse button is held down
        bool mouseDown = false;

        leftMousePressed = Input.GetMouseButtonDown(0);

        if (leftMouseButton && Input.GetMouseButton(0))
            mouseDown = true;

        if (middleMouseButton && Input.GetMouseButton(2))
            mouseDown = true;

        if (rightMouseButton && Input.GetMouseButton(1))
            mouseDown = true;

        // Enable rotation only when mouse button is held
        cesiumController.enableRotation = mouseDown;

        test = Input.anyKey;

        if (Input.GetKeyDown(KeyCode.N))
        {
            // Set camera to look north
            transform.rotation = Quaternion.Euler(0, 0, 0);
        }
    }
}