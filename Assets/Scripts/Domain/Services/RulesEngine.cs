using System;
using System.Collections.Generic;
using Uckers.Domain.Model;

namespace Uckers.Domain.Services
{
    public sealed class RulesEngine
    {
        private readonly BoardTopology topology;

        public RulesEngine(BoardTopology boardTopology)
        {
            topology = boardTopology ?? throw new ArgumentNullException(nameof(boardTopology));
        }

        public IReadOnlyList<TokenMove> GetLegalMoves(GameState state, PlayerId currentPlayer, int roll)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (roll < 1 || roll > 6)
            {
                throw new ArgumentOutOfRangeException(nameof(roll));
            }

            var tokens = state.GetTokens(currentPlayer);
            var moves = new List<TokenMove>();

            foreach (var token in tokens)
            {
                var steps = CalculateSteps(currentPlayer, token, roll);
                if (steps.Count == 0)
                {
                    continue;
                }

                var captures = DetermineCaptures(state, steps, currentPlayer);
                moves.Add(new TokenMove(currentPlayer, token.TokenIndex, steps, captures));
            }

            return moves;
        }

        private List<TokenStep> CalculateSteps(PlayerId playerId, TokenSnapshot token, int roll)
        {
            switch (token.Status)
            {
                case TokenStatus.Base:
                    return roll == 6 ? LeaveBase(playerId) : new List<TokenStep>();
                case TokenStatus.Track:
                    return MoveAlongTrack(playerId, token.Progress, roll);
                case TokenStatus.Home:
                    return MoveAlongHome(playerId, topology.ToHomeIndex(token.Progress), roll);
                case TokenStatus.Finished:
                    return new List<TokenStep>();
                default:
                    throw new InvalidOperationException("Unknown token status");
            }
        }

        private List<TokenStep> LeaveBase(PlayerId playerId)
        {
            int entryIndex = topology.GetEntryIndex(playerId);
            return new List<TokenStep> { new TokenStep(entryIndex, TokenStatus.Track) };
        }

        private List<TokenStep> MoveAlongTrack(PlayerId playerId, int startIndex, int roll)
        {
            var steps = new List<TokenStep>();
            int trackIndex = startIndex;
            int homeEntry = topology.GetHomeEntryIndex(playerId);
            int trackLength = topology.TrackLength;
            int homeLength = topology.GetHomeLength(playerId);
            TokenStatus location = TokenStatus.Track;
            int homeIndex = -1;
            int direction = 1;

            for (int step = 1; step <= roll; step++)
            {
                if (location == TokenStatus.Track)
                {
                    if (trackIndex == homeEntry)
                    {
                        location = TokenStatus.Home;
                        homeIndex = 0;
                        bool finishing = homeLength > 0 && step == roll && homeIndex == homeLength - 1;
                        var status = finishing ? TokenStatus.Finished : TokenStatus.Home;
                        steps.Add(new TokenStep(topology.ToHomeProgress(homeIndex), status));

                        if (finishing)
                        {
                            location = TokenStatus.Finished;
                            break;
                        }

                        direction = 1;
                    }
                    else
                    {
                        trackIndex = (trackIndex + 1) % trackLength;
                        steps.Add(new TokenStep(trackIndex, TokenStatus.Track));
                    }
                }
                else if (location == TokenStatus.Home)
                {
                    MoveWithinHome(playerId, ref homeIndex, ref location, ref direction, step, roll, steps);
                    if (location == TokenStatus.Finished)
                    {
                        break;
                    }
                }
                else if (location == TokenStatus.Finished)
                {
                    break;
                }
            }

            return steps;
        }

        private List<TokenStep> MoveAlongHome(PlayerId playerId, int startIndex, int roll)
        {
            var steps = new List<TokenStep>();
            int homeIndex = startIndex;
            TokenStatus location = TokenStatus.Home;
            int direction = 1;

            for (int step = 1; step <= roll; step++)
            {
                MoveWithinHome(playerId, ref homeIndex, ref location, ref direction, step, roll, steps);
                if (location == TokenStatus.Finished)
                {
                    break;
                }
            }

            return steps;
        }

        private void MoveWithinHome(PlayerId playerId, ref int homeIndex, ref TokenStatus location, ref int direction, int stepNumber, int roll, List<TokenStep> steps)
        {
            int homeLength = topology.GetHomeLength(playerId);
            if (homeLength == 0)
            {
                location = TokenStatus.Finished;
                return;
            }

            if (direction > 0)
            {
                homeIndex++;
            }
            else
            {
                homeIndex--;
            }

            homeIndex = Math.Max(0, Math.Min(homeIndex, homeLength - 1));

            bool finishing = direction > 0 && homeIndex == homeLength - 1 && stepNumber == roll;
            var status = finishing ? TokenStatus.Finished : TokenStatus.Home;
            steps.Add(new TokenStep(topology.ToHomeProgress(homeIndex), status));

            if (finishing)
            {
                location = TokenStatus.Finished;
                return;
            }

            if (homeIndex == homeLength - 1)
            {
                direction = -1;
            }
            else if (homeIndex == 0)
            {
                direction = 1;
            }
        }

        private IReadOnlyList<CaptureResult> DetermineCaptures(GameState state, IReadOnlyList<TokenStep> steps, PlayerId playerId)
        {
            if (steps.Count == 0)
            {
                return Array.Empty<CaptureResult>();
            }

            var finalStep = steps[steps.Count - 1];
            if (finalStep.Status != TokenStatus.Track)
            {
                return Array.Empty<CaptureResult>();
            }

            var captures = new List<CaptureResult>();
            foreach (var candidate in state.GetAllTokens())
            {
                if (candidate.Player == playerId)
                {
                    continue;
                }

                if (candidate.Status != TokenStatus.Track)
                {
                    continue;
                }

                if (candidate.Progress == finalStep.Progress)
                {
                    captures.Add(new CaptureResult(candidate.Player, candidate.TokenIndex));
                }
            }

            return captures;
        }
    }

    public sealed class TokenMove
    {
        public TokenMove(PlayerId player, int tokenIndex, IReadOnlyList<TokenStep> steps, IReadOnlyList<CaptureResult> captures)
        {
            Player = player;
            TokenIndex = tokenIndex;
            Steps = steps ?? throw new ArgumentNullException(nameof(steps));
            Captures = captures ?? throw new ArgumentNullException(nameof(captures));
        }

        public PlayerId Player { get; }
        public int TokenIndex { get; }
        public IReadOnlyList<TokenStep> Steps { get; }
        public IReadOnlyList<CaptureResult> Captures { get; }
    }

    public readonly struct TokenStep
    {
        public TokenStep(int progress, TokenStatus status)
        {
            Progress = progress;
            Status = status;
        }

        public int Progress { get; }
        public TokenStatus Status { get; }
    }

    public readonly struct CaptureResult
    {
        public CaptureResult(PlayerId player, int tokenIndex)
        {
            Player = player;
            TokenIndex = tokenIndex;
        }

        public PlayerId Player { get; }
        public int TokenIndex { get; }
    }
}
