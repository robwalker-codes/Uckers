using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using Uckers.Domain.Model;
using Uckers.Domain.Services;

namespace Uckers.Tests.Domain.Multiplayer
{
    public class TurnOrderTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void AdvancesThroughPlayersAndLoops(int playerCount)
        {
            var players = GameConfig.PlayerOrder.Take(playerCount).ToList();
            var manager = new TurnManager(players);

            var visited = new List<PlayerId>();
            int iterations = playerCount * 3;
            for (int i = 0; i < iterations; i++)
            {
                visited.Add(manager.CurrentPlayer);
                manager.AdvanceTurn(false);
            }

            for (int i = 0; i < iterations; i++)
            {
                Assert.AreEqual(players[i % playerCount], visited[i]);
            }
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void ExtraRollKeepsCurrentPlayer(int playerCount)
        {
            var players = GameConfig.PlayerOrder.Take(playerCount).ToList();
            var manager = new TurnManager(players);

            var first = manager.CurrentPlayer;
            manager.AdvanceTurn(true);
            Assert.AreEqual(first, manager.CurrentPlayer);

            manager.AdvanceTurn(false);
            Assert.AreEqual(players[1 % playerCount], manager.CurrentPlayer);
        }
    }
}
