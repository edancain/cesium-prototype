using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class AircraftLabelManager : MonoBehaviour
{
    [Header("Label Settings")]
    public GameObject labelPrefab;
    public Canvas screenCanvas;
    public float labelScale = 2f;
    public float fontSize = 16f;
    public float constantSizeDistance = 1000f; // Reference distance for constant sizing
    [Tooltip("Distance beyond which labels will start to shrink (in meters)")]
    public float maxConstantDistance = 10000f;

    [Header("Label Position")]
    [Tooltip("Vertical distance above the aircraft")]
    public float verticalOffset = 50f;
    [Tooltip("Horizontal distance from the aircraft")]
    public float horizontalOffset = -75f;
    [Tooltip("Direction of the horizontal offset (-1 for left, 1 for right)")]
    public float horizontalDirection = -1f; // -1 for left, 1 for right

    [Header("Label Style")]
    public Color textColor = new Color(0f, 1f, 1f, 1f); // Bright cyan

    [Header("Distance Indicator")]
    [Tooltip("Show circle indicator when distance exceeds this (meters)")]
    public float circleIndicatorDistance = 10000f;
    public Color circleColor = new Color(1f, 0f, 0f, 1f); // Bright red
    public float circleSize = 30f; // Increased size

    [Header("Debug")]
    public bool showDebugInfo = false;

    private Dictionary<string, GameObject> aircraftLabels = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> aircraftCircles = new Dictionary<string, GameObject>();
    private AircraftManager aircraftManager;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<Camera>();
            Debug.LogWarning("Camera.main not found, using first camera found");
        }
        aircraftManager = FindFirstObjectByType<AircraftManager>();
        if (aircraftManager == null)
        {
            Debug.LogError("AircraftManager not found!");
            return;
        }

        if (screenCanvas == null)
        {
            CreateScreenCanvas();
        }

        // Update labels more frequently to reduce lag
        InvokeRepeating(nameof(UpdateAllLabels), 0.1f, 0.1f);

        Debug.Log("AircraftLabelManager started");
    }

    void CreateScreenCanvas()
    {
        // Check if canvas already exists
        if (screenCanvas != null)
        {
            return;
        }

        GameObject canvasGO = new GameObject("Aircraft Labels Canvas");
        canvasGO.transform.SetParent(transform, false); // Parent to this object for proper cleanup

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        screenCanvas = canvas;

        // Use WorldSpace for constant size control
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        // Set up canvas for world space
        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect != null)
        {
            canvasRect.sizeDelta = new Vector2(1920, 1080);
        }

        // Add required components
        canvasGO.AddComponent<GraphicRaycaster>();

        // Add canvas scaler for consistent UI scaling
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.scaleFactor = 1;
        scaler.dynamicPixelsPerUnit = 100;

        Debug.Log("Created world space canvas for aircraft labels");
    }

    void UpdateAllLabels()
    {
        if (aircraftManager == null || mainCamera == null) return;

        var activeAircraft = aircraftManager.GetActiveAircraft();

        if (showDebugInfo && activeAircraft.Count > 0)
        {
            Debug.Log($"AircraftLabelManager: Updating {activeAircraft.Count} aircraft labels");
        }

        // Remove labels for aircraft that no longer exist
        List<string> toRemove = new List<string>();
        foreach (var kvp in aircraftLabels)
        {
            if (!activeAircraft.ContainsKey(kvp.Key))
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (string icao24 in toRemove)
        {
            RemoveLabel(icao24);
        }

        // Update or create labels for active aircraft
        foreach (var kvp in activeAircraft)
        {
            string icao24 = kvp.Key;
            Aircraft_Controller aircraft = kvp.Value;

            if (aircraft == null || aircraft.gameObject == null)
            {
                if (showDebugInfo)
                    Debug.LogWarning($"Aircraft controller for {icao24} is null!");
                continue;
            }

            if (aircraftLabels.ContainsKey(icao24))
            {
                UpdateLabel(icao24, aircraft);
            }
            else
            {
                CreateLabel(icao24, aircraft);
                if (showDebugInfo)
                    Debug.Log($"Created new label for {aircraft.callsign}");
            }
        }
    }

    void CreateLabel(string icao24, Aircraft_Controller aircraft)
    {
        if (screenCanvas == null) return;

        GameObject labelGO;

        if (labelPrefab != null)
        {
            labelGO = Instantiate(labelPrefab, screenCanvas.transform);
        }
        else
        {
            // Create simple label
            labelGO = new GameObject($"Label_{icao24}");
            labelGO.transform.SetParent(screenCanvas.transform, false);

            // Add RectTransform first (required for UI components)
            RectTransform rect = labelGO.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120, 60);

            // Add TextMeshProUGUI component directly to the label
            TextMeshProUGUI text = labelGO.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = textColor;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;
        }

        // Scale label
        labelGO.transform.localScale = Vector3.one * labelScale;

        aircraftLabels[icao24] = labelGO;

        Debug.Log($"Created label for {aircraft.callsign}");
    }

    void UpdateLabel(string icao24, Aircraft_Controller aircraft)
    {
        if (!aircraftLabels.ContainsKey(icao24)) return;

        GameObject labelGO = aircraftLabels[icao24];
        if (labelGO == null) return;

        // Update text content
        TextMeshProUGUI text = labelGO.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null && !string.IsNullOrEmpty(aircraft.callsign))
        {
            text.text = $"{aircraft.callsign}\nFL{aircraft.altitude / 100:F0}\n{aircraft.groundSpeed:F0}kts";
            // Ensure text renders on top
            text.gameObject.transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
        }

        // Get aircraft world position
        Vector3 worldPos = aircraft.GetWorldPosition();

        if (worldPos == Vector3.zero)
        {
            worldPos = aircraft.transform.position;
        }

        // Position label in world space with constant size
        if (worldPos != Vector3.zero)
        {
            // Calculate offsets based on the configurable settings
            Vector3 upOffset = Vector3.up * verticalOffset;
            Vector3 horizontalOffsetVector = Vector3.right * (horizontalOffset * horizontalDirection);
            Vector3 labelWorldPos = worldPos + upOffset + horizontalOffsetVector;

            labelGO.transform.position = labelWorldPos;

            // FORCE text to stay aligned with screen orientation at all angles
            Vector3 directionToCamera = (mainCamera.transform.position - labelWorldPos).normalized;
            Vector3 cameraUp = mainCamera.transform.up;
            labelGO.transform.rotation = Quaternion.LookRotation(-directionToCamera, cameraUp);

            // Get distance to camera
            float distanceToCamera = Vector3.Distance(mainCamera.transform.position, labelWorldPos);
            float scaleFactor;

            if (distanceToCamera <= maxConstantDistance)
            {
                // Maintain constant screen size up to maxConstantDistance
                scaleFactor = (distanceToCamera / constantSizeDistance) * labelScale;
            }
            else
            {
                // Calculate base scale at maxConstantDistance
                float baseScale = (maxConstantDistance / constantSizeDistance) * labelScale;
                // Beyond maxConstantDistance, start shrinking based on additional distance
                float shrinkFactor = maxConstantDistance / distanceToCamera;
                scaleFactor = baseScale * shrinkFactor;
            }

            // Ensure minimum scale so text doesn't disappear when very close
            scaleFactor = Mathf.Max(scaleFactor, 0.1f);
            labelGO.transform.localScale = Vector3.one * scaleFactor;

            // Update circle indicator
            UpdateCircleIndicator(icao24, aircraft, worldPos, distanceToCamera);

            // Show label
            if (!labelGO.activeInHierarchy)
                labelGO.SetActive(true);

            if (showDebugInfo)
            {
                Debug.Log($"Updated {aircraft.callsign} label: World pos {labelWorldPos}, Distance {distanceToCamera:F1}, Scale {scaleFactor:F2}");
            }
        }
        else
        {
            // Hide label if no valid position
            if (labelGO.activeInHierarchy)
                labelGO.SetActive(false);

            if (showDebugInfo)
            {
                Debug.Log($"{aircraft.callsign} label hidden: no valid world position");
            }
        }
    }

    void RemoveLabel(string icao24)
    {
        if (aircraftLabels.ContainsKey(icao24))
        {
            GameObject labelGO = aircraftLabels[icao24];
            if (labelGO != null)
            {
                // Clean up all child objects first
                foreach (Transform child in labelGO.transform)
                {
                    Destroy(child.gameObject);
                }
                Destroy(labelGO);
            }
            aircraftLabels.Remove(icao24);
            if (showDebugInfo)
                Debug.Log($"Removed label for {icao24}");
        }
    }

    public void ClearAllLabels()
    {
        foreach (var label in aircraftLabels.Values)
        {
            if (label != null)
            {
                // Clean up all child objects first
                foreach (Transform child in label.transform)
                {
                    Destroy(child.gameObject);
                }
                Destroy(label);
            }
        }
        aircraftLabels.Clear();
        Debug.Log("Cleared all aircraft labels");
    }

    private GameObject CreateCircleIndicator(string icao24)
    {
        GameObject circleGO = new GameObject($"Circle_{icao24}");
        circleGO.transform.SetParent(screenCanvas.transform, false);

        // Add RectTransform
        RectTransform rect = circleGO.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(10, 10); // Small base size, will scale with distance

        // Add Canvas to ensure proper rendering
        Canvas circleCanvas = circleGO.AddComponent<Canvas>();
        circleCanvas.overrideSorting = true;
        circleCanvas.sortingOrder = 1000; // Ensure it's on top

        // Add Image component for circle
        Image circle = circleGO.AddComponent<Image>();
        circle.color = new Color(1f, 0f, 0f, 1f); // Bright red
        circle.raycastTarget = false; // Disable raycasting for performance

        // Load circular sprite
        circle.sprite = CreateCircleSprite();

        return circleGO;
    }

    private Sprite CreateCircleSprite()
    {
        // Create a circular texture
        int textureSize = 32;
        Texture2D texture = new Texture2D(textureSize, textureSize);

        float radius = textureSize / 2f;
        Vector2 center = new Vector2(radius, radius);

        // Fill the texture with a circle
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = distance <= radius ? 1f : 0f;
                texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }

        texture.Apply();

        // Create sprite from texture
        return Sprite.Create(texture, new Rect(0, 0, textureSize, textureSize), new Vector2(0.5f, 0.5f));
    }

    private void UpdateCircleIndicator(string icao24, Aircraft_Controller aircraft, Vector3 worldPos, float distanceToCamera)
    {
        // Get or create circle
        GameObject circleGO;
        if (!aircraftCircles.TryGetValue(icao24, out circleGO))
        {
            circleGO = CreateCircleIndicator(icao24);
            aircraftCircles[icao24] = circleGO;
            if (showDebugInfo)
            {
                Debug.Log($"Created circle for {icao24}");
            }
        }

        if (circleGO != null)
        {
            // Always show circle
            circleGO.SetActive(true);

            // Update circle position
            circleGO.transform.position = worldPos;

            // Make circle face camera
            Vector3 directionToCamera = (mainCamera.transform.position - worldPos).normalized;
            circleGO.transform.rotation = Quaternion.LookRotation(-directionToCamera, mainCamera.transform.up);

            // Calculate scale based on distance with a minimum and maximum size
            float minScale = 1.0f;
            float maxScale = 5.0f;
            float baseDistance = 1000f; // Distance at which scaling starts
            float scaleMultiplier = Mathf.Clamp(distanceToCamera / baseDistance, minScale, maxScale);

            // Apply scale to RectTransform size
            RectTransform rect = circleGO.GetComponent<RectTransform>();
            if (rect != null)
            {
                float size = 10f * scaleMultiplier; // Base size * scale
                rect.sizeDelta = new Vector2(size, size);
            }

            // Keep transform scale at 1 since we're using RectTransform sizing
            circleGO.transform.localScale = Vector3.one;

            if (showDebugInfo)
            {
                Debug.Log($"Circle scale for {icao24}: {scaleMultiplier:F2} at distance {distanceToCamera:F0}m");
            }
        }
    }

    void OnDestroy()
    {
        ClearAllLabels();
        CancelInvoke();

        // Clean up circles
        foreach (var circle in aircraftCircles.Values)
        {
            if (circle != null)
            {
                Destroy(circle);
            }
        }
        aircraftCircles.Clear();

        if (screenCanvas != null)
        {
            Destroy(screenCanvas.gameObject);
        }
    }

    void OnValidate()
    {
        UpdateLabelColors();
    }

    private void UpdateLabelColors()
    {
        foreach (var label in aircraftLabels.Values)
        {
            if (label != null)
            {
                var text = label.GetComponent<TextMeshProUGUI>();
                if (text != null)
                {
                    text.color = textColor;
                }
            }
        }
    }
}