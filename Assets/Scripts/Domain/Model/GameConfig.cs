using System;
using System.Collections.Generic;

namespace Uckers.Domain.Model
{
    public static class GameConfig
    {
        public const int TokensPerPlayer = 4;
        public const int DefaultPlayerCount = 2;
        public const int MaxPlayers = 4;
        public const int MinPlayers = 2;

        public static readonly IReadOnlyList<PlayerId> PlayerOrder = new[]
        {
            PlayerId.Red,
            PlayerId.Blue,
            PlayerId.Green,
            PlayerId.Yellow
        };

        public static readonly IReadOnlyDictionary<PlayerId, (float r, float g, float b, float a)> PlayerColours =
            new Dictionary<PlayerId, (float r, float g, float b, float a)>
            {
                [PlayerId.Red] = (0.8f, 0.1f, 0.1f, 1f),
                [PlayerId.Blue] = (0.1f, 0.3f, 0.9f, 1f),
                [PlayerId.Green] = (0.1f, 0.7f, 0.2f, 1f),
                [PlayerId.Yellow] = (0.95f, 0.85f, 0.1f, 1f)
            };

        public static void ValidatePlayerCount(int playerCount)
        {
            if (playerCount < MinPlayers || playerCount > MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(playerCount),
                    $"Player count must be between {MinPlayers} and {MaxPlayers} inclusive.");
            }
        }
    }
}
