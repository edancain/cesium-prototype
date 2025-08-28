using System;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using Newtonsoft.Json;

// FIMS ASTERIX data structure matching your Go service output
[System.Serializable]
public class FIMSAircraftData
{
    public string aircraft_id;
    public string callsign;
    public Position position;
    public Velocity velocity;
    public string timestamp;
    public string category;
}

[System.Serializable]
public class Position
{
    public double lat;
    public double lon;
    public double alt;
}

[System.Serializable]
public class Velocity
{
    public float speed;
    public float heading;
}

public class WorkingKafkaConsumer : MonoBehaviour
{
    [Header("Kafka Configuration")]
    public string bootstrapServers = "localhost:9092"; // Change to your Windows IP
    public string windowsKafkaIP = "192.168.1.100"; // Set your Windows machine IP here
    public bool useRemoteKafka = false; // Toggle between local and remote
    public string consumerGroupId = "unity-asterix-consumer";
    public List<string> topics = new List<string>
    {
        "surveillance.asterix.cat021", // ADS-B data
        "surveillance.asterix.cat048", // Radar target reports
        "surveillance.asterix.cat062", // System tracks
        "surveillance.asterix.cat034"  // Service messages
    };

    [Header("Settings")]
    public bool autoStart = true;
    public bool debugMessages = true;
    public bool useSimulatedData = true; // Start with simulated data
    public bool keepRunningInEditor = true; // NEW: Don't stop when losing focus
    public float simulatedDataInterval = 5f; // Slower updates - less jumping!
    public int maxMessagesPerFrame = 10;

    [Header("Status")]
    [SerializeField] private bool isConnected = false;
    [SerializeField] public int messagesReceived = 0;
    [SerializeField] public int messagesProcessed = 0;
    [SerializeField] private string lastError = "";

    // Events for aircraft data
    public System.Action<FIMSAircraftData> OnAircraftDataReceived;

    // Internal state
    private Queue<FIMSAircraftData> messageQueue = new Queue<FIMSAircraftData>();
    private bool initialAircraftCreated = false;

    // Flight path tracking for simulated aircraft
    private Dictionary<string, SimulatedAircraft> simulatedAircraft = new Dictionary<string, SimulatedAircraft>();

    [System.Serializable]
    public class SimulatedAircraft
    {
        public string icao24;
        public string callsign;
        public double lat;
        public double lon;
        public double alt;
        public float speed;
        public float heading;
        public string category;

        public SimulatedAircraft(string id, string call, double latitude, double longitude, double altitude, float groundSpeed, float hdg, string cat)
        {
            icao24 = id;
            callsign = call;
            lat = latitude;
            lon = longitude;
            alt = altitude;
            speed = groundSpeed;
            heading = hdg;
            category = cat;
        }
    }

    void Start()
    {
        Debug.Log("WorkingKafkaConsumer: Starting up...");

        // Initialize simulated aircraft data
        InitializeSimulatedAircraft();

        if (autoStart)
        {
            StartConsumer();
        }
    }

    void InitializeSimulatedAircraft()
    {
        simulatedAircraft.Clear();

        // ANZ123 (Christchurch, New Zealand)
        simulatedAircraft["ANZ123"] = new SimulatedAircraft(
            "ANZ123", "ANZ123",
            -43.475309, 172.547780, 5000,
            420, 90, "cat021"
        );

        // ICE001 (McMurdo Station, Antarctica)
        simulatedAircraft["ICE001"] = new SimulatedAircraft(
            "ICE001", "ICE001",
            -77.8419, 166.6863, 15000,
            200, 270, "cat048"
        );

        // LH456 (Berlin, Germany)
        simulatedAircraft["LH456"] = new SimulatedAircraft(
            "LH456", "LH456",
            52.5200, 13.4050, 37000,
            480, 170, "cat021"
        );

        Debug.Log($"WorkingKafkaConsumer: Initialized {simulatedAircraft.Count} simulated aircraft");
    }

    public void StartConsumer()
    {
        if (isConnected)
        {
            Debug.LogWarning("Consumer already running!");
            return;
        }

        if (useSimulatedData)
        {
            Debug.Log("WorkingKafkaConsumer: Starting simulated data mode...");

            // Create initial aircraft immediately
            CreateInitialAircraft();

            // Start regular updates
            InvokeRepeating(nameof(GenerateSimulatedData), simulatedDataInterval, simulatedDataInterval);
            isConnected = true;

            Debug.Log("WorkingKafkaConsumer: Started simulated Kafka consumer with ASTERIX data");
        }
        else
        {
            Debug.LogWarning("Real Kafka consumer not implemented yet. Falling back to simulated data.");
            useSimulatedData = true;
            StartConsumer(); // Restart with simulated data
        }
    }

    void CreateInitialAircraft()
    {
        Debug.Log("WorkingKafkaConsumer: Creating initial aircraft...");

        foreach (var aircraft in simulatedAircraft.Values)
        {
            var aircraftData = new FIMSAircraftData
            {
                aircraft_id = aircraft.icao24,
                callsign = aircraft.callsign,
                position = new Position { lat = aircraft.lat, lon = aircraft.lon, alt = aircraft.alt },
                velocity = new Velocity { speed = aircraft.speed, heading = aircraft.heading },
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                category = aircraft.category
            };

            Debug.Log($"WorkingKafkaConsumer: Creating {aircraft.callsign} at LAT={aircraft.lat:F6}, LON={aircraft.lon:F6}, ALT={aircraft.alt}");

            messageQueue.Enqueue(aircraftData);
            messagesReceived++;
        }

        initialAircraftCreated = true;
        Debug.Log($"WorkingKafkaConsumer: Queued initial data for {simulatedAircraft.Count} aircraft");
    }

