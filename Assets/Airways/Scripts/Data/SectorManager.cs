using UnityEngine;
using System.Collections.Generic;
using CesiumForUnity;
using Newtonsoft.Json;
using Airways.Data;

namespace Airways.Data
{
    public class ImprovedSectorManager : MonoBehaviour
    {
        [SerializeField]
        private TextAsset sectorGeoJson;

        [SerializeField]
        private Material lineMaterial;

        [Header("Line Settings")]
        public float baseLineWidth = 50f;  // Base width at surface level
        public float lineWidth = 250f;     // Current calculated width (for display)
        public float altitudeScaling = 0.1f; // How much wider per meter of altitude
        public float heightAboveTerrain = 500f; // Height above ground (instead of lineHeight)
        public bool followTerrain = true;  // Follow terrain elevation
        public bool showAllSectors = true;
        public float currentAltitude = 5000f;

        [Header("Terrain Sampling")]
        public LayerMask terrainLayerMask = -1; // Layer mask for terrain detection
        public float maxRaycastDistance = 10000f; // Max distance to check for terrain

        [Header("Debug")]
        [SerializeField] private int sectorsCreated = 0;
        [SerializeField] private List<string> sectorNames = new List<string>();

        private List<GameObject> sectorBoundaries = new List<GameObject>();
        private CesiumForUnity.CesiumGeoreference georeference;
        private Transform georeferenceTransform;

        // Structure to hold sector boundary data
        private class SectorBoundary
        {
            public GameObject containerObject;
            public List<GameObject> anchorPoints = new List<GameObject>();
            public List<LineRenderer> boundaryLines = new List<LineRenderer>();
            public string sectorName;
        }

        void OnValidate()
        {
            // Automatically recalculate line width when values change in inspector
            if (Application.isPlaying)
            {
                CalculateLineWidth();
            }
        }

        private List<SectorBoundary> activeBoundaries = new List<SectorBoundary>();

        private void Start()
        {
            // Find georeference - SAME AS AIRCRAFT CODE
            georeference = FindObjectOfType<CesiumForUnity.CesiumGeoreference>();
            if (georeference == null)
            {
                Debug.LogError("CesiumGeoreference not found! Sector boundaries won't work correctly.");
                return;
            }
            else
            {
                georeferenceTransform = georeference.transform;
                Debug.Log($"ImprovedSectorManager: Found CesiumGeoreference: {georeference.name}");
            }

            // Calculate line width based on altitude
            CalculateLineWidth();

            if (sectorGeoJson != null)
            {
                CreateSectorBoundaries();
            }
            else
            {
                Debug.LogError("Sector GeoJSON file not assigned!");
            }
        }

        private void CalculateLineWidth()
        {
            // Scale line width based on altitude: higher = thicker lines for visibility
            float effectiveHeight = followTerrain ? heightAboveTerrain : heightAboveTerrain;
            lineWidth = baseLineWidth + (effectiveHeight * altitudeScaling);
            Debug.Log($"Calculated line width: {lineWidth} (base: {baseLineWidth}, height: {effectiveHeight}, scaling: {altitudeScaling})");
        }

