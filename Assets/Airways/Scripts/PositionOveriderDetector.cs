using UnityEngine;
using CesiumForUnity;

public class PositionOverrideDetector : MonoBehaviour
{
    [Header("Target")]
    public CesiumGlobeAnchor targetGlobeAnchor;

    [Header("Monitoring")]
    public bool enableMonitoring = true;
    public float checkInterval = 0.1f; // Check 10 times per second

    private Unity.Mathematics.double3 lastKnownPosition;
    private bool hasBeenSet = false;

    void Start()
    {
        if (targetGlobeAnchor == null)
        {
            targetGlobeAnchor = GetComponent<CesiumGlobeAnchor>();
        }

        if (targetGlobeAnchor != null && enableMonitoring)
        {
            InvokeRepeating(nameof(CheckForPositionChanges), 0.5f, checkInterval);
            lastKnownPosition = targetGlobeAnchor.longitudeLatitudeHeight;
        }
    }

    void CheckForPositionChanges()
    {
        if (targetGlobeAnchor == null) return;

        var currentPos = targetGlobeAnchor.longitudeLatitudeHeight;

        // Detect unexpected resets to (0,0,0)
        if (hasBeenSet && currentPos.x == 0 && currentPos.y == 0 && currentPos.z == 0)
        {
            Debug.LogError($"POSITION OVERRIDE DETECTED! {gameObject.name} position was reset to (0,0,0)!");
            Debug.LogError($"Previous position was: {lastKnownPosition}");

            // Print stack trace to see what called this
            Debug.LogError($"Stack trace: {System.Environment.StackTrace}");

            // List all components that might be interfering
            LogPotentialConflicts();
        }

        // Detect any significant position changes
        if (hasBeenSet)
        {
            double deltaX = System.Math.Abs(currentPos.x - lastKnownPosition.x);
            double deltaY = System.Math.Abs(currentPos.y - lastKnownPosition.y);
            double deltaZ = System.Math.Abs(currentPos.z - lastKnownPosition.z);

            if (deltaX > 0.001 || deltaY > 0.001 || deltaZ > 1.0)
            {
                Debug.LogWarning($"Position changed for {gameObject.name}: from {lastKnownPosition} to {currentPos}");
            }
        }

        lastKnownPosition = currentPos;

        // Mark as set if position is not (0,0,0)
        if (currentPos.x != 0 || currentPos.y != 0 || currentPos.z != 0)
        {
            hasBeenSet = true;
        }
    }

    void LogPotentialConflicts()
    {
        Debug.Log("=== CHECKING FOR CONFLICTING COMPONENTS ===");

        // Check for components that might interfere
        var originShift = GetComponent<CesiumOriginShift>();
        if (originShift != null)
        {
            Debug.LogWarning($"Found CesiumOriginShift component - this might be causing position resets!");
        }

        var transform = GetComponent<Transform>();
        Debug.Log($"Transform position: {transform.position}");
        Debug.Log($"Transform parent: {(transform.parent != null ? transform.parent.name : "null")}");

        // Check all components on this GameObject
        var components = GetComponents<Component>();
        Debug.Log($"All components on {gameObject.name}:");
        foreach (var component in components)
        {
            Debug.Log($"  - {component.GetType().Name}");
        }

        Debug.Log("=== END CONFLICT CHECK ===");
    }

    // Manual test method
    [ContextMenu("Force Set Test Position")]
    public void ForceSetTestPosition()
    {
        if (targetGlobeAnchor != null)
        {
            Debug.Log("MANUAL TEST: Setting position to Christchurch...");

            var testPos = new Unity.Mathematics.double3(172.547780, -43.475309, 5000);
            Debug.Log($"Setting position to: {testPos}");

            targetGlobeAnchor.longitudeLatitudeHeight = testPos;

            // Check immediately
            var resultPos = targetGlobeAnchor.longitudeLatitudeHeight;
            Debug.Log($"Result position: {resultPos}");

            if (resultPos.x == 0 && resultPos.y == 0 && resultPos.z == 0)
            {
                Debug.LogError("POSITION WAS IMMEDIATELY RESET TO (0,0,0)!");
                LogPotentialConflicts();
            }
        }
    }

    void OnDestroy()
    {
        CancelInvoke();
    }
}