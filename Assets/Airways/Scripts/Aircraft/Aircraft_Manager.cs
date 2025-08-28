using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System.Linq;

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
    private CesiumForUnity.CesiumGeoreference georeference;
    private Transform georeferenceTransform;

    void Start()
    {
        if (aircraftPrefab == null)
        {
            Debug.LogError("Aircraft Prefab not assigned to AircraftManager!");
            return;
        }

        // Find the georeference once at start
        georeference = FindObjectOfType<CesiumForUnity.CesiumGeoreference>();
        if (georeference == null)
        {
            Debug.LogError("CesiumGeoreference not found! Aircraft positioning won't work correctly.");
            Debug.LogError("Please create a GameObject with CesiumGeoreference component in your scene.");
        }
        else
        {
            georeferenceTransform = georeference.transform;
            Debug.Log($"Found CesiumGeoreference: {georeference.name}");
        }

        // Find Kafka consumer if not assigned
        if (kafkaConsumer == null)
        {
            kafkaConsumer = FindObjectOfType<WorkingKafkaConsumer>();
        }

        // Subscribe to Kafka consumer events BEFORE it starts
        if (kafkaConsumer != null)
        {
            kafkaConsumer.OnAircraftDataReceived += HandleKafkaAircraftData;
            Debug.Log($"AircraftManager: Subscribed to WorkingKafkaConsumer events");

            // DON'T restart if already running - this breaks Cesium initialization
            // Just ensure we're subscribed to the events
            if (!kafkaConsumer.IsConnected())
            {
                Debug.Log("AircraftManager: Kafka consumer not running, starting it");
                kafkaConsumer.StartConsumer();
            }
            else
            {
                Debug.Log("AircraftManager: Kafka consumer already running - using existing connection");
            }
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
        // Validate data before processing
        if (data == null)
        {
            Debug.LogWarning("Received null aircraft data from Kafka");
            return;
        }

        if (string.IsNullOrEmpty(data.aircraft_id))
        {
            Debug.LogWarning("Received aircraft data with empty aircraft_id");
            return;
        }

        if (data.position == null)
        {
            Debug.LogWarning($"Received aircraft data with null position for {data.aircraft_id}");
            return;
        }

        // Validate coordinates
        if (double.IsNaN(data.position.lat) || double.IsNaN(data.position.lon) || double.IsNaN(data.position.alt))
        {
            Debug.LogWarning($"Received invalid coordinates for {data.aircraft_id}: lat={data.position.lat}, lon={data.position.lon}, alt={data.position.alt}");
            return;
        }

        if (data.velocity == null)
        {
            Debug.LogWarning($"Received aircraft data with null velocity for {data.aircraft_id}");
            return;
        }

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
            Debug.Log($"Processed Kafka data for {data.callsign} ({data.aircraft_id}) at {data.position.lat:F4}, {data.position.lon:F4}");
        }
    }

    public void UpdateOrCreateAircraft(string icao24, string callsign,
        double longitude, double latitude, double altitude,
        float heading, float groundSpeed)
    {
        // Validate inputs
        if (string.IsNullOrEmpty(icao24))
        {
            Debug.LogWarning("Cannot create aircraft with empty ICAO24");
            return;
        }

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
                return; // Exit here to avoid recreating
            }
            else
            {
                // Aircraft controller is null, remove from dictionary and recreate
                Debug.LogWarning($"Aircraft controller for {icao24} was null, recreating...");
                activeAircraft.Remove(icao24);
                // Fall through to create new aircraft
            }
        }

        // Create new aircraft (or recreate)
        Debug.Log($"Creating NEW aircraft: {callsign} ({icao24}) at {latitude:F6}, {longitude:F6}, alt: {altitude}");

        GameObject newAircraftGO = Instantiate(aircraftPrefab);
        newAircraftGO.name = $"Aircraft_{callsign}_{icao24}"; // Give it a unique name

        // CRITICAL FIX: Parent aircraft to CesiumGeoreference
        if (georeferenceTransform != null)
        {
            newAircraftGO.transform.SetParent(georeferenceTransform);
            Debug.Log($"Aircraft {callsign} parented to CesiumGeoreference: {georeference.name}");
        }
        else
        {
            Debug.LogError($"Cannot parent aircraft {callsign} - CesiumGeoreference transform is null!");
        }

        Aircraft_Controller newAircraft = newAircraftGO.GetComponent<Aircraft_Controller>();

        if (newAircraft != null)
        {
            // Set aircraft data FIRST, before any updates
            newAircraft.icao24 = icao24;
            newAircraft.callsign = callsign;
            newAircraft.altitude = (float)altitude;
            newAircraft.groundSpeed = groundSpeed;
            newAircraft.heading = heading;

            Debug.Log($"Set aircraft data for {callsign}: alt={altitude}, speed={groundSpeed}, heading={heading}");

            // THEN update position and other properties
            newAircraft.UpdatePosition(longitude, latitude, altitude);
            newAircraft.UpdateHeading(heading);
            newAircraft.UpdateData(callsign, (float)altitude, groundSpeed);

            activeAircraft[icao24] = newAircraft;
            UpdateDebugInfo();

            Debug.Log($"SUCCESSFULLY Created aircraft: {callsign} ({icao24}) at {latitude:F4}, {longitude:F4}, altitude: {altitude}");
            Debug.Log($"Total active aircraft now: {activeAircraft.Count}");
        }
        else
        {
            Debug.LogError("Aircraft prefab doesn't have Aircraft_Controller component!");
            Destroy(newAircraftGO);
        }
    }

    public void RemoveAircraft(string icao24)
    {
        if (activeAircraft.ContainsKey(icao24))
        {
            if (activeAircraft[icao24] != null)
            {
                Destroy(activeAircraft[icao24].gameObject);
            }
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

    // SIMPLIFIED: Clean up null references before returning
    public Dictionary<string, Aircraft_Controller> GetActiveAircraft()
    {
        // Clean up only null references (remove IsValid check temporarily)
        var keysToRemove = activeAircraft.Where(kvp => kvp.Value == null).Select(kvp => kvp.Key).ToList();

        foreach (var key in keysToRemove)
        {
            Debug.LogWarning($"Removing null aircraft reference: {key}");
            activeAircraft.Remove(key);
        }

        UpdateDebugInfo();
        return new Dictionary<string, Aircraft_Controller>(activeAircraft);
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