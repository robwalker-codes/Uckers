using System;
using System.Collections.Generic;
using System.Linq;
using Uckers.Domain.Model;

namespace Uckers.Domain.Services
{
    public sealed class TurnManager
    {
        private readonly List<PlayerId> players;
        private int currentIndex;

        public TurnManager(IEnumerable<PlayerId> playerOrder)
        {
            if (playerOrder == null)
            {
                throw new ArgumentNullException(nameof(playerOrder));
            }

            players = playerOrder.ToList();
            if (players.Count == 0)
            {
                throw new ArgumentException("Turn order cannot be empty", nameof(playerOrder));
            }

            GameConfig.ValidatePlayerCount(players.Count);
            currentIndex = 0;
        }

        public PlayerId CurrentPlayer => players[currentIndex];
        public IReadOnlyList<PlayerId> Players => players;

        public PlayerId PeekNextPlayer()
        {
            int nextIndex = (currentIndex + 1) % players.Count;
            return players[nextIndex];
        }

        public void AdvanceTurn(bool hasExtraRoll)
        {
            if (hasExtraRoll)
            {
                return;
            }

            currentIndex = (currentIndex + 1) % players.Count;
        }

        public void SetCurrentPlayer(PlayerId player)
        {
            int index = players.IndexOf(player);
            if (index < 0)
            {
                throw new ArgumentException("Player is not part of the turn order", nameof(player));
            }

            currentIndex = index;
        }
    }
}
