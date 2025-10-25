using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Uckers.Domain.Model;
using Uckers.Domain.Services;

namespace Uckers.Tests.Domain.State
{
    public class GameStateInvariantsTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void InitialStatePlacesAllTokensAtBase(int playerCount)
        {
            var context = TestContext.Create(playerCount);

            foreach (var player in context.Players)
            {
                var tokens = context.State.GetTokens(player);
                Assert.That(tokens.All(t => t.Status == TokenStatus.Base));
            }

            Assert.DoesNotThrow(context.State.ValidateInvariants);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void InvariantsHoldAfterMovesAndCaptures(int playerCount)
        {
            var context = TestContext.Create(playerCount);
            var attacker = context.Players[0];
            var defender = context.Players[1 % context.Players.Count];

            context.LeaveBase(attacker, 0);
            context.LeaveBase(defender, 0);

            context.AdvanceBySteps(defender, 0, 4);
            int target = context.State.GetToken(defender, 0).Progress;
            int distance = context.DistanceOnTrack(attacker, 0, target);

            context.AdvanceBySteps(attacker, 0, distance);

            Assert.DoesNotThrow(context.State.ValidateInvariants);

            context.FinishToken(attacker, 1);
            context.FinishToken(attacker, 2);

            Assert.DoesNotThrow(context.State.ValidateInvariants);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void CapturedTokenReturnsToBase(int playerCount)
        {
            var context = TestContext.Create(playerCount);
            var attacker = context.Players[0];
            var defender = context.Players[1 % context.Players.Count];

            context.LeaveBase(attacker, 0);
            context.LeaveBase(defender, 0);

            context.AdvanceBySteps(defender, 0, 3);
            int target = context.State.GetToken(defender, 0).Progress;
            int distance = context.DistanceOnTrack(attacker, 0, target);

            context.AdvanceBySteps(attacker, 0, distance);

            var defenderToken = context.State.GetToken(defender, 0);
            Assert.AreEqual(TokenStatus.Base, defenderToken.Status);
            Assert.AreEqual(-1, defenderToken.Progress);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void HasPlayerWonWhenAllTokensFinished(int playerCount)
        {
            var context = TestContext.Create(playerCount);
            var player = context.Players[0];

            for (int i = 0; i < GameConfig.TokensPerPlayer; i++)
            {
                context.FinishToken(player, i);
            }

            Assert.IsTrue(context.State.HasPlayerWon(player));
        }

        [Test]
        public void TurnManagerHonoursExtraRoll()
        {
            var players = GameConfig.PlayerOrder.Take(3).ToList();
            var turnManager = new TurnManager(players);
            var first = turnManager.CurrentPlayer;

            turnManager.AdvanceTurn(hasExtraRoll: true);
            Assert.AreEqual(first, turnManager.CurrentPlayer);

            turnManager.AdvanceTurn(hasExtraRoll: false);
            Assert.AreEqual(players[1], turnManager.CurrentPlayer);
        }

        private sealed class TestContext
        {
            private TestContext(BoardTopology topology, List<PlayerId> players)
            {
                Topology = topology;
                Players = players;
                State = new GameState(Topology, Players);
                Rules = new RulesEngine(Topology);
            }

            public GameState State { get; }
            public RulesEngine Rules { get; }
            public BoardTopology Topology { get; }
            public List<PlayerId> Players { get; }

            public static TestContext Create(int playerCount)
            {
                var topology = new BoardTopology();
                var players = GameConfig.PlayerOrder.Take(playerCount).ToList();
                return new TestContext(topology, players);
            }

            public void LeaveBase(PlayerId player, int tokenIndex)
            {
                ApplyMove(GetMove(player, tokenIndex, 6));
            }

            public void AdvanceBySteps(PlayerId player, int tokenIndex, int steps)
            {
                int remaining = steps;
                while (remaining > 0)
                {
                    int roll = Math.Min(6, remaining);
                    ApplyMove(GetMove(player, tokenIndex, roll));
                    remaining -= roll;
                }
            }

            public void FinishToken(PlayerId player, int tokenIndex)
            {
                LeaveBase(player, tokenIndex);
                AdvanceBySteps(player, tokenIndex, Topology.TrackLength - 1);
                ApplyMove(GetMove(player, tokenIndex, 1));
                int homeSteps = Topology.GetHomeLength(player) - 1;
                if (homeSteps > 0)
                {
                    ApplyMove(GetMove(player, tokenIndex, homeSteps));
                }
            }

            public int DistanceOnTrack(PlayerId player, int tokenIndex, int targetProgress)
            {
                var token = State.GetToken(player, tokenIndex);
                int current = token.Progress;
                return (targetProgress - current + Topology.TrackLength) % Topology.TrackLength;
            }

            private TokenMove GetMove(PlayerId player, int tokenIndex, int roll)
            {
                return Rules.GetLegalMoves(State, player, roll).Single(m => m.TokenIndex == tokenIndex);
            }

            private void ApplyMove(TokenMove move)
            {
                State.ApplyMove(move);
            }
        }
    }
}
