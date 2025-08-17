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
    private bool aircraftCreated = false;
    
    // Flight path tracking
    private double anz123_lat = -43.475309;
    private double anz123_lon = 172.547780;
    private double ice001_lat = -77.8419;
    private double ice001_lon = 166.6863;

    private double lh456_lat = 52.5200;  // Berlin coordinates
    private double lh456_lon = 13.4050;

    void Start()
    {
        Debug.Log("WorkingKafkaConsumer: Starting up...");
        if (autoStart)
        {
            StartConsumer();
        }
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
            Debug.Log("WorkingKafkaConsumer: Starting with InvokeRepeating...");
            
            // Use InvokeRepeating instead of coroutine
            InvokeRepeating(nameof(GenerateSimulatedData), 1f, simulatedDataInterval);
            isConnected = true;
            
            Debug.Log("WorkingKafkaConsumer: Started simulated Kafka consumer with ASTERIX data");
        }
        else
        {
            Debug.LogWarning("Real Kafka consumer not implemented yet.");
        }
    }

    void GenerateSimulatedData()
    {
        Debug.Log("WorkingKafkaConsumer: GenerateSimulatedData called!");
        
        if (!aircraftCreated)
        {
            Debug.Log("WorkingKafkaConsumer: Creating aircraft for first time...");
            
            // Create ANZ123 (Christchurch, New Zealand - starting position)
            var nzAircraft = new FIMSAircraftData
            {
                aircraft_id = "ANZ123",
                callsign = "ANZ123",
                position = new Position { lat = anz123_lat, lon = anz123_lon, alt = 5000 },
                velocity = new Velocity { speed = 420, heading = 90 }, // Heading east
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                category = "cat021"
            };

            // Create ICE001 (McMurdo Station, Antarctica - starting position)
            var antarcticaAircraft = new FIMSAircraftData
            {
                aircraft_id = "ICE001",
                callsign = "ICE001",
                position = new Position { lat = ice001_lat, lon = ice001_lon, alt = 15000 },
                velocity = new Velocity { speed = 200, heading = 270 }, // Heading west
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                category = "cat048"
            };

            // Create LH456 (Berlin, Germany)
            var berlinAircraft = new FIMSAircraftData
            {
                aircraft_id = "LH456",
                callsign = "LH456",
                position = new Position { lat = lh456_lat, lon = lh456_lon, alt = 37000 },
                velocity = new Velocity { speed = 480, heading = 170 },
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                category = "cat021"
            };

            // Add to queue
            messageQueue.Enqueue(nzAircraft);
            messageQueue.Enqueue(antarcticaAircraft);
            messageQueue.Enqueue(berlinAircraft);
            messagesReceived += 3;
            
            aircraftCreated = true;
            Debug.Log("WorkingKafkaConsumer: Created ANZ123 (Christchurch), ICE001 (McMurdo)");
        }
        else
        {
            Debug.Log("WorkingKafkaConsumer: Updating aircraft with realistic flight paths...");
            
            // ANZ123: Flying east from Christchurch at realistic speed
            // 420 knots = ~0.12° longitude per update (5 seconds)
            anz123_lon += 0.003; // Slow eastward movement
            if (anz123_lon > 175.0) anz123_lon = 172.547780; // Reset to start
            
            var nzUpdate = new FIMSAircraftData
            {
                aircraft_id = "ANZ123",
                callsign = "ANZ123",
                position = new Position { lat = anz123_lat, lon = anz123_lon, alt = 5000 },
                velocity = new Velocity { speed = 420, heading = 90 },
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                category = "cat021"
            };

            // ICE001: Flying west over Antarctica at realistic speed  
            // 200 knots = ~0.06° longitude per update (5 seconds)
            ice001_lon -= 0.002; // Slow westward movement
            if (ice001_lon < 164.0) ice001_lon = 166.6863; // Reset to start
            
            var antarcticaUpdate = new FIMSAircraftData
            {
                aircraft_id = "ICE001",
                callsign = "ICE001",
                position = new Position { lat = ice001_lat, lon = ice001_lon, alt = 15000 },
                velocity = new Velocity { speed = 200, heading = 270 },
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                category = "cat048"
            };

            // LH456: Flying south over Europe
            lh456_lat -= 0.002; // Moving south
            if (lh456_lat < 50.0) lh456_lat = 52.5200; // Reset to Berlin
            
            var berlinUpdate = new FIMSAircraftData
            {
                aircraft_id = "LH456",
                callsign = "LH456",
                position = new Position { lat = lh456_lat, lon = lh456_lon, alt = 37000 },
                velocity = new Velocity { speed = 480, heading = 170 },
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                category = "cat021"
            };

            messageQueue.Enqueue(nzUpdate);
            messageQueue.Enqueue(antarcticaUpdate);
            messageQueue.Enqueue(berlinUpdate);
            messagesReceived += 3;
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
            
            Debug.Log($"WorkingKafkaConsumer: Processing aircraft {aircraftData.callsign} - Event listeners: {OnAircraftDataReceived?.GetInvocationList()?.Length ?? 0}");
            
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
        aircraftCreated = false;
        messagesReceived = 0;
        messagesProcessed = 0;
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