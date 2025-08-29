using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class AircraftLabelManager : MonoBehaviour
{
    [Header("Label Settings")]
    public GameObject labelPrefab;
    public Canvas screenCanvas;
    public float labelOffset = 50f;
    public float labelScale = 1f;
    public float fontSize = 16f; // Public font size variable
    public float constantSizeDistance = 1000f; // Reference distance for constant sizing

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
            mainCamera = FindObjectOfType<Camera>();
            Debug.LogWarning("Camera.main not found, using first camera found");
        }

        aircraftManager = FindObjectOfType<AircraftManager>();
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
        GameObject canvasGO = new GameObject("Aircraft Labels Canvas");
        screenCanvas = canvasGO.AddComponent<Canvas>();

        // Use WorldSpace for constant size control
        screenCanvas.renderMode = RenderMode.WorldSpace;
        screenCanvas.sortingOrder = 100;

        // Set up canvas for world space
        RectTransform canvasRect = screenCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();

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

            // Add TextMeshProUGUI component
            TextMeshProUGUI text = labelGO.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize; // Use public font size variable
            text.color = Color.green;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle = FontStyles.Bold;

            // Set text background color for visibility (instead of Image component)
            text.fontMaterial = text.font.material; // Ensure material exists
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
        TextMeshProUGUI text = labelGO.GetComponent<TextMeshProUGUI>();
        if (text != null && !string.IsNullOrEmpty(aircraft.callsign))
        {
            text.text = $"{aircraft.callsign}\nFL{aircraft.altitude / 100:F0}\n{aircraft.groundSpeed:F0}kts";
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
            // Use your updated offset values (1.5f multiplier) but in world coordinates
            Vector3 upOffset = Vector3.up * (labelOffset * 1.5f);
            Vector3 leftOffset = Vector3.left * (labelOffset * 1.5f);
            Vector3 labelWorldPos = worldPos + upOffset + leftOffset;

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
            if (aircraftLabels[icao24] != null)
            {
                Destroy(aircraftLabels[icao24]);
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
                Destroy(label);
        }
        aircraftLabels.Clear();
        Debug.Log("Cleared all aircraft labels");
    }

    void OnDestroy()
    {
        CancelInvoke();
    }
}