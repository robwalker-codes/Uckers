using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Uckers.Domain.Model;
using Uckers.Domain.Services;

namespace Uckers.Tests.Domain.Rules
{
    public class RulesEngineMultiPlayerTests
    {
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void BaseTokensRequireSixToLeave(int playerCount)
        {
            var context = TestContext.Create(playerCount);
            var player = context.Players[0];

            var withoutSix = context.Rules.GetLegalMoves(context.State, player, 5);
            Assert.IsEmpty(withoutSix);

            var withSix = context.Rules.GetLegalMoves(context.State, player, 6);
            Assert.AreEqual(GameConfig.TokensPerPlayer, withSix.Count);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void MultipleTokensProvideIndependentMoves(int playerCount)
        {
            var context = TestContext.Create(playerCount);
            var player = context.Players[0];

            context.LeaveBase(player, tokenIndex: 0);
            context.LeaveBase(player, tokenIndex: 1);

            var moves = context.Rules.GetLegalMoves(context.State, player, 3);
            Assert.AreEqual(2, moves.Count);
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, moves.Select(m => m.TokenIndex));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void CaptureSendsOpponentBackToBase(int playerCount)
        {
            var context = TestContext.Create(playerCount);
            var attacker = context.Players[0];
            var defender = context.Players[1 % context.Players.Count];

            context.LeaveBase(attacker, 0);
            context.LeaveBase(defender, 0);

            context.AdvanceBySteps(defender, 0, 4);
            var targetPosition = context.State.GetToken(defender, 0).Progress;

            int attackerSteps = context.DistanceOnTrack(attacker, 0, targetPosition);
            var captureMove = context.AdvanceBySteps(attacker, 0, attackerSteps, recordFinalMove: true);

            Assert.IsNotNull(captureMove);
            Assert.IsTrue(captureMove.Captures.Any(c => c.Player == defender && c.TokenIndex == 0));

            var defenderToken = context.State.GetToken(defender, 0);
            Assert.AreEqual(TokenStatus.Base, defenderToken.Status);
            Assert.AreEqual(-1, defenderToken.Progress);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void ExactRollFinishesToken(int playerCount)
        {
            var context = TestContext.Create(playerCount);
            var player = context.Players[0];

            context.PrepareTokenForHome(player, 0);
            context.AdvanceToken(player, 0, 1); // enter home lane

            int homeLength = context.Topology.GetHomeLength(player);
            if (homeLength > 1)
            {
                context.AdvanceToken(player, 0, homeLength - 2);
            }

            var finishMove = context.GetMove(player, 0, 1);
            Assert.AreEqual(TokenStatus.Finished, finishMove.Steps.Last().Status);
            context.State.ApplyMove(finishMove);

            var snapshot = context.State.GetToken(player, 0);
            Assert.AreEqual(TokenStatus.Finished, snapshot.Status);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void OvershootBouncesWithinHomeLane(int playerCount)
        {
            var context = TestContext.Create(playerCount);
            var player = context.Players[0];

            context.PrepareTokenForHome(player, 1);
            context.AdvanceToken(player, 1, 1); // enter home lane

            int homeLength = context.Topology.GetHomeLength(player);
            if (homeLength > 2)
            {
                context.AdvanceToken(player, 1, homeLength - 3);
            }

            var bounceMove = context.GetMove(player, 1, 2);
            context.State.ApplyMove(bounceMove);

            var snapshot = context.State.GetToken(player, 1);
            Assert.AreEqual(TokenStatus.Home, snapshot.Status);
            int expectedIndex = Math.Max(0, homeLength - 2);
            Assert.AreEqual(context.Topology.ToHomeProgress(expectedIndex), snapshot.Progress);
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void PlayerWinsAfterFinishingAllTokens(int playerCount)
        {
            var context = TestContext.Create(playerCount);
            var player = context.Players[0];

            for (int tokenIndex = 0; tokenIndex < GameConfig.TokensPerPlayer; tokenIndex++)
            {
                context.FinishToken(player, tokenIndex);
            }

            Assert.IsTrue(context.State.HasPlayerWon(player));
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

            public void AdvanceToken(PlayerId player, int tokenIndex, int roll)
            {
                ApplyMove(GetMove(player, tokenIndex, roll));
            }

            public TokenMove GetMove(PlayerId player, int tokenIndex, int roll)
            {
                var moves = Rules.GetLegalMoves(State, player, roll);
                return moves.Single(m => m.TokenIndex == tokenIndex);
            }

            public void ApplyMove(TokenMove move)
            {
                State.ApplyMove(move);
            }

            public TokenMove AdvanceBySteps(PlayerId player, int tokenIndex, int steps, bool recordFinalMove = false)
            {
                TokenMove lastMove = null;
                int remaining = steps;
                while (remaining > 0)
                {
                    int roll = Math.Min(6, remaining);
                    var move = GetMove(player, tokenIndex, roll);
                    ApplyMove(move);
                    lastMove = move;
                    remaining -= roll;
                }

                return recordFinalMove ? lastMove : null;
            }

            public int DistanceOnTrack(PlayerId player, int tokenIndex, int targetProgress)
            {
                var token = State.GetToken(player, tokenIndex);
                int current = token.Progress;
                if (current < 0)
                {
                    throw new InvalidOperationException("Token must be on the track to measure distance");
                }

                int distance = (targetProgress - current + Topology.TrackLength) % Topology.TrackLength;
                return distance;
            }

            public void PrepareTokenForHome(PlayerId player, int tokenIndex)
            {
                LeaveBase(player, tokenIndex);
                int stepsToHomeEntry = Topology.TrackLength - 1;
                AdvanceBySteps(player, tokenIndex, stepsToHomeEntry);
            }

            public void FinishToken(PlayerId player, int tokenIndex)
            {
                PrepareTokenForHome(player, tokenIndex);
                AdvanceToken(player, tokenIndex, 1); // enter home lane
                int homeSteps = Topology.GetHomeLength(player) - 1;
                if (homeSteps > 0)
                {
                    AdvanceToken(player, tokenIndex, homeSteps);
                }
            }
        }
    }
}