        private float SampleTerrainHeight(double longitude, double latitude)
        {
            if (!followTerrain)
                return 0f; // Use sea level if not following terrain

            // Convert lat/lon to a world position for raycasting
            // Create a temporary anchor high above to get world position
            GameObject tempAnchor = new GameObject("TempHeightSampler");
            tempAnchor.transform.SetParent(georeferenceTransform);

            CesiumGlobeAnchor tempAnchorComponent = tempAnchor.AddComponent<CesiumGlobeAnchor>();
            tempAnchorComponent.longitudeLatitudeHeight = new Unity.Mathematics.double3(longitude, latitude, 5000); // High altitude for sampling

            // Wait a frame for positioning (in a real implementation, you'd want to make this async)
            // For now, we'll use the position immediately and raycast down
            Vector3 worldPos = tempAnchor.transform.position;

            // Raycast downward to find terrain
            RaycastHit hit;
            Vector3 rayStart = worldPos + Vector3.up * 2000f; // Start well above
            Vector3 rayDirection = Vector3.down;

            float terrainHeight = 0f;
            if (Physics.Raycast(rayStart, rayDirection, out hit, maxRaycastDistance, terrainLayerMask))
            {
                // Get the local height relative to the anchor position
                Vector3 hitLocalPos = tempAnchorComponent.transform.InverseTransformPoint(hit.point);
                terrainHeight = hitLocalPos.y;
            }
            else
            {
                // Fallback: try sampling using Cesium tileset if available
                var tilesets = FindObjectsOfType<CesiumForUnity.Cesium3DTileset>();
                if (tilesets.Length > 0)
                {
                    // Use first tileset for height sampling (assuming it's terrain)
                    // This is a simplified approach - in practice you'd want async height sampling
                    terrainHeight = 0f; // Fallback to sea level
                }
            }

            // Clean up temporary object
            if (Application.isPlaying)
                Destroy(tempAnchor);
            else
                DestroyImmediate(tempAnchor);

            return terrainHeight;
        }

        private void CreateSectorBoundaries()
        {
            try
            {
                Debug.Log("Starting to parse GeoJSON...");

                // Parse JSON manually to handle the crs field issue
                var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(sectorGeoJson.text);

                if (!jsonObject.ContainsKey("features"))
                {
                    Debug.LogError("GeoJSON missing 'features' field");
                    return;
                }

                // Parse features array
                var featuresJson = JsonConvert.SerializeObject(jsonObject["features"]);
                var features = JsonConvert.DeserializeObject<List<SectorFeature>>(featuresJson);

                if (features == null)
                {
                    Debug.LogError("Failed to parse features array");
                    return;
                }

                Debug.Log($"Parsed {features.Count} features from GeoJSON");

                int processedCount = 0;
                foreach (var feature in features)
                {
                    Debug.Log($"Processing feature {processedCount}: {feature?.properties?.displayname}");

                    if (feature?.geometry?.coordinates == null || feature.properties == null)
                    {
                        Debug.LogWarning($"Skipping feature {processedCount} - missing geometry or properties");
                        continue;
                    }

                    string sectorName = feature.properties.displayname ?? "Unknown";
                    Debug.Log($"Processing sector: {sectorName}");

                    // Skip if not in altitude range (if filtering is enabled)
                    if (!showAllSectors)
                    {
                        Sector sector = ConvertFeatureToSector(feature);
                        if (!sector.IsAltitudeInRange(currentAltitude))
                        {
                            Debug.Log($"Skipping sector {sectorName} - not in altitude range ({currentAltitude} not between {sector.lowerFilter} and {sector.upperFilter})");
                            continue;
                        }
                    }

                    Debug.Log($"Creating boundaries for {sectorName} - has {feature.geometry.coordinates.Count} polygons");

                    // Handle MultiPolygon coordinates
                    for (int polygonIndex = 0; polygonIndex < feature.geometry.coordinates.Count; polygonIndex++)
                    {
                        var polygon = feature.geometry.coordinates[polygonIndex];
                        if (polygon.Count == 0)
                        {
                            Debug.LogWarning($"Polygon {polygonIndex} in {sectorName} is empty");
                            continue;
                        }

                        // Get outer ring (first element)
                        var ring = polygon[0];
                        if (ring.Count < 3)
                        {
                            Debug.LogWarning($"Ring in polygon {polygonIndex} of {sectorName} has less than 3 points");
                            continue;
                        }

                        Debug.Log($"Creating boundary for {sectorName}_{polygonIndex} with {ring.Count} points");
                        CreateGlobeAnchoredSectorBoundary(ring, $"{sectorName}_{polygonIndex}", feature.properties);
                    }
                    processedCount++;
                }

                UpdateDebugInfo();
                Debug.Log($"ImprovedSectorManager: Successfully created {sectorsCreated} sector boundaries from {processedCount} processed features");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error parsing sector JSON: {ex.Message}");
                Debug.LogError($"Stack trace: {ex.StackTrace}");
            }
        }

