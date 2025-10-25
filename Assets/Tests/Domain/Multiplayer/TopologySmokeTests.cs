using NUnit.Framework;
using System.Linq;
using Uckers.Domain.Model;

namespace Uckers.Tests.Domain.Multiplayer
{
    public class TopologySmokeTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void ProvidesEntriesAndHomeLanesForPlayers(int playerCount)
        {
            var topology = new BoardTopology();
            var players = GameConfig.PlayerOrder.Take(playerCount).ToList();

            foreach (var player in players)
            {
                int entryIndex = topology.GetEntryIndex(player);
                Assert.GreaterOrEqual(entryIndex, 0);
                Assert.Less(entryIndex, topology.Lap.Count);

                var homeLane = topology.GetHomeLane(player);
                Assert.IsNotNull(homeLane);
                Assert.AreEqual(4, homeLane.Count, "Each home lane should have 4 steps");
            }
        }

        [Test]
        public void HomeLanesDoNotOverlapExceptAtCenter()
        {
            var topology = new BoardTopology();
            var players = GameConfig.PlayerOrder.Take(GameConfig.MaxPlayers).ToList();

            var laneEndpoints = players.Select(p => topology.GetHomeLane(p).Last()).ToList();
            for (int i = 0; i < laneEndpoints.Count; i++)
            {
                for (int j = i + 1; j < laneEndpoints.Count; j++)
                {
                    Assert.AreEqual(laneEndpoints[i].X, laneEndpoints[j].X, 0.001f);
                    Assert.AreEqual(laneEndpoints[i].Z, laneEndpoints[j].Z, 0.001f);
                }
            }
        }
    }
}
