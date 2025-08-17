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
    public TextMeshProUGUI labelText;

    // Current position tracking
    private double currentLat;
    private double currentLon;
    private double currentAlt;

    void Start()
    {
        if (globeAnchor == null)
            globeAnchor = GetComponent<CesiumGlobeAnchor>();

        // DEBUG: Let's see what children this GameObject actually has
        Debug.Log($"Aircraft {callsign} has {transform.childCount} children:");
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            Debug.Log($"  Child {i}: '{child.name}' (GameObject: {child.gameObject.name})");
            
            // Also check grandchildren
            for (int j = 0; j < child.childCount; j++)
            {
                Transform grandChild = child.GetChild(j);
                Debug.Log($"    Grandchild {j}: '{grandChild.name}'");
            }
        }

        if (aircraftModel == null)
        {
            // Try multiple ways to find the aircraft model
            aircraftModel = transform.Find("An-225")?.gameObject;
            Debug.Log($"Method 1 - Found aircraftModel: '{aircraftModel?.name}'");
            
            if (aircraftModel == null)
            {
                // Try finding by component
                MeshFilter meshFilter = GetComponentInChildren<MeshFilter>();
                if (meshFilter != null)
                {
                    aircraftModel = meshFilter.gameObject;
                    Debug.Log($"Method 2 - Found aircraftModel by MeshFilter: '{aircraftModel?.name}'");
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
                        Debug.Log($"Method 3 - Found aircraftModel (non-Canvas): '{aircraftModel?.name}'");
                        break;
                    }
                }
            }
        }

        if (labelText == null)
        {
            var labelObj = transform.Find("Canvas/Text (TMP)");
            if (labelObj != null)
                labelText = labelObj.GetComponent<TextMeshProUGUI>();
        }

        UpdateLabel();
    }

    void Update()
    {
        //if (isFlying && groundSpeed > 0)
        //{
            // Calculate continuous movement based on speed and heading
            //MoveAircraftContinuously();
        //}
    }

    void MoveAircraftContinuously()
    {
        // Convert speed from knots to meters per second
        float speedMs = groundSpeed * 0.514444f; // knots to m/s
        
        // Calculate movement per frame
        float deltaTime = Time.deltaTime;
        float distanceThisFrame = speedMs * deltaTime; // meters moved this frame
        
        // Aviation heading: 0° = North, 90° = East, 180° = South, 270° = West
        float headingRad = heading * Mathf.Deg2Rad;
        
        // Calculate lat/lon changes - CORRECT DIRECTION
        // North = +latitude, East = +longitude
        double deltaLat = (distanceThisFrame * Mathf.Cos(headingRad)) / 111320.0; // North component  
        double deltaLon = (distanceThisFrame * Mathf.Sin(headingRad)) / (111320.0 * Mathf.Cos((float)currentLat * Mathf.Deg2Rad)); // East component
        
        // Update position
        currentLat += deltaLat;
        currentLon += deltaLon;
        
        // Update globe anchor
        if (globeAnchor != null)
        {
            globeAnchor.longitudeLatitudeHeight = new Unity.Mathematics.double3(currentLon, currentLat, currentAlt);
        }
    }

    public void UpdatePosition(double longitude, double latitude, double altitude)
    {
        // Set new position as current position
        currentLon = longitude;
        currentLat = latitude;
        currentAlt = altitude;
        this.altitude = (float)altitude;
        
        // Update globe anchor immediately
        if (globeAnchor != null)
        {
            globeAnchor.longitudeLatitudeHeight = new Unity.Mathematics.double3(longitude, latitude, altitude);
        }
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
        callsign = newCallsign;
        altitude = newAltitude;
        groundSpeed = newGroundSpeed;
        
        // Start flying if we have speed
        isFlying = (groundSpeed > 0);
        
        UpdateLabel();
    }

    void UpdateLabel()
    {
        if (labelText != null && !string.IsNullOrEmpty(callsign))
        {
            labelText.text = $"{callsign}\nFL{altitude/100:F0}\n{groundSpeed:F0}kts\nHDG {heading:F0}°";
        }
    }

    public Unity.Mathematics.double3 GetCurrentPosition()
    {
        return new Unity.Mathematics.double3(currentLon, currentLat, currentAlt);
    }

    void OnValidate()
    {
        UpdateLabel();
    }
}