        private void CreateGlobeAnchoredSectorBoundary(List<List<double>> ring, string sectorName, SectorProperties properties)
        {
            // Create container object - SAME PATTERN AS OUR SUCCESSFUL LINE
            GameObject boundaryContainer = new GameObject($"SectorBoundary_{sectorName}");

            // CRITICAL: Parent to CesiumGeoreference - SAME AS AIRCRAFT
            if (georeferenceTransform != null)
            {
                boundaryContainer.transform.SetParent(georeferenceTransform);
                Debug.Log($"Sector boundary {sectorName} parented to CesiumGeoreference");
            }
            else
            {
                Debug.LogError($"Cannot parent sector boundary {sectorName} - CesiumGeoreference transform is null!");
            }

            SectorBoundary boundary = new SectorBoundary
            {
                containerObject = boundaryContainer,
                sectorName = sectorName
            };

            // Create anchor points for each coordinate with terrain following
            for (int i = 0; i < ring.Count; i++)
            {
                double lon = ring[i][0];
                double lat = ring[i][1];

                // Sample terrain height at this coordinate
                float terrainHeight = SampleTerrainHeight(lon, lat);
                float finalHeight = followTerrain ? terrainHeight + heightAboveTerrain : heightAboveTerrain;

                // Create anchor point
                GameObject anchorPoint = new GameObject($"Anchor_{i}");
                anchorPoint.transform.SetParent(boundaryContainer.transform);

                // Add CesiumGlobeAnchor with terrain-adjusted height
                CesiumGlobeAnchor anchor = anchorPoint.AddComponent<CesiumGlobeAnchor>();
                anchor.longitudeLatitudeHeight = new Unity.Mathematics.double3(lon, lat, finalHeight);

                boundary.anchorPoints.Add(anchorPoint);

                if (followTerrain && i % 10 == 0) // Log every 10th point to avoid spam
                {
                    Debug.Log($"Point {i}: Terrain height {terrainHeight}m, Final height {finalHeight}m");
                }
            }

            // Create connecting lines between adjacent anchor points (SAME AS OUR SUCCESSFUL APPROACH)
            for (int i = 0; i < boundary.anchorPoints.Count; i++)
            {
                int nextIndex = (i + 1) % boundary.anchorPoints.Count; // Wrap around to close the boundary

                GameObject lineObject = new GameObject($"BoundaryLine_{i}");
                lineObject.transform.SetParent(boundaryContainer.transform);

                LineRenderer line = lineObject.AddComponent<LineRenderer>();
                line.material = lineMaterial != null ? lineMaterial : new Material(Shader.Find("Sprites/Default"));
                line.startWidth = lineWidth;
                line.endWidth = lineWidth;
                line.positionCount = 2;
                line.useWorldSpace = true;

                if (lineMaterial == null)
                {
                    line.startColor = followTerrain ? Color.green : Color.yellow; // Green when following terrain
                    line.endColor = followTerrain ? Color.green : Color.yellow;
                }

                boundary.boundaryLines.Add(line);
            }

            activeBoundaries.Add(boundary);
            sectorBoundaries.Add(boundaryContainer);
            sectorsCreated++;

            Debug.Log($"Created terrain-following sector boundary: {sectorName} with {ring.Count} anchor points (terrain following: {followTerrain})");
        }

        // UPDATE METHOD - SAME PATTERN AS OUR SUCCESSFUL LINE
        void Update()
        {
            // Update boundary line positions based on anchor positions
            foreach (var boundary in activeBoundaries)
            {
                if (boundary.anchorPoints == null || boundary.boundaryLines == null) continue;

                for (int i = 0; i < boundary.boundaryLines.Count; i++)
                {
                    if (i >= boundary.anchorPoints.Count) continue;

                    int nextIndex = (i + 1) % boundary.anchorPoints.Count;

                    if (boundary.anchorPoints[i] != null &&
                        boundary.anchorPoints[nextIndex] != null &&
                        boundary.boundaryLines[i] != null)
                    {
                        boundary.boundaryLines[i].SetPosition(0, boundary.anchorPoints[i].transform.position);
                        boundary.boundaryLines[i].SetPosition(1, boundary.anchorPoints[nextIndex].transform.position);
                    }
                }
            }
        }

