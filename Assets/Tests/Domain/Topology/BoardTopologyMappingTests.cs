using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Uckers.Domain.Model;

namespace Uckers.Tests.Domain.Topology
{
    public class BoardTopologyMappingTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void PlayersHaveValidEntriesAndHomeLanes(int playerCount)
        {
            var topology = new BoardTopology();
            var players = GameConfig.PlayerOrder.Take(playerCount).ToList();

            foreach (var player in players)
            {
                int entry = topology.GetEntryIndex(player);
                Assert.That(entry, Is.GreaterThanOrEqualTo(0).And.LessThan(topology.TrackLength));

                int homeEntry = topology.GetHomeEntryIndex(player);
                Assert.That(homeEntry, Is.GreaterThanOrEqualTo(0).And.LessThan(topology.TrackLength));

                var homeLane = topology.GetHomeLane(player);
                Assert.That(homeLane, Is.Not.Null);
                Assert.AreEqual(GameConfig.TokensPerPlayer, homeLane.Count);
            }
        }

        [Test]
        public void HomeLanesOnlyOverlapAtFinalNode()
        {
            var topology = new BoardTopology();
            var players = GameConfig.PlayerOrder.Take(GameConfig.MaxPlayers).ToList();

            var nonFinalPoints = new HashSet<(float x, float z)>();
            foreach (var player in players)
            {
                var lane = topology.GetHomeLane(player);
                for (int i = 0; i < lane.Count - 1; i++)
                {
                    var point = lane[i];
                    var key = (Round(point.X), Round(point.Z));
                    Assert.IsFalse(nonFinalPoints.Contains(key), $"Home lane overlap detected before centre for {player}");
                    nonFinalPoints.Add(key);
                }
            }

            var finalPoints = players
                .Select(player => topology.GetHomeLane(player).Last())
                .Select(p => (Round(p.X), Round(p.Z)))
                .Distinct()
                .ToList();

            Assert.AreEqual(1, finalPoints.Count, "Final home positions should converge at centre");
        }

        private static float Round(float value)
        {
            return Mathf.Round(value * 1000f) / 1000f;
        }
    }
}
