using System.Collections.Generic;
using UnityEngine;
using CesiumForUnity;

public class ModernAircraftUI : MonoBehaviour
{
    [Header("UI Settings")]
    public bool showAircraftPanel = true;
    public float updateInterval = 1f;

    [Header("Panel Layout")]
    public float panelWidth = 250f;
    public float panelHeight = 400f;
    public float leftMargin = 10f;
    public float topMargin = 10f;

    [Header("Button Styling")]
    public float buttonHeight = 60f;
    public float buttonSpacing = 5f;

    [Header("Camera Control")]
    public CesiumFlyToController flyToController;
    public float cameraHeight = 1000f;
    public float cameraDistance = 2000f;

    // NEW: Aircraft following
    private string followingAircraftId = null;
    private float followUpdateRate = 0.1f; // Update every 0.1 seconds

    private AircraftManager aircraftManager;
    private Dictionary<string, Aircraft_Controller> currentAircraft = new Dictionary<string, Aircraft_Controller>();
    private Vector2 scrollPosition = Vector2.zero;
    private GUIStyle buttonStyle;
    private GUIStyle panelStyle;
    private GUIStyle headerStyle;
    private bool stylesInitialized = false;

    void Start()
    {
        aircraftManager = FindObjectOfType<AircraftManager>();
        if (aircraftManager == null)
        {
            Debug.LogError("AircraftManager not found! Make sure it exists in the scene.");
            return;
        }

        if (flyToController == null)
        {
            flyToController = FindObjectOfType<CesiumFlyToController>();
        }

        if (flyToController == null)
        {
            Debug.LogWarning("CesiumFlyToController not found! Camera fly-to won't work.");
        }

        InvokeRepeating(nameof(RefreshAircraftList), 0.5f, updateInterval);

        // NEW: Start following update
        InvokeRepeating(nameof(UpdateFollowingAircraft), followUpdateRate, followUpdateRate);

        Debug.Log("ModernAircraftUI started");
    }

    void InitializeStyles()
    {
        if (stylesInitialized) return;

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 12;
        buttonStyle.fontStyle = FontStyle.Bold;
        buttonStyle.alignment = TextAnchor.MiddleLeft;
        buttonStyle.padding = new RectOffset(10, 10, 8, 8);
        buttonStyle.normal.textColor = Color.white;
        buttonStyle.hover.textColor = Color.cyan;

        panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.padding = new RectOffset(10, 10, 10, 10);

        headerStyle = new GUIStyle(GUI.skin.label);
        headerStyle.fontSize = 16;
        headerStyle.fontStyle = FontStyle.Bold;
        headerStyle.alignment = TextAnchor.MiddleCenter;
        headerStyle.normal.textColor = Color.white;

        stylesInitialized = true;
    }

    public void RefreshAircraftList()
    {
        if (aircraftManager == null) return;

        currentAircraft = aircraftManager.GetActiveAircraft();

        if (currentAircraft.Count > 0)
        {
            Debug.Log($"ModernAircraftUI: Refreshed list with {currentAircraft.Count} aircraft");
        }
    }

    void OnGUI()
    {
        if (!showAircraftPanel) return;

        InitializeStyles();

        Rect panelRect = new Rect(leftMargin, topMargin, panelWidth, panelHeight);

        GUILayout.BeginArea(panelRect, "AIRCRAFT RADAR", panelStyle);
        GUILayout.Space(30);

        GUILayout.Label("ACTIVE AIRCRAFT", headerStyle);
        GUILayout.Space(10);

        GUILayout.Label($"Tracking: {currentAircraft.Count} aircraft", GUI.skin.label);

        // NEW: Show following status
        if (!string.IsNullOrEmpty(followingAircraftId))
        {
            var followedAircraft = currentAircraft.ContainsKey(followingAircraftId) ? currentAircraft[followingAircraftId] : null;
            string followedName = followedAircraft?.callsign ?? followingAircraftId;
            GUILayout.Label($"Following: {followedName}", GUI.skin.label);
        }

        GUILayout.Space(10);

        float scrollViewHeight = panelHeight - 100f;
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.Height(scrollViewHeight));

