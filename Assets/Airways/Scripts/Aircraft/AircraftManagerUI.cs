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

        // Start auto-update if enabled
        if (autoUpdate)
        {
            InvokeRepeating(nameof(UpdateAircraftList), 1f, updateInterval);
        }
    }

    public void UpdateAircraftList()
    {
        if (aircraftManager == null) return;

        // Get current aircraft from the manager
        var currentAircraft = GetActiveAircraft();

        // Remove buttons for aircraft that no longer exist
        List<string> toRemove = new List<string>();
        foreach (var kvp in aircraftButtons)
        {
            if (!currentAircraft.ContainsKey(kvp.Key))
            {
                toRemove.Add(kvp.Key);
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

            if (aircraftButtons.ContainsKey(icao24))
            {
                // Update existing button
                UpdateAircraftButton(icao24, aircraft);
            }
            else
            {
                // Create new button
                CreateAircraftButton(icao24, aircraft);
            }
        }
    }

    void UpdateAircraftButton(string icao24, Aircraft_Controller aircraft)
    {
        if (!aircraftButtons.ContainsKey(icao24)) return;

        GameObject buttonObj = aircraftButtons[icao24];
        TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();

        if (buttonText != null)
        {
            buttonText.text = aircraft.callsign;
            Debug.Log($"Updated button text for {aircraft.callsign} ({icao24})"); // Debug log
        }
    }

    void CreateAircraftButton(string icao24, Aircraft_Controller aircraft)
    {
        if (aircraftButtonPrefab == null || buttonContainer == null)
        {
            Debug.LogError("Aircraft button prefab or container not assigned!");
            return;
        }

        Debug.Log($"Creating button for aircraft: {aircraft.callsign} ({icao24})"); // ADD THIS

        // Instantiate button
        GameObject buttonObj = Instantiate(aircraftButtonPrefab, buttonContainer);

        Debug.Log($"Button created for {aircraft.callsign}, total buttons now: {aircraftButtons.Count + 1}"); // ADD THIS

        // Set up button text - ONLY CALLSIGN
        TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.text = aircraft.callsign;
            Debug.Log($"Button text set to: {buttonText.text}"); // ADD THIS
        }

        // Set up button click event
        Button button = buttonObj.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => FlyToAircraft(icao24));
        }

        // Store button reference
        aircraftButtons[icao24] = buttonObj;

        Debug.Log($"Button stored in dictionary. Dictionary now contains: {aircraftButtons.Count} buttons"); // ADD THIS
    }

    void RemoveAircraftButton(string icao24)
    {
        if (aircraftButtons.ContainsKey(icao24))
        {
            Destroy(aircraftButtons[icao24]);
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

        // Position camera DIRECTLY ABOVE aircraft
        double cameraLongitude = aircraftPosition.x;  // Same longitude
        double cameraLatitude = aircraftPosition.y;   // Same latitude  
        double cameraAltitude = aircraftPosition.z + cameraHeight; // Above aircraft

        // Fly camera to position above aircraft, looking straight down
        if (flyToController != null)
        {
            flyToController.FlyToLocationLongitudeLatitudeHeight(
                new Unity.Mathematics.double3(cameraLongitude, cameraLatitude, cameraAltitude),
                0f,   // yaw (facing north)
                90f, // pitch (looking straight down) 
                true  // can interrupt
            );

            Debug.Log($"Flying to aircraft: {aircraft.callsign} - positioned directly above, looking down");
        }
    }
    // Helper method to get active aircraft from the manager
    private Dictionary<string, Aircraft_Controller> GetActiveAircraft()
    {
        if (aircraftManager != null)
        {
            return aircraftManager.GetActiveAircraft();
        }

        return new Dictionary<string, Aircraft_Controller>();
    }

    // Public methods for manual control
    public void RefreshList()
    {
        UpdateAircraftList();
    }

    public void ClearAllButtons()
    {
        foreach (var button in aircraftButtons.Values)
        {
            if (button != null)
                Destroy(button);
        }
        aircraftButtons.Clear();
    }

    void OnDestroy()
    {
        CancelInvoke();
    }
}