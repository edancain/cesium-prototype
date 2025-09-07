using UnityEngine;
using CesiumForUnity;
using Unity.Mathematics;

public class GlobeLineDrawer : MonoBehaviour
{
    public CesiumGeoreference cesiumGeoreference;
    public Material materialForLine;

    [Header("Line Configuration")]
    public int segmentCount = 20;
    public float cubeSize = 100f;
    public float connectionWidth = 50f;
    public bool displayCubes = true;

    private GameObject[] anchorPoints;
    private LineRenderer[] connectionLines;

    void Start()
    {
        if (cesiumGeoreference == null)
        {
            cesiumGeoreference = FindObjectOfType<CesiumGeoreference>();
        }

        if (cesiumGeoreference == null)
        {
            Debug.LogError("CesiumGeoreference not found!");
            return;
        }

        BuildGlobeAnchoredLine();
    }

    void BuildGlobeAnchoredLine()
    {
        // Christchurch and Wellington coordinates
        double3 christchurch = new double3(172.6362, -43.5321, 500.0);
        double3 wellington = new double3(174.7787, -41.2924, 500.0);

        // Create parent container
        GameObject lineContainer = new GameObject("Globe_Line_CHCH_to_Wellington");
        lineContainer.transform.SetParent(cesiumGeoreference.transform);

        // Initialize arrays
        anchorPoints = new GameObject[segmentCount + 1];
        connectionLines = new LineRenderer[segmentCount];

        // Create anchor segments
        for (int i = 0; i <= segmentCount; i++)
        {
            float interpolation = (float)i / segmentCount;

            // Calculate position along the path
            double3 currentPosition = new double3(
                math.lerp(christchurch.x, wellington.x, interpolation),
                math.lerp(christchurch.y, wellington.y, interpolation),
                math.lerp(christchurch.z, wellington.z, interpolation)
            );

            // Create anchor cube
            GameObject anchorCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            anchorCube.name = $"Anchor_{i}";
            anchorCube.transform.SetParent(lineContainer.transform);
            anchorCube.transform.localScale = new Vector3(cubeSize, cubeSize, cubeSize);

            // Add globe anchor component
            CesiumGlobeAnchor globeAnchor = anchorCube.AddComponent<CesiumGlobeAnchor>();
            globeAnchor.longitudeLatitudeHeight = currentPosition;

            // Set material and visibility
            Renderer cubeRenderer = anchorCube.GetComponent<Renderer>();
            if (materialForLine != null)
            {
                cubeRenderer.material = materialForLine;
            }
            else
            {
                cubeRenderer.material = new Material(Shader.Find("Standard"));
                cubeRenderer.material.color = Color.red;
            }
            cubeRenderer.enabled = displayCubes;

            anchorPoints[i] = anchorCube;
        }

        // Create connecting lines
        for (int i = 0; i < segmentCount; i++)
        {
            GameObject connectionObject = new GameObject($"Connection_{i}");
            connectionObject.transform.SetParent(lineContainer.transform);

            LineRenderer lineComponent = connectionObject.AddComponent<LineRenderer>();
            lineComponent.material = materialForLine != null ? materialForLine : new Material(Shader.Find("Sprites/Default"));
            lineComponent.startWidth = connectionWidth;
            lineComponent.endWidth = connectionWidth;
            lineComponent.positionCount = 2;
            lineComponent.useWorldSpace = true;

            if (materialForLine == null)
            {
                lineComponent.startColor = Color.red;
                lineComponent.endColor = Color.red;
            }

            connectionLines[i] = lineComponent;
        }

        Debug.Log($"Globe line created with {segmentCount + 1} anchors and {segmentCount} connections");
    }

    void Update()
    {
        // Update line connections based on anchor positions
        if (anchorPoints != null && connectionLines != null)
        {
            for (int i = 0; i < connectionLines.Length; i++)
            {
                if (anchorPoints[i] != null && anchorPoints[i + 1] != null && connectionLines[i] != null)
                {
                    connectionLines[i].SetPosition(0, anchorPoints[i].transform.position);
                    connectionLines[i].SetPosition(1, anchorPoints[i + 1].transform.position);
                }
            }
        }
    }
}