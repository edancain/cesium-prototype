using UnityEngine;
using CesiumForUnity;
using TMPro;

public class Aircraft_Controller : MonoBehaviour
{
    [Header("Aircraft Data")]
    public string icao24;
    public string callsign;
    public float altitude;
    public float groundSpeed; // knots
    public float heading; // degrees

    [Header("Components")]
    public CesiumGlobeAnchor globeAnchor;

    [Header("Flight Path")]
    public bool isFlying = false;

    [Header("Visual")]
    public GameObject aircraftModel;

    // Current position tracking
    private double currentLat;
    private double currentLon;
    private double currentAlt;
    private bool isInitialized = false;

    // Position monitoring variables - removed problematic monitoring system
    private Unity.Mathematics.double3 lastSetPosition;
    private bool positionWasSet = false;

    void Start()
    {
        if (globeAnchor == null)
            globeAnchor = GetComponent<CesiumGlobeAnchor>();

        // Find aircraft model
        FindAircraftModel();

        // Removed problematic position monitoring that was causing issues
        Debug.Log($"Aircraft_Controller started for {callsign}");
    }

    void FindAircraftModel()
    {
        if (aircraftModel == null)
        {
            // Try multiple ways to find the aircraft model
            aircraftModel = transform.Find("An-225")?.gameObject;

            if (aircraftModel == null)
            {
                // Try finding by component
                MeshFilter meshFilter = GetComponentInChildren<MeshFilter>();
                if (meshFilter != null)
                {
                    aircraftModel = meshFilter.gameObject;
                }
            }

            if (aircraftModel == null)
            {
                // Try finding any child that's not Canvas
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child.name != "Canvas")
                    {
                        aircraftModel = child.gameObject;
                        break;
                    }
                }
            }
        }
    }

    // Validation method for AircraftManager
    public bool IsValid()
    {
        return gameObject != null && globeAnchor != null &&
               !gameObject.Equals(null) && !globeAnchor.Equals(null) &&
               gameObject.activeInHierarchy;
    }

    void Update()
    {
        // Removed continuous movement system as it was causing position conflicts
        // Movement is now handled entirely through UpdatePosition calls
    }

    public void UpdatePosition(double longitude, double latitude, double altitude)
    {
        // Set new position as current position
        currentLon = longitude;
        currentLat = latitude;
        currentAlt = altitude;
        this.altitude = (float)altitude;

        Debug.Log($"{callsign}: UpdatePosition called with {longitude:F4}, {latitude:F4}, {altitude}");

        // Update globe anchor immediately
        if (globeAnchor != null)
        {
            var newPos = new Unity.Mathematics.double3(longitude, latitude, altitude);

            Debug.Log($"SETTING position for {callsign}: {newPos}");
            globeAnchor.longitudeLatitudeHeight = newPos;

            // Store for reference
            lastSetPosition = newPos;
            positionWasSet = true;

            // Verify the position was set correctly
            var checkPos = globeAnchor.longitudeLatitudeHeight;
            if (Vector3.Distance(new Vector3((float)checkPos.x, (float)checkPos.y, (float)checkPos.z),
                               new Vector3((float)newPos.x, (float)newPos.y, (float)newPos.z)) > 0.001f)
            {
                Debug.LogWarning($"Position verification failed for {callsign}! Set {newPos}, got {checkPos}");
            }
            else
            {
                Debug.Log($"Position successfully set for {callsign}: {checkPos}");
                Debug.Log($"{callsign}: Transform position after update: {transform.position}");
            }
        }
        else
        {
            Debug.LogError($"CesiumGlobeAnchor is null for {callsign}!");
        }

        isInitialized = true;
    }

    public void UpdateHeading(float newHeading)
    {
        Debug.Log($"UpdateHeading called: {newHeading} for {callsign}");
        heading = newHeading;

        if (globeAnchor != null)
        {
            // Use Cesium's East-Up-North coordinate system
            // In ENU: X=East, Y=Up, Z=North
            // Aviation heading: 0°=North, 90°=East, 180°=South, 270°=West
            // So we need to rotate around the Y (up) axis
            Quaternion headingRotation = Quaternion.Euler(0, newHeading, 0);
            globeAnchor.rotationEastUpNorth = headingRotation;
        }
    }

    public void UpdateData(string newCallsign, float newAltitude, float newGroundSpeed)
    {
        // Only update if we have valid data
        if (string.IsNullOrEmpty(newCallsign))
        {
            Debug.LogWarning($"Received empty callsign for aircraft {icao24}");
            return;
        }

        callsign = newCallsign;
        altitude = newAltitude;
        groundSpeed = newGroundSpeed;

        // Start flying if we have speed
        isFlying = (groundSpeed > 0);
    }

    public void SetSpeed(float speed)
    {
        groundSpeed = speed;
        isFlying = (speed > 0);
    }

    public void SetAltitude(float alt)
    {
        altitude = alt;
        currentAlt = alt;
    }

    public Unity.Mathematics.double3 GetCurrentPosition()
    {
        return new Unity.Mathematics.double3(currentLon, currentLat, currentAlt);
    }

    public Vector3 GetWorldPosition()
    {
        if (globeAnchor != null)
        {
            return globeAnchor.transform.position;
        }
        return transform.position;
    }

    // Method to check if aircraft has valid positioning data
    public bool HasValidPosition()
    {
        return isInitialized && globeAnchor != null &&
               (currentLat != 0 || currentLon != 0 || currentAlt != 0);
    }

    void OnValidate()
    {
        // Removed label update - handled by AircraftLabelManager
    }

    void OnDestroy()
    {
        CancelInvoke();
        Debug.Log($"Aircraft_Controller destroyed for {callsign}");
    }
}