        if (currentAircraft.Count == 0)
        {
            GUILayout.Label("No aircraft detected...", GUI.skin.label);
            GUILayout.Space(10);
            if (GUILayout.Button("Force Create Test Aircraft", buttonStyle, GUILayout.Height(buttonHeight)))
            {
                CreateTestAircraft();
            }
        }
        else
        {
            foreach (var kvp in currentAircraft)
            {
                string icao24 = kvp.Key;
                Aircraft_Controller aircraft = kvp.Value;

                if (aircraft == null) continue;

                // NEW: Change button color if this aircraft is being followed
                GUIStyle currentButtonStyle = buttonStyle;
                if (icao24 == followingAircraftId)
                {
                    // Create temporary style for following aircraft
                    currentButtonStyle = new GUIStyle(buttonStyle);
                    currentButtonStyle.normal.textColor = Color.yellow;
                    currentButtonStyle.hover.textColor = Color.white;
                }

                string buttonText = $"{aircraft.callsign}\n" +
                                  $"FL{aircraft.altitude / 100:F0} • {aircraft.groundSpeed:F0}kts\n" +
                                  $"HDG {aircraft.heading:F0}°";

                if (GUILayout.Button(buttonText, currentButtonStyle, GUILayout.Height(buttonHeight + 20)))
                {
                    ToggleFollowAircraft(icao24); // CHANGED: Now toggle instead of just fly to
                }

                GUILayout.Space(buttonSpacing);
            }
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // NEW: Toggle follow functionality
    public void ToggleFollowAircraft(string icao24)
    {
        if (followingAircraftId == icao24)
        {
            // Stop following
            followingAircraftId = null;
            Debug.Log($"Stopped following aircraft {icao24}");
        }
        else
        {
            // Start following new aircraft
            followingAircraftId = icao24;
            FlyToAircraft(icao24); // Initial fly to
            Debug.Log($"Now following aircraft {icao24}");
        }
    }

    // NEW: Update following aircraft camera position
    void UpdateFollowingAircraft()
    {
        if (string.IsNullOrEmpty(followingAircraftId) || flyToController == null) return;

        if (!currentAircraft.ContainsKey(followingAircraftId))
        {
            // Aircraft no longer exists, stop following
            followingAircraftId = null;
            return;
        }

        Aircraft_Controller aircraft = currentAircraft[followingAircraftId];
        if (aircraft == null || aircraft.globeAnchor == null) return;

        // Get current aircraft position
        var aircraftPosition = aircraft.globeAnchor.longitudeLatitudeHeight;

        // Update camera to follow aircraft
        double cameraLongitude = aircraftPosition.x;
        double cameraLatitude = aircraftPosition.y;
        double cameraAltitude = aircraftPosition.z + cameraHeight;

        flyToController.FlyToLocationLongitudeLatitudeHeight(
            new Unity.Mathematics.double3(cameraLongitude, cameraLatitude, cameraAltitude),
            0f,   // yaw
            90f,  // pitch (looking down)
            true  // can interrupt
        );
    }

    public void FlyToAircraft(string icao24)
    {
        if (!currentAircraft.ContainsKey(icao24))
        {
            Debug.LogWarning($"Aircraft {icao24} not found!");
            return;
        }

        Aircraft_Controller aircraft = currentAircraft[icao24];
        if (aircraft == null || aircraft.globeAnchor == null)
        {
            Debug.LogWarning($"Aircraft {icao24} or its globe anchor is null!");
            return;
        }

        var aircraftPosition = aircraft.globeAnchor.longitudeLatitudeHeight;

        Debug.Log($"Flying to {aircraft.callsign} at {aircraftPosition.y:F4}, {aircraftPosition.x:F4}");

        double cameraLongitude = aircraftPosition.x;
        double cameraLatitude = aircraftPosition.y;
        double cameraAltitude = aircraftPosition.z + cameraHeight;

        if (flyToController != null)
        {
            flyToController.FlyToLocationLongitudeLatitudeHeight(
                new Unity.Mathematics.double3(cameraLongitude, cameraLatitude, cameraAltitude),
                0f,   // yaw
                90f,  // pitch
                true  // can interrupt
            );

            Debug.Log($"Camera flying to: {aircraft.callsign} - positioned directly above at {cameraAltitude}m, looking straight down");
        }
    }

    public void CreateTestAircraft()
    {
        if (aircraftManager != null)
        {
            Debug.Log("Creating test aircraft manually...");

            aircraftManager.UpdateOrCreateAircraft(
                "ANZ123", "ANZ123",
                172.547780, -43.475309, 5000,
                90f, 420f
            );

            aircraftManager.UpdateOrCreateAircraft(
                "ICE001", "ICE001",
                166.6863, -77.8419, 15000,
                270f, 200f
            );

            aircraftManager.UpdateOrCreateAircraft(
                "LH456", "LH456",
                13.4050, 52.5200, 37000,
                170f, 480f
            );

            Debug.Log("Created 3 test aircraft manually");
        }
    }

    public void TogglePanel()
    {
        showAircraftPanel = !showAircraftPanel;
    }

    void OnDestroy()
    {
        CancelInvoke();
    }
}