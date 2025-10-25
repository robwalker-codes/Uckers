using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Uckers.Domain.Model;
using Uckers.Domain.Services;

public class GameManager : MonoBehaviour
{
    private enum TurnState
    {
        Idle,
        Rolling,
        Selecting,
        Animating,
        GameOver
    }

    private BoardBuilder board;
    private UIController ui;
    private TurnManager turnManager;
    private DomainAdapter domainAdapter;

    private TurnState state = TurnState.Idle;
    private readonly System.Random rng = new System.Random();
    private int lastRoll;

    private readonly List<DomainAdapter.MovePlan> moveOptions = new List<DomainAdapter.MovePlan>();
    private int selectedIndex;
    private float lastClickTime;
    private const float DoubleClickWindow = 0.3f;

    public void Initialise(BoardBuilder boardRef, IDictionary<PlayerId, List<TokenView>> tokens, UIController controller, TurnManager manager, DomainAdapter adapter)
    {
        board = boardRef;
        ui = controller;
        turnManager = manager;
        domainAdapter = adapter;
        _ = tokens;

        ui.SetRollEnabled(true);
        ui.SetRollLabel("Roll");
        ui.SetTurn($"{turnManager.CurrentPlayer} to roll");
        ui.SetStatus("Press Roll or Space to begin");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartScene();
        }

        switch (state)
        {
            case TurnState.Idle:
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    AttemptRoll();
                }
                break;
            case TurnState.Selecting:
                HandleSelectionInput();
                break;
        }
    }

    private void HandleSelectionInput()
    {
        if (moveOptions.Count == 0)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (TryRaycastToken(out var clickedToken))
            {
                int idx = moveOptions.FindIndex(m => m.Token == clickedToken);
                if (idx >= 0)
                {
                    if (selectedIndex == idx && Time.time - lastClickTime <= DoubleClickWindow)
                    {
                        ConfirmSelection();
                        return;
                    }

                    SelectMove(idx);
                    lastClickTime = Time.time;
                    return;
                }
            }

            CycleSelection();
            lastClickTime = Time.time;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ConfirmSelection();
        }
    }

    private bool TryRaycastToken(out TokenView token)
    {
        token = null;
        var ray = Camera.main != null ? Camera.main.ScreenPointToRay(Input.mousePosition) : new Ray();
        if (Physics.Raycast(ray, out var hit, 100f))
        {
            token = hit.collider.GetComponentInParent<TokenView>();
        }

        return token != null;
    }

    public void RequestRoll()
    {
        AttemptRoll();
    }

    private void AttemptRoll()
    {
        if (state != TurnState.Idle)
        {
            return;
        }

        state = TurnState.Rolling;
        ui.SetRollEnabled(false);
        lastRoll = rng.Next(1, 7);
        var currentPlayer = turnManager.CurrentPlayer;
        ui.SetStatus($"{currentPlayer} rolled {lastRoll}");

        moveOptions.Clear();
        BuildMoveOptions(currentPlayer);

        if (moveOptions.Count == 0)
        {
            bool extraRoll = lastRoll == 6;
            ui.SetStatus($"{currentPlayer} rolled {lastRoll} – no moves");
            StartCoroutine(AdvanceTurnDelayed(extraRoll));
            return;
        }

        if (moveOptions.Count == 1)
        {
            selectedIndex = 0;
            ConfirmSelection();
        }
        else
        {
            state = TurnState.Selecting;
            selectedIndex = 0;
            UpdateSelectionHighlights();
            ui.SetStatus($"{currentPlayer} rolled {lastRoll}. Click to cycle, Enter to confirm");
        }
    }

    private IEnumerator AdvanceTurnDelayed(bool extraRoll)
    {
        yield return new WaitForSeconds(0.75f);
        AdvanceTurn(extraRoll);
    }

    private void BuildMoveOptions(PlayerId player)
    {
        var plans = domainAdapter.GetLegalMoves(player, lastRoll);
        moveOptions.AddRange(plans);
    }

    private void ConfirmSelection()
    {
        if (selectedIndex < 0 || selectedIndex >= moveOptions.Count)
        {
            return;
        }

        var option = moveOptions[selectedIndex];
        state = TurnState.Animating;
        ui.SetStatus($"{turnManager.CurrentPlayer} moving");
        ui.SetRollEnabled(false);
        UpdateSelectionHighlights(clearOnly: true);
        StartCoroutine(ResolveMove(option));
    }

    private IEnumerator ResolveMove(DomainAdapter.MovePlan option)
    {
        var positions = new List<Vector3>(option.WorldPositions);
        var progress = new List<int>(option.ProgressSequence);
        var states = new List<TokenPlacementState>(option.StateSequence);

        yield return option.Token.AnimateMove(positions, progress, states);

        foreach (var captured in option.CapturedTokens)
        {
            captured.ReturnToBase();
        }

        if (option.CapturedTokens.Count > 0)
        {
            var capturedPlayers = option.CapturedTokens
                .Select(t => t.Player)
                .Distinct()
                .Select(p => p.ToString());
            ui.SetStatus($"{option.Token.Player} captured {string.Join(", ", capturedPlayers)}!");
        }

        domainAdapter.ApplyMove(option);

        if (CheckWin(option.Token.Player))
        {
            state = TurnState.GameOver;
            ui.SetStatus($"{option.Token.Player} wins!");
            ui.SetTurn("Game Over");
            ui.SetRollLabel("Restart (R)");
            yield break;
        }

        bool extraRoll = lastRoll == 6;
        AdvanceTurn(extraRoll);
    }

    private void AdvanceTurn(bool extraRoll)
    {
        moveOptions.Clear();
        selectedIndex = 0;

        if (extraRoll)
        {
            state = TurnState.Idle;
            ui.SetRollEnabled(true);
            ui.SetRollLabel("Roll");
            ui.SetTurn($"{turnManager.CurrentPlayer} extra roll");
            ui.SetStatus("Roll again!");
        }
        else
        {
            turnManager.AdvanceTurn(false);
            state = TurnState.Idle;
            ui.SetRollEnabled(true);
            ui.SetRollLabel("Roll");
            ui.SetTurn($"{turnManager.CurrentPlayer} to roll");
            ui.SetStatus("Press Roll or Space");
        }
    }

    private void SelectMove(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, moveOptions.Count - 1);
        UpdateSelectionHighlights();
    }

    private void CycleSelection()
    {
        if (moveOptions.Count == 0)
        {
            return;
        }

        selectedIndex = (selectedIndex + 1) % moveOptions.Count;
        UpdateSelectionHighlights();
    }

    private void UpdateSelectionHighlights(bool clearOnly = false)
    {
        foreach (var option in moveOptions)
        {
            bool highlight = !clearOnly && option == moveOptions[selectedIndex];
            option.Token.SetHighlight(highlight);
        }
    }

    private bool CheckWin(PlayerId player)
    {
        return domainAdapter.HasPlayerWon(player);
    }

    private void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

}
