using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class AircraftLabelManager : MonoBehaviour
{
    [Header("Label Settings")]
    public GameObject labelPrefab; // A UI prefab with TextMeshProUGUI
    public Canvas screenCanvas; // Screen space canvas to hold all labels
    public float labelOffset = 150f; // Pixels above aircraft

    [Header("Debug")]
    public bool showDebugInfo = true;

    private Dictionary<string, GameObject> aircraftLabels = new Dictionary<string, GameObject>();
    private AircraftManager aircraftManager;
    private Camera mainCamera;

    void Start()
    {
        mainCamera = Camera.main;
        aircraftManager = FindObjectOfType<AircraftManager>();

        // Create screen canvas if not assigned
        if (screenCanvas == null)
        {
            CreateScreenCanvas();
        }

        // Update labels regularly
        InvokeRepeating(nameof(UpdateAllLabels), 0.1f, 0.1f);

        Debug.Log("AircraftLabelManager started");
    }

    void CreateScreenCanvas()
    {
        GameObject canvasGO = new GameObject("Aircraft Labels Canvas");
        screenCanvas = canvasGO.AddComponent<Canvas>();
        screenCanvas.renderMode = RenderMode.WorldSpace; // Changed to WorldSpace
        screenCanvas.sortingOrder = 100;

        // Set canvas to face camera and scale appropriately
        RectTransform canvasRect = screenCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1920, 1080);
        canvasRect.localScale = Vector3.one * 0.001f; // Small scale for world space

        canvasGO.AddComponent<GraphicRaycaster>();

        Debug.Log("Created world space canvas for aircraft labels");
    }

    void UpdateAllLabels()
    {
        if (aircraftManager == null || mainCamera == null) return;

        var activeAircraft = aircraftManager.GetActiveAircraft();

        if (showDebugInfo)
        {
            Debug.Log($"AircraftLabelManager: Found {activeAircraft.Count} active aircraft, have {aircraftLabels.Count} labels");
        }

        // Remove labels for aircraft that no longer exist
        List<string> toRemove = new List<string>();
        foreach (var kvp in aircraftLabels)
        {
            if (!activeAircraft.ContainsKey(kvp.Key))
            {
                toRemove.Add(kvp.Key);
                Debug.LogWarning($"AircraftLabelManager: Marking label {kvp.Key} for removal - not found in active aircraft");
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

            if (aircraft == null)
            {
                Debug.LogWarning($"AircraftLabelManager: Aircraft controller for {icao24} is null!");
                continue;
            }

            if (aircraftLabels.ContainsKey(icao24))
            {
                UpdateLabel(icao24, aircraft);
            }
            else
            {
                CreateLabel(icao24, aircraft);
                Debug.Log($"AircraftLabelManager: Created new label for {aircraft.callsign}");
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
            // Create simple label if no prefab assigned
            labelGO = new GameObject($"Label_{icao24}");
            labelGO.transform.SetParent(screenCanvas.transform);

            TextMeshProUGUI text = labelGO.AddComponent<TextMeshProUGUI>();
            text.fontSize = 14;
            text.color = Color.green;
            text.alignment = TextAlignmentOptions.Center;

            RectTransform rect = labelGO.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(120, 80);
        }

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
            text.text = $"{aircraft.callsign}\nFL{aircraft.altitude / 100:F0}\n{aircraft.groundSpeed:F0}kts\nHDG {aircraft.heading:F0}°";
        }

        // Get world position from aircraft
        Vector3 worldPos;
        if (aircraft.globeAnchor != null)
        {
            worldPos = aircraft.transform.position;
            var cesiumPos = aircraft.globeAnchor.longitudeLatitudeHeight;

            if (showDebugInfo)
            {
                Debug.Log($"{aircraft.callsign}: Cesium pos {cesiumPos}, Transform pos {worldPos}");
            }

            if (worldPos == Vector3.zero && (cesiumPos.x != 0 || cesiumPos.y != 0 || cesiumPos.z != 0))
            {
                Debug.LogWarning($"{aircraft.callsign}: Transform position is zero but Cesium has coordinates. Position sync issue.");
                if (labelGO.activeInHierarchy)
                    labelGO.SetActive(false);
                return;
            }
        }
        else
        {
            worldPos = aircraft.transform.position;
        }

        // MAIN FIX: Position label directly in world space above and to the left of aircraft
        if (worldPos != Vector3.zero)
        {
            // Use consistent world-space offsets regardless of camera angle
            Vector3 upOffset = Vector3.up * (labelOffset * 0.5f); // Increased scale
            Vector3 leftOffset = Vector3.left * (labelOffset * 0.5f); // Same scale for clear side positioning
            Vector3 labelWorldPos = worldPos + upOffset + leftOffset;

            labelGO.transform.position = labelWorldPos;

            // Make label face camera
            labelGO.transform.LookAt(mainCamera.transform);
            labelGO.transform.Rotate(0, 180, 0); // Flip to face camera correctly

            // Show label - it will be visible as long as aircraft is visible
            if (!labelGO.activeInHierarchy)
                labelGO.SetActive(true);

            if (showDebugInfo)
            {
                Debug.Log($"Updated {aircraft.callsign} label: Aircraft at {worldPos}, Label at {labelWorldPos}");
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