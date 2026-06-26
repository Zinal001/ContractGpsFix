using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;

namespace ContractGpsFix
{
    [Serializable]
    public class ContractGpsRef
    {
        public long IdentityId;
        public string Key;

        public string OriginalName;
        public int CharacterId;
        
        public string CustomName;
        public int CustomGpsHash;

        public double X;
        public double Y;
        public double Z;

        public bool Hidden;
        public int MissingScans;

        public static string GetMarkerName(long identityId, IMyGps gps)
            => $"{identityId}_{Math.Round(gps.Coords.X)}_{Math.Round(gps.Coords.Y)}_{Math.Round(gps.Coords.Z)}_{gps.Name}";
    }
}