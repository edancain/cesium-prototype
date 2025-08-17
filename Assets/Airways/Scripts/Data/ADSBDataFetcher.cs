// ADSB API Handler
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class ADSBDataFetcher : MonoBehaviour
{
    [Header("API Settings")]
    public string apiUrl = "https://opensky-network.org/api/states/all";
    public AircraftManager aircraftManager;
    
    [System.Serializable]
    public class OpenSkyResponse
    {
        public float time;
        public string[][] states; // Array of aircraft state arrays
    }
    
    public void FetchADSBData()
    {
        StartCoroutine(GetADSBData());
    }
    
    IEnumerator GetADSBData()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                ParseADSBData(request.downloadHandler.text);
            }
            else
            {
                Debug.LogError("ADSB API Error: " + request.error);
            }
        }
    }
    
    void ParseADSBData(string jsonData)
    {
        OpenSkyResponse response = JsonUtility.FromJson<OpenSkyResponse>(jsonData);
        
        foreach (string[] state in response.states)
        {
            // OpenSky API format: [icao24, callsign, origin_country, time_position, 
            // time_velocity, longitude, latitude, baro_altitude, on_ground, velocity, 
            // true_track, vertical_rate, sensors, geo_altitude, squawk, sil, position_source]
            
            string icao24 = state[0];
            string callsign = state[1]?.Trim();
            float? longitude = state[5] != null ? float.Parse(state[5]) : null;
            float? latitude = state[6] != null ? float.Parse(state[6]) : null;
            float? altitude = state[7] != null ? float.Parse(state[7]) : null;
            float? heading = state[10] != null ? float.Parse(state[10]) : null;
            float? velocity = state[9] != null ? float.Parse(state[9]) : null;
            
            if (longitude.HasValue && latitude.HasValue && altitude.HasValue)
            {
                aircraftManager.UpdateOrCreateAircraft(
                    icao24, callsign ?? "Unknown",
                    longitude.Value, latitude.Value, altitude.Value,
                    heading ?? 0, velocity ?? 0
                );
            }
        }
    }
}