    void GenerateSimulatedData()
    {
        if (!initialAircraftCreated)
        {
            Debug.LogWarning("Initial aircraft not created, creating now...");
            CreateInitialAircraft();
            return;
        }

        Debug.Log("WorkingKafkaConsumer: Generating simulated movement updates...");

        // Update each aircraft's position
        foreach (var kvp in simulatedAircraft)
        {
            var aircraft = kvp.Value;

            // Calculate movement based on speed and heading
            UpdateAircraftMovement(aircraft);

            // Create update data
            var updateData = new FIMSAircraftData
            {
                aircraft_id = aircraft.icao24,
                callsign = aircraft.callsign,
                position = new Position { lat = aircraft.lat, lon = aircraft.lon, alt = aircraft.alt },
                velocity = new Velocity { speed = aircraft.speed, heading = aircraft.heading },
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                category = aircraft.category
            };

            messageQueue.Enqueue(updateData);
            messagesReceived++;
        }

        Debug.Log($"WorkingKafkaConsumer: Generated updates for {simulatedAircraft.Count} aircraft");
    }

    void UpdateAircraftMovement(SimulatedAircraft aircraft)
    {
        // Convert speed from knots to degrees per update interval
        float speedKnots = aircraft.speed;
        float timeInterval = simulatedDataInterval; // seconds

        // Approximate: 1 degree latitude = 111 km, 1 knot = 1.852 km/h
        double speedKmH = speedKnots * 1.852;
        double speedKmPerInterval = speedKmH * (timeInterval / 3600.0);
        double speedDegreesPerInterval = speedKmPerInterval / 111.0;

        // Calculate movement based on heading
        float headingRad = aircraft.heading * Mathf.Deg2Rad;
        double deltaLat = speedDegreesPerInterval * Math.Cos(headingRad);
        double deltaLon = speedDegreesPerInterval * Math.Sin(headingRad) / Math.Cos(aircraft.lat * Math.PI / 180.0);

        // Update position
        aircraft.lat += deltaLat;
        aircraft.lon += deltaLon;

        // Implement simple wrap-around or bouncing for demo purposes
        switch (aircraft.icao24)
        {
            case "ANZ123": // Flying east from Christchurch
                if (aircraft.lon > 175.0) aircraft.lon = 172.547780; // Reset to start
                break;

            case "ICE001": // Flying west over Antarctica
                if (aircraft.lon < 164.0) aircraft.lon = 166.6863; // Reset to start
                break;

            case "LH456": // Flying south over Europe
                if (aircraft.lat < 50.0) aircraft.lat = 52.5200; // Reset to Berlin
                break;
        }

        if (debugMessages)
        {
            Debug.Log($"Updated {aircraft.callsign} position: {aircraft.lat:F4}, {aircraft.lon:F4}");
        }
    }

    public void StopConsumer()
    {
        if (!isConnected) return;

        CancelInvoke(); // Stop InvokeRepeating
        isConnected = false;
        Debug.Log("Consumer stopped");
    }

    void Update()
    {
        // Process queued messages on main thread
        ProcessQueuedMessages();
    }

    private void ProcessQueuedMessages()
    {
        int processed = 0;

        while (messageQueue.Count > 0 && processed < maxMessagesPerFrame)
        {
            FIMSAircraftData aircraftData = messageQueue.Dequeue();

            if (debugMessages)
            {
                Debug.Log($"WorkingKafkaConsumer: Processing aircraft {aircraftData.callsign} - Event listeners: {OnAircraftDataReceived?.GetInvocationList()?.Length ?? 0}");
            }

            // Invoke event for AircraftManager to handle
            OnAircraftDataReceived?.Invoke(aircraftData);

            messagesProcessed++;
            processed++;
        }
    }

    // Public methods for manual control
    public void RestartConsumer()
    {
        StopConsumer();
        initialAircraftCreated = false; // Reset flag so aircraft are recreated
        StartConsumer();
    }

    public void ClearMessageQueue()
    {
        messageQueue.Clear();
        Debug.Log("Message queue cleared");
    }

    public void SetSimulatedMode(bool useSimulated)
    {
        if (useSimulated != useSimulatedData)
        {
            useSimulatedData = useSimulated;
            if (isConnected)
            {
                RestartConsumer();
            }
        }
    }

    public void ForceReset()
    {
        StopConsumer();
        ClearMessageQueue();
        initialAircraftCreated = false;
        messagesReceived = 0;
        messagesProcessed = 0;
        InitializeSimulatedAircraft(); // Reinitialize aircraft data
        Debug.Log("WorkingKafkaConsumer reset - will create new aircraft on next start");
    }

    // Status methods
    public bool IsConnected() => isConnected;
    public int GetQueueSize() => messageQueue.Count;
    public int GetMessagesReceived() => messagesReceived;
    public int GetMessagesProcessed() => messagesProcessed;
    public string GetLastError() => lastError;

    void OnDestroy()
    {
        StopConsumer();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (keepRunningInEditor) return; // Don't stop in editor

        if (pauseStatus)
            StopConsumer();
        else if (autoStart)
            StartConsumer();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (keepRunningInEditor) return; // Don't stop in editor

        if (!hasFocus)
            StopConsumer();
        else if (autoStart)
            StartConsumer();
    }
}