        private Sector ConvertFeatureToSector(SectorFeature feature)
        {
            return new Sector
            {
                id = feature.properties.id,
                sectorName = feature.properties.sector,
                type = feature.properties.type,
                displayName = feature.properties.displayname,
                lowerLimit = feature.properties.lower,
                lowerUOM = feature.properties.lower_uom,
                lowerFilter = feature.properties.lowerfilter,
                upperLimit = feature.properties.upper,
                upperUOM = feature.properties.upper_uom,
                upperFilter = feature.properties.upperfilter,
                coordinates = feature.geometry.coordinates
            };
        }

        private void UpdateDebugInfo()
        {
            sectorsCreated = activeBoundaries.Count;
            sectorNames.Clear();
            foreach (var boundary in activeBoundaries)
            {
                if (boundary.containerObject != null)
                    sectorNames.Add(boundary.sectorName);
            }
        }

        [ContextMenu("Clear Boundaries")]
        public void ClearBoundaries()
        {
            foreach (var boundary in activeBoundaries)
            {
                if (boundary.containerObject != null)
                {
                    if (Application.isPlaying)
                        Destroy(boundary.containerObject);
                    else
                        DestroyImmediate(boundary.containerObject);
                }
            }
            activeBoundaries.Clear();
            sectorBoundaries.Clear();
            sectorsCreated = 0;
            sectorNames.Clear();
            Debug.Log("Cleared all sector boundaries");
        }

        [ContextMenu("Recreate Boundaries")]
        public void RecreateBoundaries()
        {
            ClearBoundaries();
            if (sectorGeoJson != null)
            {
                CreateSectorBoundaries();
            }
        }

        // Public methods for runtime control
        public void SetAltitudeFilter(float altitude)
        {
            currentAltitude = altitude;
            if (!showAllSectors)
            {
                RecreateBoundaries(); // Rebuild with new altitude filter
            }
        }

        public void ToggleShowAllSectors(bool showAll)
        {
            showAllSectors = showAll;
            RecreateBoundaries();
        }

        public void SetLineHeight(float height)
        {
            heightAboveTerrain = height;
            CalculateLineWidth(); // Recalculate width based on new height
            RecreateBoundaries();
        }

        public void SetAltitudeScaling(float scaling)
        {
            altitudeScaling = scaling;
            CalculateLineWidth(); // Recalculate width based on new scaling
            RecreateBoundaries();
        }

        public void ToggleTerrainFollowing(bool follow)
        {
            followTerrain = follow;
            CalculateLineWidth();
            RecreateBoundaries();
            Debug.Log($"Terrain following {(follow ? "enabled" : "disabled")}");
        }

        // Get sectors that contain a specific altitude
        public List<string> GetSectorsAtAltitude(float altitude)
        {
            List<string> activeSectors = new List<string>();

            if (sectorGeoJson == null) return activeSectors;

            try
            {
                // Use the same manual parsing approach
                var jsonObject = JsonConvert.DeserializeObject<Dictionary<string, object>>(sectorGeoJson.text);

                if (!jsonObject.ContainsKey("features")) return activeSectors;

                var featuresJson = JsonConvert.SerializeObject(jsonObject["features"]);
                var features = JsonConvert.DeserializeObject<List<SectorFeature>>(featuresJson);

                foreach (var feature in features)
                {
                    Sector sector = ConvertFeatureToSector(feature);
                    if (sector.IsAltitudeInRange(altitude))
                    {
                        activeSectors.Add(sector.displayName);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error getting sectors at altitude: {ex.Message}");
            }

            return activeSectors;
        }
    }
}