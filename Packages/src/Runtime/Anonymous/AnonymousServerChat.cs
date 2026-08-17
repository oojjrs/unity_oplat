using System;
using System.Collections.Generic;

namespace oojjrs.oplat.anonymous
{
    internal static class AnonymousServerChat
    {
        internal sealed record MessageData
        {
            public string Message { get; set; }
            public string PlayerId { get; set; }
            public string RoomId { get; set; }
        }

        internal sealed class State
        {
            private readonly Dictionary<string, HashSet<string>> PlayersByRoomId = new();

            internal bool Contains(string playerId, string roomId)
            {
                return PlayersByRoomId.TryGetValue(roomId, out var playerIds) && playerIds.Contains(playerId);
            }

            internal IEnumerable<string> GetPlayers(string roomId)
            {
                if (PlayersByRoomId.TryGetValue(roomId, out var playerIds))
                    return playerIds;

                return Array.Empty<string>();
            }

            internal void Join(string playerId, string roomId)
            {
                if (PlayersByRoomId.TryGetValue(roomId, out var playerIds) == false)
                {
                    playerIds = new HashSet<string>();
                    PlayersByRoomId.Add(roomId, playerIds);
                }

                playerIds.Add(playerId);
            }

            internal void Remove(string playerId)
            {
                foreach (var playerIds in PlayersByRoomId.Values)
                    playerIds.Remove(playerId);

                var emptyRoomIds = new List<string>();
                foreach (var entry in PlayersByRoomId)
                {
                    if (entry.Value.Count == 0)
                        emptyRoomIds.Add(entry.Key);
                }

                foreach (var roomId in emptyRoomIds)
                    PlayersByRoomId.Remove(roomId);
            }

            internal void Remove(string playerId, string roomId)
            {
                if (PlayersByRoomId.TryGetValue(roomId, out var playerIds) == false)
                    return;

                playerIds.Remove(playerId);
                if (playerIds.Count == 0)
                    PlayersByRoomId.Remove(roomId);
            }
        }
    }
}
