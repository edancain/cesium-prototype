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

    [Header("Label Position")]
    [Tooltip("Vertical distance above the aircraft")]
    public float verticalOffset = 50f;
    [Tooltip("Horizontal distance from the aircraft")]
    public float horizontalOffset = -75f;
    [Tooltip("Direction of the horizontal offset (-1 for left, 1 for right)")]
    public float horizontalDirection = -1f; // -1 for left, 1 for right

    [Header("Label Style")]
    public Color textColor = new Color(0f, 1f, 1f, 1f); // Bright cyan

    [Header("Debug")]
    public bool showDebugInfo = false;

    private Dictionary<string, GameObject> aircraftLabels = new Dictionary<string, GameObject>();
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

            // Calculate constant size based on camera distance - improved formula
            float distanceToCamera = Vector3.Distance(mainCamera.transform.position, labelWorldPos);
            float scaleFactor = (distanceToCamera / constantSizeDistance) * labelScale;
            // Ensure minimum scale so text doesn't disappear when very close
            scaleFactor = Mathf.Max(scaleFactor, 0.1f);
            labelGO.transform.localScale = Vector3.one * scaleFactor;

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

    void OnDestroy()
    {
        ClearAllLabels();
        CancelInvoke();

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