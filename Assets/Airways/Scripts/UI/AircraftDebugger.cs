using UnityEngine;
using CesiumForUnity;

public class AircraftDebugHelper : MonoBehaviour
{
    [Header("Debug Controls")]
    public bool showDebugInfo = true;
    public bool logPositionUpdates = true;

    [Header("Manual Aircraft Creation")]
    public GameObject aircraftPrefab;
    public double testLatitude = -43.475309;  // Christchurch
    public double testLongitude = 172.547780;
    public double testAltitude = 5000;

    private AircraftManager aircraftManager;
    private WorkingKafkaConsumer kafkaConsumer;

    void Start()
    {
        aircraftManager = FindObjectOfType<AircraftManager>();
        kafkaConsumer = FindObjectOfType<WorkingKafkaConsumer>();

        if (showDebugInfo)
        {
            InvokeRepeating(nameof(LogDebugInfo), 2f, 5f);
        }
    }

    void LogDebugInfo()
    {
        Debug.Log("=== AIRCRAFT DEBUG INFO ===");

        if (aircraftManager != null)
        {
            var activeAircraft = aircraftManager.GetActiveAircraft();
            Debug.Log($"AircraftManager: {activeAircraft.Count} active aircraft");

            foreach (var kvp in activeAircraft)
            {
                var aircraft = kvp.Value;
                if (aircraft != null)
                {
                    var pos = aircraft.GetCurrentPosition();
                    Debug.Log($"  {aircraft.callsign}: Lat={pos.y:F6}, Lon={pos.x:F6}, Alt={pos.z:F0}");

                    // Check if CesiumGlobeAnchor is working
                    if (aircraft.globeAnchor != null)
                    {
                        var anchorPos = aircraft.globeAnchor.longitudeLatitudeHeight;
                        Debug.Log($"    GlobeAnchor: Lon={anchorPos.x:F6}, Lat={anchorPos.y:F6}, Alt={anchorPos.z:F0}");
                        Debug.Log($"    Transform World Pos: {aircraft.transform.position}");
                    }
                }
            }
        }

        if (kafkaConsumer != null)
        {
            Debug.Log($"KafkaConsumer: Connected={kafkaConsumer.IsConnected()}, Messages={kafkaConsumer.GetMessagesReceived()}, Processed={kafkaConsumer.GetMessagesProcessed()}");
        }

        Debug.Log("=== END DEBUG INFO ===");
    }

    [ContextMenu("Direct Position Test")]
    public void DirectPositionTest()
    {
        // Find an existing aircraft and directly set its position
        if (aircraftManager != null)
        {
            var activeAircraft = aircraftManager.GetActiveAircraft();
            if (activeAircraft.Count > 0)
            {
                var firstAircraft = activeAircraft.Values.GetEnumerator();
                firstAircraft.MoveNext();
                var aircraft = firstAircraft.Current;

                if (aircraft != null && aircraft.globeAnchor != null)
                {
                    Debug.Log("DIRECT TEST: Setting aircraft position directly...");

                    // Set Christchurch coordinates directly
                    var testPos = new Unity.Mathematics.double3(172.547780, -43.475309, 5000);

                    Debug.Log($"BEFORE direct set: {aircraft.globeAnchor.longitudeLatitudeHeight}");
                    aircraft.globeAnchor.longitudeLatitudeHeight = testPos;
                    Debug.Log($"AFTER direct set: {aircraft.globeAnchor.longitudeLatitudeHeight}");

                    // Force transform update
                    aircraft.globeAnchor.enabled = false;
                    aircraft.globeAnchor.enabled = true;

                    Debug.Log($"Transform position after toggle: {aircraft.transform.position}");
                }
            }
        }
    }

    [ContextMenu("Test Christchurch Aircraft")]
    public void TestChristchurchAircraft()
    {
        if (aircraftManager != null)
        {
            Debug.Log("Creating aircraft specifically at Christchurch coordinates...");

            aircraftManager.UpdateOrCreateAircraft(
                icao24: "CHC001",
                callsign: "CHC001",
                longitude: 172.547780,  // Christchurch longitude
                latitude: -43.475309,   // Christchurch latitude
                altitude: 1000,         // Low altitude to see it clearly
                heading: 0f,            // North
                groundSpeed: 0f         // Stationary
            );
        }
    }

    [ContextMenu("Clear All Aircraft")]
    public void ClearAllAircraft()
    {
        if (aircraftManager != null)
        {
            aircraftManager.ClearAllAircraft();
        }
    }

    [ContextMenu("Restart Kafka Consumer")]
    public void RestartKafkaConsumer()
    {
        if (kafkaConsumer != null)
        {
            Debug.Log("Manually restarting Kafka consumer...");
            kafkaConsumer.ForceReset();
            kafkaConsumer.StartConsumer();
        }
    }

    [ContextMenu("Force Create All Aircraft")]
    public void ForceCreateAllAircraft()
    {
        if (aircraftManager != null)
        {
            // Create ANZ123
            aircraftManager.UpdateOrCreateAircraft(
                "ANZ123", "ANZ123",
                172.547780, -43.475309, 5000,
                90f, 420f
            );

            // Create ICE001  
            aircraftManager.UpdateOrCreateAircraft(
                "ICE001", "ICE001",
                166.6863, -77.8419, 15000,
                270f, 200f
            );

            // Create LH456
            aircraftManager.UpdateOrCreateAircraft(
                "LH456", "LH456",
                13.4050, 52.5200, 37000,
                170f, 480f
            );

            Debug.Log("Force created all 3 aircraft manually");
        }
    }

    void OnGUI()
    {
        if (!showDebugInfo) return;

        // Move debug panel to RIGHT side of screen
        float panelWidth = 300f;
        float panelHeight = 200f;
        float rightMargin = 10f;

        GUILayout.BeginArea(new Rect(Screen.width - panelWidth - rightMargin, 10, panelWidth, panelHeight));
        GUILayout.BeginVertical("box");

        GUILayout.Label("Aircraft Debug Helper");

        if (aircraftManager != null)
        {
            var activeCount = aircraftManager.GetActiveAircraft().Count;
            GUILayout.Label($"Active Aircraft: {activeCount}");
        }

        if (kafkaConsumer != null)
        {
            GUILayout.Label($"Kafka Connected: {kafkaConsumer.IsConnected()}");
            GUILayout.Label($"Messages Received: {kafkaConsumer.GetMessagesReceived()}");
        }

        if (GUILayout.Button("Force Create All"))
        {
            ForceCreateAllAircraft();
        }

        if (GUILayout.Button("Clear All"))
        {
            ClearAllAircraft();
        }

        if (GUILayout.Button("Restart Kafka"))
        {
            RestartKafkaConsumer();
        }

        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}