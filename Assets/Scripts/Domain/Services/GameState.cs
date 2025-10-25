using System;
using System.Collections.Generic;
using System.Linq;
using Uckers.Domain.Model;

namespace Uckers.Domain.Services
{
    public enum TokenStatus
    {
        Base,
        Track,
        Home,
        Finished
    }

    public readonly struct TokenSnapshot
    {
        public TokenSnapshot(PlayerId player, int tokenIndex, TokenStatus status, int progress)
        {
            Player = player;
            TokenIndex = tokenIndex;
            Status = status;
            Progress = progress;
        }

        public PlayerId Player { get; }
        public int TokenIndex { get; }
        public TokenStatus Status { get; }
        public int Progress { get; }
    }

    internal struct TokenRecord
    {
        public TokenStatus Status;
        public int Progress;
    }

    public sealed class GameState
    {
        private readonly BoardTopology topology;
        private readonly List<PlayerId> players;
        private readonly Dictionary<PlayerId, List<TokenRecord>> tokensByPlayer = new Dictionary<PlayerId, List<TokenRecord>>();

        public GameState(BoardTopology boardTopology, IEnumerable<PlayerId> playerOrder)
        {
            topology = boardTopology ?? throw new ArgumentNullException(nameof(boardTopology));
            if (playerOrder == null)
            {
                throw new ArgumentNullException(nameof(playerOrder));
            }

            players = playerOrder.ToList();
            if (players.Count == 0)
            {
                throw new ArgumentException("Player order cannot be empty", nameof(playerOrder));
            }

            GameConfig.ValidatePlayerCount(players.Count);

            foreach (var player in players)
            {
                var list = new List<TokenRecord>();
                for (int i = 0; i < GameConfig.TokensPerPlayer; i++)
                {
                    list.Add(new TokenRecord { Status = TokenStatus.Base, Progress = -1 });
                }

                tokensByPlayer[player] = list;
            }

            ValidateInvariants();
        }

        public IReadOnlyList<PlayerId> Players => players;

        public IReadOnlyList<TokenSnapshot> GetTokens(PlayerId playerId)
        {
            ValidatePlayer(playerId);
            var list = tokensByPlayer[playerId];
            return list.Select((record, index) => new TokenSnapshot(playerId, index, record.Status, record.Progress)).ToList();
        }

        public TokenSnapshot GetToken(PlayerId playerId, int tokenIndex)
        {
            ValidatePlayer(playerId);
            ValidateTokenIndex(tokenIndex);
            var record = tokensByPlayer[playerId][tokenIndex];
            return new TokenSnapshot(playerId, tokenIndex, record.Status, record.Progress);
        }

        public IEnumerable<TokenSnapshot> GetAllTokens()
        {
            foreach (var player in players)
            {
                var records = tokensByPlayer[player];
                for (int i = 0; i < records.Count; i++)
                {
                    var record = records[i];
                    yield return new TokenSnapshot(player, i, record.Status, record.Progress);
                }
            }
        }

        public void ApplyMove(TokenMove move)
        {
            if (move == null)
            {
                throw new ArgumentNullException(nameof(move));
            }

            ValidatePlayer(move.Player);
            ValidateTokenIndex(move.TokenIndex);

            if (move.Steps.Count == 0)
            {
                throw new ArgumentException("Move must contain at least one step", nameof(move));
            }

            var list = tokensByPlayer[move.Player];
            var record = list[move.TokenIndex];
            var finalStep = move.Steps[move.Steps.Count - 1];
            record.Status = finalStep.Status;
            record.Progress = finalStep.Progress;
            list[move.TokenIndex] = record;

            foreach (var capture in move.Captures)
            {
                ValidatePlayer(capture.Player);
                ValidateTokenIndex(capture.TokenIndex);
                var capturedList = tokensByPlayer[capture.Player];
                var captured = capturedList[capture.TokenIndex];
                captured.Status = TokenStatus.Base;
                captured.Progress = -1;
                capturedList[capture.TokenIndex] = captured;
            }

            ValidateInvariants();
        }

        public bool HasPlayerWon(PlayerId playerId)
        {
            ValidatePlayer(playerId);
            return tokensByPlayer[playerId].All(t => t.Status == TokenStatus.Finished);
        }

        public void ValidateInvariants()
        {
            foreach (var player in players)
            {
                var list = tokensByPlayer[player];
                if (list.Count != GameConfig.TokensPerPlayer)
                {
                    throw new InvalidOperationException($"Player {player} does not have the expected number of tokens");
                }

                int baseCount = 0;
                int trackCount = 0;
                int homeCount = 0;
                int finishedCount = 0;

                foreach (var record in list)
                {
                    switch (record.Status)
                    {
                        case TokenStatus.Base:
                            if (record.Progress != -1)
                            {
                                throw new InvalidOperationException("Base tokens must have progress -1");
                            }

                            baseCount++;
                            break;
                        case TokenStatus.Track:
                            EnsureRange(record.Progress, 0, topology.TrackLength - 1);
                            trackCount++;
                            break;
                        case TokenStatus.Home:
                            ValidateHomeProgress(player, record.Progress);
                            homeCount++;
                            break;
                        case TokenStatus.Finished:
                            int expected = topology.GetFinalHomeProgress(player);
                            if (record.Progress != expected)
                            {
                                throw new InvalidOperationException("Finished tokens must be at the final home progress");
                            }

                            finishedCount++;
                            break;
                        default:
                            throw new InvalidOperationException("Unknown token status");
                    }
                }

                EnsureRange(baseCount, 0, GameConfig.TokensPerPlayer);
                EnsureRange(trackCount, 0, GameConfig.TokensPerPlayer);
                EnsureRange(homeCount, 0, GameConfig.TokensPerPlayer);
                EnsureRange(finishedCount, 0, GameConfig.TokensPerPlayer);

                int total = baseCount + trackCount + homeCount + finishedCount;
                if (total != GameConfig.TokensPerPlayer)
                {
                    throw new InvalidOperationException($"Token conservation violated for player {player}");
                }
            }
        }

        private void ValidatePlayer(PlayerId playerId)
        {
            if (!tokensByPlayer.ContainsKey(playerId))
            {
                throw new ArgumentException("Player is not part of this game", nameof(playerId));
            }
        }

        private static void ValidateTokenIndex(int tokenIndex)
        {
            if (tokenIndex < 0 || tokenIndex >= GameConfig.TokensPerPlayer)
            {
                throw new ArgumentOutOfRangeException(nameof(tokenIndex));
            }
        }

        private void ValidateHomeProgress(PlayerId player, int progress)
        {
            int homeIndex = topology.ToHomeIndex(progress);
            int homeLength = topology.GetHomeLength(player);
            EnsureRange(homeIndex, 0, homeLength - 1);
        }

        private static void EnsureRange(int value, int min, int max)
        {
            if (value < min || value > max)
            {
                throw new InvalidOperationException($"Value {value} outside expected range {min}..{max}");
            }
        }
    }
}
