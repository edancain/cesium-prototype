using UnityEngine;
using CesiumForUnity;

public class GeoreferenceManager : MonoBehaviour
{
    [Header("Fixed Origin Settings")]
    public double originLatitude = -43.475309;   // Christchurch
    public double originLongitude = 172.547780;  // Christchurch  
    public double originHeight = 0;              // Sea level

    [Header("Components")]
    public CesiumGeoreference georeference;

    private Unity.Mathematics.double3 lockedOrigin;
    private bool originLocked = false;

    void Start()
    {
        // Find georeference if not assigned
        if (georeference == null)
        {
            georeference = FindObjectOfType<CesiumGeoreference>();
        }

        if (georeference == null)
        {
            Debug.LogError("CesiumGeoreference not found! GeoreferenceManager cannot work.");
            return;
        }

        // Set and lock the origin
        LockOriginPosition();
    }

    void LockOriginPosition()
    {
        if (georeference == null) return;

        // Set the desired origin using the correct API
        lockedOrigin = new Unity.Mathematics.double3(originLongitude, originLatitude, originHeight);
        georeference.longitude = originLongitude;
        georeference.latitude = originLatitude;
        georeference.height = originHeight;

        originLocked = true;

        Debug.Log($"Georeference origin locked to: Lat={originLatitude:F6}, Lon={originLongitude:F6}, H={originHeight}");
    }

    void LateUpdate()
    {
        // Continuously enforce the locked origin position
        if (originLocked && georeference != null)
        {
            // Check if origin has drifted from our locked position
            double deltaLat = System.Math.Abs(georeference.latitude - originLatitude);
            double deltaLon = System.Math.Abs(georeference.longitude - originLongitude);
            double deltaHeight = System.Math.Abs(georeference.height - originHeight);

            // If origin has moved significantly, reset it
            if (deltaLat > 0.001 || deltaLon > 0.001 || deltaHeight > 1.0)
            {
                Debug.Log($"Origin drifted! Resetting from ({georeference.latitude:F6}, {georeference.longitude:F6}, {georeference.height:F1}) back to locked position");
                georeference.longitude = originLongitude;
                georeference.latitude = originLatitude;
                georeference.height = originHeight;
            }
        }
    }

    // Public methods to change locked origin
    public void SetNewOrigin(double latitude, double longitude, double height)
    {
        originLatitude = latitude;
        originLongitude = longitude;
        originHeight = height;
        LockOriginPosition();
    }

    [ContextMenu("Reset to Christchurch Origin")]
    public void ResetToChristchurchOrigin()
    {
        SetNewOrigin(-43.475309, 172.547780, 0);
    }

    [ContextMenu("Reset to World Center Origin")]
    public void ResetToWorldCenterOrigin()
    {
        SetNewOrigin(0, 0, 0);
    }

    public void UnlockOrigin()
    {
        originLocked = false;
        Debug.Log("Georeference origin unlocked - will now move freely");
    }

    public bool IsOriginLocked()
    {
        return originLocked;
    }

    void OnValidate()
    {
        // Clamp coordinates to valid ranges
        originLatitude = System.Math.Clamp(originLatitude, -90.0, 90.0);
        originLongitude = System.Math.Clamp(originLongitude, -180.0, 180.0);
    }
}