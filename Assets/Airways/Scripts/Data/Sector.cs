using UnityEngine;
using System.Collections.Generic;

namespace Airways.Data
{
    [System.Serializable]
    public class Sector
    {
        public int id;
        public string sectorName;
        public string type;
        public string displayName;
        public string lowerLimit;
        public string lowerUOM;
        public float lowerFilter;
        public string upperLimit;
        public string upperUOM;
        public float upperFilter;
        public List<List<List<List<double>>>> coordinates;

        public bool IsAltitudeInRange(float altitude)
        {
            // Convert altitude to feet if it's in FL (Flight Level)
            float altitudeFeet = altitude;
            if (upperUOM == "FL")
            {
                altitudeFeet = altitude * 100; // Convert FL to feet
            }

            return altitudeFeet >= lowerFilter && altitudeFeet <= upperFilter;
        }
    }

    [System.Serializable]
    public class SectorProperties
    {
        public int id;
        public string sector;
        public string type;
        public string name;
        public string lower;
        public string lower_uom;
        public float lowerfilter;
        public string upper;
        public string upper_uom;
        public float upperfilter;
        public string displayname;
    }

    [System.Serializable]
    public class SectorGeometry
    {
        public string type;
        public List<List<List<List<double>>>> coordinates;
    }

    [System.Serializable]
    public class SectorFeature
    {
        public string type;
        public SectorProperties properties;
        public SectorGeometry geometry;
    }

    [System.Serializable]
    public class SectorCollection
    {
        public string type;
        public string name;
        public Dictionary<string, object> crs;
        public List<SectorFeature> features;
    }
}
