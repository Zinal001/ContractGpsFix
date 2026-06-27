using System;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace ContractGpsFix
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class ContractGpsFix : MySessionComponentBase
    {
        private readonly List<IMyGps> _GpsList = new List<IMyGps>();
        private readonly List<IMyPlayer> _MpPlayers = new List<IMyPlayer>();
        private int _TicksCounter;
        private readonly Dictionary<long, int> _PlayerLoadCounters = new Dictionary<long, int>();
        
        private ContractGpsSaveData _SaveList = new ContractGpsSaveData();
        private ContractGpsConfig _Config = new ContractGpsConfig();
        
        private bool IsServer()
        {
            return MyAPIGateway.Multiplayer == null || MyAPIGateway.Multiplayer.IsServer;
        }

        public override void LoadData()
        {
            if (IsServer())
            {
                LoadConfigData();
                LoadGpsReferences();
            }

            if (!MyAPIGateway.Utilities.IsDedicated)
                MyAPIGateway.Utilities.MessageEntered += OnMessageEntered;

            if (MyAPIGateway.Multiplayer == null)
                return;
            
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(
                Constants.NetworkCommandId,
                OnNetworkCommandReceived);

            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(
                Constants.NetworkResponseId,
                OnNetworkResponseReceived);
        }

        protected override void UnloadData()
        {
            if (!MyAPIGateway.Utilities.IsDedicated)
                MyAPIGateway.Utilities.MessageEntered -= OnMessageEntered;

            if (MyAPIGateway.Multiplayer == null)
                return;
            
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(
                Constants.NetworkCommandId,
                OnNetworkCommandReceived);

            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(
                Constants.NetworkResponseId,
                OnNetworkResponseReceived);
        }

        public override void SaveData()
        {
            if (IsServer())
                SaveGpsReferences();
        }

        public override void UpdateAfterSimulation()
        {
            if (++_TicksCounter < 300)
                return;

            _TicksCounter = 0;
            
            var isServer = MyAPIGateway.Multiplayer == null || MyAPIGateway.Multiplayer.IsServer;
            var isDedicated = MyAPIGateway.Utilities.IsDedicated;

            if (!isServer)
                return;

            if (isDedicated)
            {
                _MpPlayers.Clear();
                // ReSharper disable PossibleNullReferenceException
                MyAPIGateway.Multiplayer.Players.GetPlayers(_MpPlayers);
                // ReSharper restore PossibleNullReferenceException
                foreach(var player in _MpPlayers)
                    UpdateGpsMarkers(player.IdentityId);
            }
            else
            {
                var player = MyAPIGateway.Session?.Player;
                if (player == null)
                    return;
                
                UpdateGpsMarkers(player.IdentityId);
            }

            _SaveList.CleanupEmptyPlayerStates();
        }

        private void SaveGpsReferences()
        {
            try
            {
                var xml = MyAPIGateway.Utilities.SerializeToXML(_SaveList);

                using (var writer =
                       MyAPIGateway.Utilities.WriteFileInWorldStorage(Constants.SaveFileName, typeof(ContractGpsSaveData)))
                {
                    writer.Write(xml);
                }
            }
            catch(Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage(
                    "Contract GPS Fix",
                    "Failed to save GPS preferences: " + e.Message);
            }
        }

        private void LoadGpsReferences()
        {
            try
            {
                if (!MyAPIGateway.Utilities.FileExistsInWorldStorage(Constants.SaveFileName, typeof(ContractGpsSaveData)))
                {
                    _SaveList = new ContractGpsSaveData();
                    return;
                }

                using (var reader =
                       MyAPIGateway.Utilities.ReadFileInWorldStorage(Constants.SaveFileName, typeof(ContractGpsSaveData)))
                {
                    var xml = reader.ReadToEnd();
                
                    _SaveList = MyAPIGateway.Utilities.SerializeFromXML<ContractGpsSaveData>(xml) ?? new ContractGpsSaveData();

                    foreach (var pref in _SaveList.Preferences)
                        pref.MissingScans = 0;
                }
            }
            catch (Exception e)
            {
                _SaveList = new ContractGpsSaveData();

                MyAPIGateway.Utilities.ShowMessage(
                    "Contract GPS Fix",
                    "Failed to load GPS preferences: " + e.Message);
            }
        }

        private void LoadConfigData()
        {
            try
            {
                if (!MyAPIGateway.Utilities.FileExistsInLocalStorage(Constants.ConfigSaveFileName, typeof(ContractGpsConfig)))
                {
                    SaveConfigData();
                    return;
                }

                using (var reader =
                       MyAPIGateway.Utilities.ReadFileInLocalStorage(Constants.ConfigSaveFileName,
                           typeof(ContractGpsConfig)))
                {
                    var xml = reader.ReadToEnd();
                    _Config = MyAPIGateway.Utilities.SerializeFromXML<ContractGpsConfig>(xml) ?? new ContractGpsConfig();
                }
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage(
                    "Contract GPS Fix",
                    "Failed to load config: " + e.Message);
            }
        }

        private void SaveConfigData()
        {
            try
            {
                string xml = MyAPIGateway.Utilities.SerializeToXML(_Config);

                using (var writer =
                       MyAPIGateway.Utilities.WriteBinaryFileInLocalStorage(Constants.ConfigSaveFileName, typeof(ContractGpsConfig)))
                {
                    writer.Write(xml);
                }
            }
            catch(Exception e)
            {
                MyAPIGateway.Utilities.ShowMessage(
                    "Contract GPS Fix",
                    "Failed to save config: " + e.Message);
            }
        }

        private void UpdateGpsMarkers(long identityId)
        {
            bool useSaved = ShouldUseSavedFor(identityId);
            
            _GpsList.Clear();
            MyAPIGateway.Session.GPS.GetGpsList(identityId, _GpsList);

            var foundSourceKeys = new HashSet<string>();
            
            foreach (var gps in _GpsList)
            {
                if (!IsSalvageContractGps(gps))
                    continue;

                var markerKey = ContractGpsRef.GetMarkerName(identityId, gps);
                foundSourceKeys.Add(markerKey);

                var gpsRef = _SaveList.FindRefByGps(identityId, markerKey);

                if (gpsRef == null)
                {
                    CreateCustomGpsForSource(identityId, gps, markerKey);
                    continue;
                }

                gpsRef.MissingScans = 0;

                if (useSaved && gps.ShowOnHud && gpsRef.Hidden)
                {
                    MyAPIGateway.Session.GPS.SetShowOnHud(identityId, gps, false);
                }
                else if (!useSaved && gps.ShowOnHud != !gpsRef.Hidden)
                {
                    gpsRef.Hidden = !gps.ShowOnHud;
                }
            }
            
            CleanupOrphanedCustomGps(identityId, foundSourceKeys, useSaved);
        }

        private static bool IsSalvageContractGps(IMyGps gps)
        {
            return "Salvage Site".Equals(gps.Name, StringComparison.OrdinalIgnoreCase) &&
                   (gps.Description ?? "").IndexOf("Retrieve the datapad from its cargo", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        
        private void CreateCustomGpsForSource(long identityId, IMyGps sourceGps, string markerKey)
        {
            var playerState = _SaveList.GetOrCreatePlayerState(identityId);

            var customName = _Config.NamingScheme == NamingScheme.Letter
                ? $"Salvage Site {(char)playerState.CurrentCharacterId}"
                : $"Salvage Site {playerState.CurrentCharacterId - 'A' + 1}";

            var newGps = MyAPIGateway.Session.GPS.Create(
                customName,
                sourceGps.Description,
                sourceGps.Coords,
                true);

            newGps.GPSColor = ColorExtensions.FromHtml(_Config.MarkerColor) ?? Color.Orange;

            MyAPIGateway.Session.GPS.AddGps(identityId, newGps);

            _SaveList.Preferences.Add(new ContractGpsRef
            {
                IdentityId = identityId,
                CharacterId = playerState.CurrentCharacterId,
                Key = markerKey,
                OriginalName = sourceGps.Name,
                CustomName = customName,
                CustomGpsHash = newGps.Hash,
                X = sourceGps.Coords.X,
                Y = sourceGps.Coords.Y,
                Z = sourceGps.Coords.Z,
                Hidden = true,
                MissingScans = 0
            });

            playerState.CurrentCharacterId++;
            if (playerState.CurrentCharacterId > 'Z')
                playerState.CurrentCharacterId = 'A';

            if (sourceGps.ShowOnHud)
                MyAPIGateway.Session.GPS.SetShowOnHud(identityId, sourceGps, false);

            if (_Config.Verbose)
            {
                MyAPIGateway.Utilities.ShowMessage(
                    "Contract GPS Fix",
                    $"Added {customName}");
            }
        }
        
        private void CleanupOrphanedCustomGps(
            long identityId,
            HashSet<string> foundSourceKeys,
            bool useSaved)
        {
            if (useSaved)
                return;

            for (var i = _SaveList.Preferences.Count - 1; i >= 0; i--)
            {
                var gpsRef = _SaveList.Preferences[i];

                if (gpsRef.IdentityId != identityId)
                    continue;

                if (foundSourceKeys.Contains(gpsRef.Key))
                {
                    gpsRef.MissingScans = 0;
                    continue;
                }

                gpsRef.MissingScans++;

                if (gpsRef.MissingScans < _Config.MissingScansBeforeCleanup)
                    continue;

                RemoveCustomGps(identityId, gpsRef);

                _SaveList.Preferences.RemoveAt(i);
            }
        }
        
        private void RemoveCustomGps(long identityId, ContractGpsRef gpsRef)
        {
            _GpsList.Clear();
            MyAPIGateway.Session.GPS.GetGpsList(identityId, _GpsList);

            var customGps = _GpsList.FirstOrDefault(gps => gps.Hash == gpsRef.CustomGpsHash);

            if (customGps == null)
            {
                foreach (var gps in _GpsList
                             .Where(gps => string.Equals(gps.Name, gpsRef.CustomName, StringComparison.OrdinalIgnoreCase))
                             .Where(gps => !(DistanceSquaredToRef(gps, gpsRef) > 1.0)))
                {
                    customGps = gps;
                    break;
                }
            }

            if (customGps == null)
                return;

            MyAPIGateway.Session.GPS.RemoveGps(identityId, customGps);

            if (_Config.Verbose)
            {
                MyAPIGateway.Utilities.ShowMessage(
                    "Contract GPS Fix",
                    $"Removed marker {customGps.Name}");
            }
        }
        
        private static double DistanceSquaredToRef(IMyGps gps, ContractGpsRef gpsRef)
        {
            var dx = gps.Coords.X - gpsRef.X;
            var dy = gps.Coords.Y - gpsRef.Y;
            var dz = gps.Coords.Z - gpsRef.Z;

            return dx * dx + dy * dy + dz * dz;
        }

        private void OnMessageEntered(string messageText, ref bool sendToOthers)
        {
            if (string.IsNullOrWhiteSpace(messageText))
                return;

            var message = messageText.Trim();

            if (!message.StartsWith(_Config.CommandPrefix, StringComparison.OrdinalIgnoreCase))
                return;

            sendToOthers = false;

            var parts = message.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2 &&
                parts[1].Equals("reset", StringComparison.OrdinalIgnoreCase))
            {
                RequestResetCommand();
                return;
            }

            MyAPIGateway.Utilities.ShowMessage(
                "Contract GPS Fix",
                $"Commands: {_Config.CommandPrefix} reset");
        }
        
        private void RequestResetCommand()
        {
            if (IsServer())
            {
                var player = MyAPIGateway.Session?.Player;
                if (player == null)
                    return;

                ResetPlayerState(player.IdentityId);

                MyAPIGateway.Utilities.ShowMessage(
                    "Contract GPS Fix",
                    $"Salvage site letter reset to {(_Config.NamingScheme == NamingScheme.Letter ? "A" : "1")}.");

                return;
            }

            if (MyAPIGateway.Multiplayer == null)
                return;

            var payload = new[] { (byte)ClientCommand.Reset };

            MyAPIGateway.Multiplayer.SendMessageToServer(
                Constants.NetworkCommandId,
                payload);
        }
        
        private void OnNetworkCommandReceived(
            ushort channelId,
            byte[] data,
            ulong senderSteamId,
            bool isSenderServer)
        {
            if (!IsServer())
                return;

            if (data == null || data.Length < 1)
                return;

            var command = (ClientCommand)data[0];

            var identityId = MyAPIGateway.Multiplayer.Players.TryGetIdentityId(senderSteamId);

            if (identityId == 0)
            {
                SendResponseToClient(
                    senderSteamId,
                    ServerResponse.Error,
                    "Could not find your player identity.");

                return;
            }

            switch (command)
            {
                case ClientCommand.Reset:
                    ResetPlayerState(identityId);

                    SendResponseToClient(
                        senderSteamId,
                        ServerResponse.Info,
                        "Salvage site letter reset to A.");

                    break;
                default:
                    return;
            }
        }
        
        private void SendResponseToClient(
            ulong recipientSteamId,
            ServerResponse responseType,
            string message)
        {
            if (MyAPIGateway.Multiplayer == null)
                return;

            var messageBytes = System.Text.Encoding.UTF8.GetBytes(message ?? "");

            var payload = new byte[messageBytes.Length + 1];
            payload[0] = (byte)responseType;

            Array.Copy(messageBytes, 0, payload, 1, messageBytes.Length);

            MyAPIGateway.Multiplayer.SendMessageTo(
                Constants.NetworkResponseId,
                payload,
                recipientSteamId);
        }
        
        private void OnNetworkResponseReceived(
            ushort channelId,
            byte[] data,
            ulong senderSteamId,
            bool isSenderServer)
        {
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            if (data == null || data.Length < 1)
                return;

            var _ = (ServerResponse)data[0];

            var message = "";

            if (data.Length > 1)
            {
                message = System.Text.Encoding.UTF8.GetString(
                    data,
                    1,
                    data.Length - 1);
            }

            if (string.IsNullOrWhiteSpace(message))
                return;

            MyAPIGateway.Utilities.ShowMessage(
                "Contract GPS Fix",
                message);
        }
        
        private void ResetPlayerState(long identityId)
        {
            var state = _SaveList.GetOrCreatePlayerState(identityId);
            state.CurrentCharacterId = 'A';
        }
        
        private bool ShouldUseSavedFor(long identityId)
        {
            int counter;
            if (!_PlayerLoadCounters.TryGetValue(identityId, out counter))
            {
                _PlayerLoadCounters[identityId] = 0;
                return true;
            }

            if (counter > 10)
                return false;
            
            _PlayerLoadCounters[identityId] = counter + 1;
            return true;
        }
    }
}