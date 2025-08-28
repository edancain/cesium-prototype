using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CesiumForUnity;

public class AircraftUIManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject aircraftButtonPrefab;
    public Transform buttonContainer;
    public ScrollRect scrollRect;

    [Header("Camera Control")]
    public CesiumFlyToController flyToController;
    public float cameraDistance = 2000f; // Distance from aircraft
    public float cameraHeight = 500f;    // Height above aircraft

    [Header("Settings")]
    public bool autoUpdate = true;
    public float updateInterval = 2f;

    [Header("Debug")]
    [SerializeField] private int buttonCount = 0;
    [SerializeField] private List<string> buttonList = new List<string>();

    private AircraftManager aircraftManager;
    private Dictionary<string, GameObject> aircraftButtons = new Dictionary<string, GameObject>();

    void Start()
    {
        // Find the aircraft manager
        aircraftManager = FindObjectOfType<AircraftManager>();
        if (aircraftManager == null)
        {
            Debug.LogError("AircraftManager not found! Make sure it exists in the scene.");
            return;
        }

        // Find fly to controller if not assigned
        if (flyToController == null)
        {
            flyToController = FindObjectOfType<CesiumFlyToController>();
        }

        if (flyToController == null)
        {
            Debug.LogError("CesiumFlyToController not found! Camera fly-to won't work.");
        }

        // Validate UI components
        if (aircraftButtonPrefab == null)
        {
            Debug.LogError("Aircraft button prefab not assigned!");
        }

        if (buttonContainer == null)
        {
            Debug.LogError("Button container not assigned!");
        }

        // Start auto-update if enabled - but start immediately and more frequently
        if (autoUpdate)
        {
            // Update immediately, then every interval
            UpdateAircraftList();
            InvokeRepeating(nameof(UpdateAircraftList), updateInterval, updateInterval);
        }

        Debug.Log("AircraftUIManager started");
    }

    public void UpdateAircraftList()
    {
        if (aircraftManager == null)
        {
            Debug.LogWarning("AircraftManager is null, cannot update aircraft list");
            return;
        }

        // Get current aircraft from the manager
        var currentAircraft = GetActiveAircraft();

        Debug.Log($"UpdateAircraftList called - Found {currentAircraft.Count} aircraft in manager");

        // Remove buttons for aircraft that no longer exist
        List<string> toRemove = new List<string>();
        foreach (var kvp in aircraftButtons)
        {
            if (!currentAircraft.ContainsKey(kvp.Key))
            {
                toRemove.Add(kvp.Key);
                Debug.Log($"Marking button for removal: {kvp.Key}");
            }
        }

        foreach (string icao24 in toRemove)
        {
            RemoveAircraftButton(icao24);
        }

        // Add or update buttons for current aircraft
        foreach (var kvp in currentAircraft)
        {
            string icao24 = kvp.Key;
            Aircraft_Controller aircraft = kvp.Value;

            if (aircraft == null)
            {
                Debug.LogWarning($"Aircraft controller for {icao24} is null!");
                continue;
            }

            if (aircraftButtons.ContainsKey(icao24))
            {
                // Update existing button
                UpdateAircraftButton(icao24, aircraft);
                Debug.Log($"Updated existing button for {aircraft.callsign}");
            }
            else
            {
                // Create new button
                Debug.Log($"Creating NEW button for {aircraft.callsign} ({icao24})");
                CreateAircraftButton(icao24, aircraft);
            }
        }

        // Update debug info
        UpdateDebugInfo();
    }

    void UpdateAircraftButton(string icao24, Aircraft_Controller aircraft)
    {
        if (!aircraftButtons.ContainsKey(icao24)) return;

        GameObject buttonObj = aircraftButtons[icao24];
        if (buttonObj == null)
        {
            Debug.LogWarning($"Button object for {icao24} is null, removing from dictionary");
            aircraftButtons.Remove(icao24);
            return;
        }

        TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();

        if (buttonText != null && !string.IsNullOrEmpty(aircraft.callsign))
        {
            buttonText.text = aircraft.callsign;
            Debug.Log($"Updated button text for {aircraft.callsign} ({icao24})");
        }
    }

    void CreateAircraftButton(string icao24, Aircraft_Controller aircraft)
    {
        if (aircraftButtonPrefab == null || buttonContainer == null)
        {
            Debug.LogError("Aircraft button prefab or container not assigned!");
            return;
        }

        if (string.IsNullOrEmpty(aircraft.callsign))
        {
            Debug.LogWarning($"Aircraft {icao24} has no callsign, skipping button creation");
            return;
        }

        Debug.Log($"CREATING BUTTON for aircraft: {aircraft.callsign} ({icao24})");

        // Instantiate button
        GameObject buttonObj = Instantiate(aircraftButtonPrefab, buttonContainer);
        buttonObj.name = $"Button_{aircraft.callsign}"; // Give it a descriptive name

        // Position button with Y spacing of 45 units
        RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
        if (buttonRect != null)
        {
            int buttonIndex = aircraftButtons.Count; // Current number of buttons (0, 1, 2...)
            float yPosition = -buttonIndex * 45f;    // -0, -45, -90, etc.

            buttonRect.anchoredPosition = new Vector2(0f, yPosition);
            Debug.Log($"Button positioned at Y: {yPosition} (index: {buttonIndex})");
        }

        Debug.Log($"Button GameObject created: {buttonObj.name}");

        // Set up button text - ONLY CALLSIGN
        TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.text = aircraft.callsign;
            Debug.Log($"Button text set to: '{buttonText.text}'");
        }
        else
        {
            Debug.LogError($"Could not find TextMeshProUGUI component in button prefab for {aircraft.callsign}!");
        }

        // Set up button click event
        Button button = buttonObj.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => FlyToAircraft(icao24));
            Debug.Log($"Click listener added for {aircraft.callsign}");
        }
        else
        {
            Debug.LogError($"Could not find Button component in button prefab for {aircraft.callsign}!");
        }

        // Store button reference
        aircraftButtons[icao24] = buttonObj;

        Debug.Log($"Button successfully stored in dictionary for {aircraft.callsign}. Total buttons: {aircraftButtons.Count}");
    }

    void RemoveAircraftButton(string icao24)
    {
        if (aircraftButtons.ContainsKey(icao24))
        {
            Debug.Log($"Removing button for {icao24}");
            if (aircraftButtons[icao24] != null)
            {
                Destroy(aircraftButtons[icao24]);
            }
            aircraftButtons.Remove(icao24);
        }
    }

    public void FlyToAircraft(string icao24)
    {
        var activeAircraft = GetActiveAircraft();

        if (!activeAircraft.ContainsKey(icao24))
        {
            Debug.LogWarning($"Aircraft {icao24} not found!");
            return;
        }

        Aircraft_Controller aircraft = activeAircraft[icao24];
        if (aircraft == null || aircraft.globeAnchor == null)
        {
            Debug.LogWarning($"Aircraft {icao24} or its globe anchor is null!");
            return;
        }

        // Get aircraft position
        var aircraftPosition = aircraft.globeAnchor.longitudeLatitudeHeight;

        // Position camera DIRECTLY ABOVE aircraft for centered top-down view
        double cameraLongitude = aircraftPosition.x;  // Same longitude (centered)
        double cameraLatitude = aircraftPosition.y;   // Same latitude (centered)
        double cameraAltitude = aircraftPosition.z + cameraHeight; // Above aircraft

        // Fly camera to position above aircraft, looking straight down
        if (flyToController != null)
        {
            flyToController.FlyToLocationLongitudeLatitudeHeight(
                new Unity.Mathematics.double3(cameraLongitude, cameraLatitude, cameraAltitude),
                0f,    // yaw (facing north)
                90f,  // pitch (looking straight down)
                true   // can interrupt
            );

            Debug.Log($"Flying to aircraft: {aircraft.callsign} - positioned directly above at {cameraAltitude}m, looking straight down");
        }
    }

    // Helper method to get active aircraft from the manager
    private Dictionary<string, Aircraft_Controller> GetActiveAircraft()
    {
        if (aircraftManager != null)
        {
            var aircraft = aircraftManager.GetActiveAircraft();
            Debug.Log($"GetActiveAircraft: Retrieved {aircraft.Count} aircraft from manager");
            return aircraft;
        }

        Debug.LogWarning("AircraftManager is null!");
        return new Dictionary<string, Aircraft_Controller>();
    }

    private void UpdateDebugInfo()
    {
        buttonCount = aircraftButtons.Count;
        buttonList.Clear();

        foreach (var kvp in aircraftButtons)
        {
            if (kvp.Value != null)
            {
                var buttonText = kvp.Value.GetComponentInChildren<TextMeshProUGUI>();
                string displayText = buttonText != null ? buttonText.text : "No Text";
                buttonList.Add($"{kvp.Key}: {displayText}");
            }
        }
    }

    // Public methods for manual control
    public void RefreshList()
    {
        Debug.Log("Manual refresh requested");
        UpdateAircraftList();
    }

    public void ClearAllButtons()
    {
        Debug.Log($"Clearing all {aircraftButtons.Count} buttons");
        foreach (var button in aircraftButtons.Values)
        {
            if (button != null)
                Destroy(button);
        }
        aircraftButtons.Clear();
        UpdateDebugInfo();
    }

    // Manual button creation for debugging
    [ContextMenu("Force Create All Buttons")]
    public void ForceCreateAllButtons()
    {
        Debug.Log("Force creating buttons...");
        ClearAllButtons();
        UpdateAircraftList();
    }

    void OnDestroy()
    {
        CancelInvoke();
    }
}