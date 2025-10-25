using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Uckers.Domain.Model;
using Uckers.Domain.Services;

public sealed class DomainAdapter
{
    private static readonly Vector3 RaiseOffset = Vector3.up * 0.3f;

    private readonly BoardBuilder board;
    private readonly GameState gameState;
    private readonly RulesEngine rulesEngine;
    private readonly Dictionary<PlayerId, List<TokenView>> tokensByPlayer;

    public DomainAdapter(BoardBuilder boardBuilder, IReadOnlyDictionary<PlayerId, List<TokenView>> tokenViews, IReadOnlyList<PlayerId> players)
    {
        board = boardBuilder ?? throw new ArgumentNullException(nameof(boardBuilder));
        if (tokenViews == null)
        {
            throw new ArgumentNullException(nameof(tokenViews));
        }

        if (players == null)
        {
            throw new ArgumentNullException(nameof(players));
        }

        var playerList = players.ToList();
        GameConfig.ValidatePlayerCount(playerList.Count);

        tokensByPlayer = new Dictionary<PlayerId, List<TokenView>>();
        foreach (var player in playerList)
        {
            if (!tokenViews.TryGetValue(player, out var list) || list == null)
            {
                throw new ArgumentException($"Missing tokens for player {player}", nameof(tokenViews));
            }

            if (list.Count != GameConfig.TokensPerPlayer)
            {
                throw new ArgumentException($"Player {player} must provide {GameConfig.TokensPerPlayer} tokens", nameof(tokenViews));
            }

            tokensByPlayer[player] = list;
        }

        var topology = board.Topology ?? new BoardTopology();
        gameState = new GameState(topology, playerList);
        rulesEngine = new RulesEngine(topology);
    }

    public IReadOnlyList<MovePlan> GetLegalMoves(PlayerId playerId, int roll)
    {
        var moves = rulesEngine.GetLegalMoves(gameState, playerId, roll);
        var result = new List<MovePlan>();

        foreach (var move in moves)
        {
            var tokenView = tokensByPlayer[move.Player][move.TokenIndex];
            var positions = new List<Vector3>();
            var progress = new List<int>();
            var states = new List<TokenPlacementState>();

            foreach (var step in move.Steps)
            {
                progress.Add(step.Progress);
                states.Add(ConvertStatus(step.Status));
                positions.Add(Raise(board.GetProgressPosition(move.Player, step.Progress)));
            }

            var capturedTokens = move.Captures
                .Select(capture => tokensByPlayer[capture.Player][capture.TokenIndex])
                .ToList();

            result.Add(new MovePlan(tokenView, move, positions, progress, states, capturedTokens));
        }

        return result;
    }

    public void ApplyMove(MovePlan plan)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        gameState.ApplyMove(plan.DomainMove);
    }

    public bool HasPlayerWon(PlayerId playerId)
    {
        return gameState.HasPlayerWon(playerId);
    }

    private static TokenPlacementState ConvertStatus(TokenStatus status)
    {
        return status switch
        {
            TokenStatus.Base => TokenPlacementState.Base,
            TokenStatus.Track => TokenPlacementState.Track,
            TokenStatus.Home => TokenPlacementState.Home,
            TokenStatus.Finished => TokenPlacementState.Finished,
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    private static Vector3 Raise(Vector3 position)
    {
        return position + RaiseOffset;
    }

    public sealed class MovePlan
    {
        internal MovePlan(TokenView token, TokenMove domainMove, IReadOnlyList<Vector3> worldPositions, IReadOnlyList<int> progressSequence, IReadOnlyList<TokenPlacementState> stateSequence, IReadOnlyList<TokenView> capturedTokens)
        {
            Token = token ?? throw new ArgumentNullException(nameof(token));
            DomainMove = domainMove ?? throw new ArgumentNullException(nameof(domainMove));
            WorldPositions = worldPositions ?? throw new ArgumentNullException(nameof(worldPositions));
            ProgressSequence = progressSequence ?? throw new ArgumentNullException(nameof(progressSequence));
            StateSequence = stateSequence ?? throw new ArgumentNullException(nameof(stateSequence));
            CapturedTokens = capturedTokens ?? throw new ArgumentNullException(nameof(capturedTokens));
        }

        public TokenView Token { get; }
        internal TokenMove DomainMove { get; }
        public IReadOnlyList<Vector3> WorldPositions { get; }
        public IReadOnlyList<int> ProgressSequence { get; }
        public IReadOnlyList<TokenPlacementState> StateSequence { get; }
        public IReadOnlyList<TokenView> CapturedTokens { get; }
        public TokenPlacementState FinalState => StateSequence.Count == 0 ? TokenPlacementState.Base : StateSequence[StateSequence.Count - 1];
    }
}
