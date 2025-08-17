using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class AircraftManager : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject aircraftPrefab;

    [Header("Kafka Integration")]
    public WorkingKafkaConsumer kafkaConsumer;

    [Header("Settings")]
    public float updateInterval = 5f; // Update every 5 seconds
    public bool useSimulatedData = true; // Toggle for testing
    public int maxAircraft = 50; // Limit for performance
    public float aircraftTimeout = 30f; // Remove aircraft after 30 seconds of no updates

    [Header("Debug Info")]
    [SerializeField] private int activeAircraftCount = 0;
    [SerializeField] private List<string> aircraftList = new List<string>();
    [SerializeField] private int kafkaMessagesReceived = 0;

    private Dictionary<string, Aircraft_Controller> activeAircraft = new Dictionary<string, Aircraft_Controller>();
    private Dictionary<string, float> lastUpdateTime = new Dictionary<string, float>();
    private bool aircraftCreated = false;

    void Start()
    {
        if (aircraftPrefab == null)
        {
            Debug.LogError("Aircraft Prefab not assigned to AircraftManager!");
            return;
        }

        // Find Kafka consumer if not assigned
        if (kafkaConsumer == null)
        {
            kafkaConsumer = FindObjectOfType<WorkingKafkaConsumer>();
        }

        // Subscribe to Kafka consumer events
        if (kafkaConsumer != null)
        {
            kafkaConsumer.OnAircraftDataReceived += HandleKafkaAircraftData;
            Debug.Log($"AircraftManager: Subscribed to WorkingKafkaConsumer events");
        }
        else if (!useSimulatedData)
        {
            Debug.LogWarning("No Kafka consumer found! Switching to simulated data.");
            useSimulatedData = true;
        }

        // Start the update coroutine
        StartCoroutine(UpdateAircraftData());
        
        // Start cleanup routine for stale aircraft
        StartCoroutine(CleanupStaleAircraft());
        
        Debug.Log("Aircraft Manager started. Use simulated data: " + useSimulatedData);
    }

    IEnumerator UpdateAircraftData()
    {
        while (true)
        {
            if (useSimulatedData)
            {
                // DISABLED - Use WorkingKafkaConsumer instead
                //SimulateADSBData();
                Debug.Log("AircraftManager: Simulated data disabled - using WorkingKafkaConsumer");
            }
            // Real data comes through Kafka events (HandleKafkaAircraftData)

            yield return new WaitForSeconds(updateInterval);
        }
    }

    IEnumerator CleanupStaleAircraft()
    {
        while (true)
        {
            if (!useSimulatedData) // Only cleanup for real data
            {
                List<string> staleAircraft = new List<string>();
                float currentTime = Time.time;

                foreach (var kvp in lastUpdateTime)
                {
                    if (currentTime - kvp.Value > aircraftTimeout)
                    {
                        staleAircraft.Add(kvp.Key);
                    }
                }

                foreach (string icao24 in staleAircraft)
                {
                    Debug.Log($"Removing stale aircraft: {icao24}");
                    RemoveAircraft(icao24);
                }
            }

            yield return new WaitForSeconds(10f); // Check every 10 seconds
        }
    }

    // Handle real ASTERIX data from Kafka
    private void HandleKafkaAircraftData(FIMSAircraftData data)
    {
        kafkaMessagesReceived++;

        // Convert FIMS data to our format and update aircraft
        UpdateOrCreateAircraft(
            icao24: data.aircraft_id,
            callsign: !string.IsNullOrEmpty(data.callsign) ? data.callsign : data.aircraft_id,
            longitude: data.position.lon,
            latitude: data.position.lat,
            altitude: data.position.alt,
            heading: data.velocity.heading,
            groundSpeed: data.velocity.speed
        );

        // Update last seen time for cleanup
        lastUpdateTime[data.aircraft_id] = Time.time;

        if (kafkaConsumer.debugMessages)
        {
            Debug.Log($"Processed Kafka data for {data.callsign} ({data.aircraft_id})");
        }
    }

    public void UpdateOrCreateAircraft(string icao24, string callsign,
        double longitude, double latitude, double altitude,
        float heading, float groundSpeed)
    {
        // Check aircraft limit
        if (!activeAircraft.ContainsKey(icao24) && activeAircraft.Count >= maxAircraft)
        {
            Debug.LogWarning($"Aircraft limit reached ({maxAircraft}). Cannot add {icao24}");
            return;
        }

        if (activeAircraft.ContainsKey(icao24))
        {
            // Update existing aircraft
            Aircraft_Controller aircraft = activeAircraft[icao24];
            if (aircraft != null)
            {
                aircraft.UpdatePosition(longitude, latitude, altitude);
                aircraft.UpdateHeading(heading);
                aircraft.UpdateData(callsign, (float)altitude, groundSpeed);
            }
        }
        else
        {
            // Create new aircraft
            GameObject newAircraftGO = Instantiate(aircraftPrefab);
            Aircraft_Controller newAircraft = newAircraftGO.GetComponent<Aircraft_Controller>();

            if (newAircraft != null)
            {
                // Make sure it's a child of the georeference
                Transform georeference = FindObjectOfType<CesiumForUnity.CesiumGeoreference>()?.transform;
                if (georeference != null)
                {
                    newAircraftGO.transform.SetParent(georeference);
                }

                newAircraft.icao24 = icao24;
                newAircraft.callsign = callsign;
                newAircraft.altitude = (float)altitude;
                newAircraft.groundSpeed = groundSpeed;
                newAircraft.heading = heading;

                newAircraft.UpdatePosition(longitude, latitude, altitude);
                newAircraft.UpdateHeading(heading);
                newAircraft.UpdateData(callsign, (float)altitude, groundSpeed);

                activeAircraft[icao24] = newAircraft;
                UpdateDebugInfo();

                Debug.Log($"Created aircraft: {callsign} ({icao24}) at {latitude:F4}, {longitude:F4}");
            }
            else
            {
                Debug.LogError("Aircraft prefab doesn't have Aircraft_Controller component!");
                Destroy(newAircraftGO);
            }
        }
    }

    public void RemoveAircraft(string icao24)
    {
        if (activeAircraft.ContainsKey(icao24))
        {
            Destroy(activeAircraft[icao24].gameObject);
            activeAircraft.Remove(icao24);
            lastUpdateTime.Remove(icao24);
            UpdateDebugInfo();
            Debug.Log($"Removed aircraft: {icao24}");
        }
    }

    public void ClearAllAircraft()
    {
        foreach (var aircraft in activeAircraft.Values)
        {
            if (aircraft != null)
                Destroy(aircraft.gameObject);
        }
        activeAircraft.Clear();
        lastUpdateTime.Clear();
        UpdateDebugInfo();
        Debug.Log("Cleared all aircraft");
    }

    private void UpdateDebugInfo()
    {
        activeAircraftCount = activeAircraft.Count;
        aircraftList.Clear();
        foreach (var kvp in activeAircraft)
        {
            if (kvp.Value != null)
                aircraftList.Add($"{kvp.Value.callsign} ({kvp.Key})");
        }
    }

    // Public method to switch between simulated and real data
    public void SetSimulatedData(bool useSimulated)
    {
        useSimulatedData = useSimulated;
        
        if (!useSimulated)
        {
            ClearAllAircraft(); // Clear simulated aircraft when switching to real data
            aircraftCreated = false; // Reset flag
            
            // Make sure Kafka consumer is running
            if (kafkaConsumer != null && !kafkaConsumer.IsConnected())
            {
                kafkaConsumer.StartConsumer();
            }
        }
        else
        {
            // Reset simulated data flag
            aircraftCreated = false;
        }
        
        Debug.Log("Switched to " + (useSimulated ? "simulated" : "real") + " data mode");
    }

    public Dictionary<string, Aircraft_Controller> GetActiveAircraft()
    {
        return activeAircraft;
    }

    // Status methods for debugging
    public int GetKafkaMessagesReceived() => kafkaMessagesReceived;
    public bool IsKafkaConnected() => kafkaConsumer != null && kafkaConsumer.IsConnected();

    void OnValidate()
    {
        // Ensure reasonable limits
        updateInterval = Mathf.Clamp(updateInterval, 1f, 60f);
        maxAircraft = Mathf.Clamp(maxAircraft, 1, 1000);
        aircraftTimeout = Mathf.Clamp(aircraftTimeout, 10f, 300f);
    }

    void OnDestroy()
    {
        // Unsubscribe from Kafka events
        if (kafkaConsumer != null)
        {
            kafkaConsumer.OnAircraftDataReceived -= HandleKafkaAircraftData;
        }
    }
}