using UnityEngine;

[CreateAssetMenu(fileName = "ASTERIXConfig", menuName = "Airways/ASTERIX Configuration")]
public class ASTERIXConfiguration : ScriptableObject
{
    [Header("Kafka Settings")]
    public string kafkaBootstrapServers = "localhost:9092";
    public string consumerGroupId = "unity-asterix-consumer";
    
    [Header("ASTERIX Topics")]
    public string cat021Topic = "surveillance.asterix.cat021"; // ADS-B data
    public string cat048Topic = "surveillance.asterix.cat048"; // Radar target reports
    public string cat062Topic = "surveillance.asterix.cat062"; // System tracks
    public string cat034Topic = "surveillance.asterix.cat034"; // Service messages
    
    [Header("Connection Settings")]
    public int sessionTimeoutMs = 10000;
    public int heartbeatIntervalMs = 3000;
    public bool autoOffsetReset = true; // Start from latest messages
    
    [Header("Performance Settings")]
    public int maxMessagesPerFrame = 10;
    public float aircraftTimeoutSeconds = 30f;
    public int maxAircraftLimit = 50;
    
    [Header("Debug Settings")]
    public bool enableDebugLogging = true;
    public bool logKafkaMessages = false;
    public bool showConnectionStatus = true;
    
    [Header("Test Data")]
    public bool useSimulatedData = true;
    public float simulatedUpdateInterval = 5f;
    
    // Expected aircraft from your ASTERIX data
    [Header("Expected Aircraft (from ASTERIX files)")]
    [TextArea(3, 10)]
    public string expectedAircraft = @"DLH65A - Lufthansa flight at FL330
Updates every 2 seconds via CAT021 (ADS-B)
Position updates from radar surveillance";
    
    public string[] GetAllTopics()
    {
        return new string[] { cat021Topic, cat048Topic, cat062Topic, cat034Topic };
    }
}