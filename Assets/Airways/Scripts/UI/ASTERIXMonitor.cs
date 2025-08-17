using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;

public class ASTERIXMonitor : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI statusText;
    public Button toggleDataModeButton;
    public Button restartKafkaButton;
    public Button clearAircraftButton;
    
    [Header("Settings")]
    public bool autoUpdate = true;
    public float updateInterval = 1f;

    private AircraftManager aircraftManager;
    private WorkingKafkaConsumer kafkaConsumer;
    private StringBuilder statusBuilder = new StringBuilder();

    void Start()
    {
        // Find components
        aircraftManager = FindObjectOfType<AircraftManager>();
        kafkaConsumer = FindObjectOfType<WorkingKafkaConsumer>();

        // Setup button events
        if (toggleDataModeButton != null)
        {
            toggleDataModeButton.onClick.AddListener(ToggleDataMode);
        }

        if (restartKafkaButton != null)
        {
            restartKafkaButton.onClick.AddListener(RestartKafka);
        }

        if (clearAircraftButton != null)
        {
            clearAircraftButton.onClick.AddListener(ClearAllAircraft);
        }

        // Start auto-update
        if (autoUpdate)
        {
            InvokeRepeating(nameof(UpdateStatus), 1f, updateInterval);
        }
    }

    void UpdateStatus()
    {
        if (statusText == null) return;

        statusBuilder.Clear();
        statusBuilder.AppendLine("=== ASTERIX SURVEILLANCE STATUS ===");
        statusBuilder.AppendLine();

        // Pipeline Status
        statusBuilder.AppendLine("📡 PIPELINE STATUS:");
        statusBuilder.AppendLine($"• ASTERIX Publisher: {GetPublisherStatus()}");
        statusBuilder.AppendLine($"• Python Parser: {GetParserStatus()}");
        statusBuilder.AppendLine($"• FIMS Service: {GetFIMSStatus()}");
        statusBuilder.AppendLine($"• Kafka Consumer: {GetKafkaStatus()}");
        statusBuilder.AppendLine();

        // Aircraft Manager Status
        if (aircraftManager != null)
        {
            statusBuilder.AppendLine("✈️ AIRCRAFT MANAGER:");
            statusBuilder.AppendLine($"• Data Mode: {(aircraftManager.useSimulatedData ? "SIMULATED" : "REAL (Kafka)")}");
            statusBuilder.AppendLine($"• Active Aircraft: {aircraftManager.GetActiveAircraft().Count}");
            statusBuilder.AppendLine($"• Kafka Messages: {aircraftManager.GetKafkaMessagesReceived()}");
            statusBuilder.AppendLine($"• Max Aircraft: {aircraftManager.maxAircraft}");
            statusBuilder.AppendLine();
        }

        // Kafka Consumer Details
        if (kafkaConsumer != null)
        {
            statusBuilder.AppendLine("🔌 KAFKA CONSUMER:");
            statusBuilder.AppendLine($"• Connected: {kafkaConsumer.IsConnected()}");
            statusBuilder.AppendLine($"• Queue Size: {kafkaConsumer.GetQueueSize()}");
            statusBuilder.AppendLine($"• Messages Received: {kafkaConsumer.GetMessagesReceived()}");
            statusBuilder.AppendLine($"• Messages Processed: {kafkaConsumer.GetMessagesProcessed()}");
            statusBuilder.AppendLine($"• Bootstrap Servers: {kafkaConsumer.bootstrapServers}");
            statusBuilder.AppendLine();
        }

        // Active Aircraft List
        if (aircraftManager != null && aircraftManager.GetActiveAircraft().Count > 0)
        {
            statusBuilder.AppendLine("🛩️ ACTIVE AIRCRAFT:");
            foreach (var kvp in aircraftManager.GetActiveAircraft())
            {
                var aircraft = kvp.Value;
                if (aircraft != null)
                {
                    statusBuilder.AppendLine($"• {aircraft.callsign} ({kvp.Key})");
                    statusBuilder.AppendLine($"  Alt: {aircraft.altitude:F0}ft, Speed: {aircraft.groundSpeed:F0}kts");
                    statusBuilder.AppendLine($"  Heading: {aircraft.heading:F0}°");
                }
            }
            statusBuilder.AppendLine();
        }

        // Expected Data Info
        statusBuilder.AppendLine("📋 EXPECTED ASTERIX DATA:");
        statusBuilder.AppendLine("• DLH65A (Lufthansa) at FL330");
        statusBuilder.AppendLine("• Updates every 2 seconds");
        statusBuilder.AppendLine("• Topics: cat021, cat048, cat062, cat034");
        statusBuilder.AppendLine();

        // Setup Instructions
        if (aircraftManager != null && !aircraftManager.useSimulatedData && 
            (kafkaConsumer == null || !kafkaConsumer.IsConnected()))
        {
            statusBuilder.AppendLine("⚠️ SETUP REQUIRED:");
            statusBuilder.AppendLine("1. Run: make start-kafka");
            statusBuilder.AppendLine("2. Run: make create-asterix-topics");
            statusBuilder.AppendLine("3. Start: make start-asterix-parser");
            statusBuilder.AppendLine("4. Start: make start-fims-asterix");
            statusBuilder.AppendLine("5. Run: .\\bin\\asterix-publisher.exe");
        }

        statusText.text = statusBuilder.ToString();
    }

    private string GetPublisherStatus()
    {
        // You could ping UDP port or check process
        return "Check manually (UDP 239.255.0.1:10000)";
    }

    private string GetParserStatus()
    {
        // You could check if Python process is running
        return "Check manually (Port 10001)";
    }

    private string GetFIMSStatus()
    {
        // You could check if FIMS service is running
        return "Check manually (Kafka Producer)";
    }

    private string GetKafkaStatus()
    {
        if (kafkaConsumer == null)
            return "❌ NO CONSUMER";
        
        if (!kafkaConsumer.IsConnected())
            return "❌ DISCONNECTED";
        
        return $"✅ CONNECTED ({kafkaConsumer.GetQueueSize()} queued)";
    }

    // Button event handlers
    public void ToggleDataMode()
    {
        if (aircraftManager != null)
        {
            aircraftManager.SetSimulatedData(!aircraftManager.useSimulatedData);
            Debug.Log($"Toggled to {(aircraftManager.useSimulatedData ? "simulated" : "real")} data mode");
        }
    }

    public void RestartKafka()
    {
        if (kafkaConsumer != null)
        {
            kafkaConsumer.RestartConsumer();
            Debug.Log("Restarted Kafka consumer");
        }
    }

    public void ClearAllAircraft()
    {
        if (aircraftManager != null)
        {
            aircraftManager.ClearAllAircraft();
            Debug.Log("Cleared all aircraft");
        }
    }

    // Manual update for testing
    public void RefreshStatus()
    {
        UpdateStatus();
    }

    void OnDestroy()
    {
        CancelInvoke();
    }
}