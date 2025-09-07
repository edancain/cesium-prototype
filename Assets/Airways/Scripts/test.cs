using UnityEngine;
using CesiumForUnity;
using Unity.Mathematics;

public class DrawLineOnMap : MonoBehaviour
{
    public CesiumGeoreference georeference;
    public Material lineMaterial;

    [Header("Line Settings")]
    public int numberOfSegments = 20; // Number of cubes to create the line
    public float segmentSize = 100f;   // Size of each segment in meters

    void Start()
    {
        if (georeference == null)
        {
            georeference = FindObjectOfType<CesiumGeoreference>();
        }

        if (georeference == null)
        {
            Debug.LogError("CesiumGeoreference not found!");
            return;
        }

        CreateGlobeAnchoredLine();
    }

    void CreateGlobeAnchoredLine()
    {
        // Christchurch and Wellington coordinates
        double3 startCoords = new double3(172.6362, -43.5321, 500.0);
        double3 endCoords = new double3(174.7787, -41.2924, 500.0);

        // Create parent object for the line
        GameObject lineParent = new GameObject("CHCH_to_Wellington_Line");
        lineParent.transform.SetParent(georeference.transform);

        // Create segments along the line (same approach as your aircraft system)
        for (int i = 0; i <= numberOfSegments; i++)
        {
            float t = (float)i / numberOfSegments;

            // Interpolate between start and end coordinates
            double3 segmentCoords = new double3(
                math.lerp(startCoords.x, endCoords.x, t),
                math.lerp(startCoords.y, endCoords.y, t),
                math.lerp(startCoords.z, endCoords.z, t)
            );

            // Create cube segment (same pattern as your aircraft prefab)
            GameObject segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
            segment.name = $"LineSegment_{i}";
            segment.transform.SetParent(lineParent.transform);

            // Scale the cube to create line width
            segment.transform.localScale = new Vector3(segmentSize, segmentSize, segmentSize);

            // Add CesiumGlobeAnchor (same as Aircraft_Controller)
            CesiumGlobeAnchor anchor = segment.AddComponent<CesiumGlobeAnchor>();
            anchor.longitudeLatitudeHeight = segmentCoords;

            // Apply material
            Renderer renderer = segment.GetComponent<Renderer>();
            if (lineMaterial != null)
            {
                renderer.material = lineMaterial;
            }
            else
            {
                renderer.material = new Material(Shader.Find("Standard"));
                renderer.material.color = Color.red;
            }

            Debug.Log($"Created line segment {i} at {segmentCoords.x:F4}, {segmentCoords.y:F4}, {segmentCoords.z:F0}");
        }

        Debug.Log($"Created globe-anchored line with {numberOfSegments + 1} segments from Christchurch to Wellington");
    }
}