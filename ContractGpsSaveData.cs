using System;
using System.Collections.Generic;
using System.Linq;

namespace ContractGpsFix
{
    [Serializable]
    public class ContractGpsSaveData
    {
        public List<ContractGpsRef> Preferences = new List<ContractGpsRef>();
        public List<PlayerGpsState> PlayerStates = new List<PlayerGpsState>();

        public ContractGpsRef FindRefByGps(long identityId, string key)
        {
            return Preferences.FirstOrDefault(pref =>
                pref.IdentityId == identityId &&
                pref.Key == key);
        }
        
        public PlayerGpsState GetOrCreatePlayerState(long identityId)
        {
            var state = PlayerStates.FirstOrDefault(x => x.IdentityId == identityId);
            if (state != null)
                return state;

            state = new PlayerGpsState
            {
                IdentityId = identityId,
                CurrentCharacterId = 'A'
            };

            PlayerStates.Add(state);
            return state;
        }
        
        public void CleanupEmptyPlayerStates()
        {
            for (var i = PlayerStates.Count - 1; i >= 0; i--)
            {
                var identityId = PlayerStates[i].IdentityId;

                if (Preferences.All(p => p.IdentityId != identityId))
                    PlayerStates.RemoveAt(i);
            }
        }
    }
}