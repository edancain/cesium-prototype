using UnityEngine;
using Unity.Mathematics;
using CesiumForUnity;

public class PlaceCylindersAtCities : MonoBehaviour
{
    // Drag your new "CityMarker_Prefab" from the Assets folder onto this slot in the Inspector.
    [Header("Prefab")]
    public GameObject cityMarkerPrefab;

    void Start()
    {
        if (cityMarkerPrefab == null)
        {
            Debug.LogError("City Marker Prefab is not assigned in the Inspector!");
            return;
        }

        // Christchurch: Lon=172.6362, Lat=-43.5321
        CreateCylinder(new double3(172.6362, -43.5321, 0), "ChristchurchCylinder");

        // Wellington: Lon=174.7787, Lat=-41.2924
        CreateCylinder(new double3(174.7787, -41.2924, 0), "WellingtonCylinder");
    }

    // This is now a simple, regular method again.
    private void CreateCylinder(double3 coords, string name)
    {
        // 1. Instantiate the prefab you created.
        GameObject newMarker = Instantiate(cityMarkerPrefab);
        newMarker.name = name;

        // 2. Get the CesiumGlobeAnchor component that's already on the prefab.
        CesiumGlobeAnchor anchor = newMarker.GetComponent<CesiumGlobeAnchor>();

        // 3. Set the coordinates directly. It works instantly because the component
        // was initialized with the object.
        if (anchor != null)
        {
            anchor.longitudeLatitudeHeight = coords;
        }
        else
        {
            Debug.LogError("The assigned prefab is missing the CesiumGlobeAnchor component!");
        }
    }
}