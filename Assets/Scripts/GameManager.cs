using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    private class MoveOption
    {
        public Token Token;
        public List<Vector3> Positions = new List<Vector3>();
        public List<int> Progress = new List<int>();
        public List<TokenPlacementState> States = new List<TokenPlacementState>();
        public TokenPlacementState FinalState;
    }

    private Board board;
    private UIController ui;
    private readonly List<Token> redTokens = new List<Token>();
    private readonly List<Token> blueTokens = new List<Token>();

    private TurnState state = TurnState.Idle;
    private TokenColor currentPlayer = TokenColor.Red;
    private readonly System.Random rng = new System.Random();
    private int lastRoll;

    private readonly List<MoveOption> moveOptions = new List<MoveOption>();
    private int selectedIndex;
    private float lastClickTime;
    private const float DoubleClickWindow = 0.3f;

    public void Initialise(Board boardRef, IEnumerable<Token> reds, IEnumerable<Token> blues, UIController controller)
    {
        board = boardRef;
        ui = controller;
        redTokens.AddRange(reds);
        blueTokens.AddRange(blues);

        ui.Build(AttemptRoll);
        ui.SetRollEnabled(true);
        ui.SetRollLabel("Roll");
        ui.SetTurn("Red to roll");
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

    private bool TryRaycastToken(out Token token)
    {
        token = null;
        var ray = Camera.main != null ? Camera.main.ScreenPointToRay(Input.mousePosition) : new Ray();
        if (Physics.Raycast(ray, out var hit, 100f))
        {
            token = hit.collider.GetComponentInParent<Token>();
        }

        return token != null;
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
        ui.SetStatus($"{currentPlayer} rolled {lastRoll}");

        moveOptions.Clear();
        BuildMoveOptions();

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

    private void BuildMoveOptions()
    {
        var tokens = GetTokensForPlayer(currentPlayer);
        foreach (var token in tokens)
        {
            var option = BuildOptionForToken(token, lastRoll);
            if (option != null)
            {
                moveOptions.Add(option);
            }
        }
    }

    private MoveOption BuildOptionForToken(Token token, int roll)
    {
        if (token.PlacementState == TokenPlacementState.Finished)
        {
            return null;
        }

        var option = new MoveOption { Token = token };
        TokenPlacementState currentState = token.PlacementState;
        int progress = token.Progress;
        int lapCount = board.Lap.Count;
        int homeCount = board.GetHomeCount(token.Color);
        int finish = board.GetFinishProgress(token.Color);

        if (currentState == TokenPlacementState.Base)
        {
            if (roll != 6)
            {
                return null;
            }

            int targetProgress = 0;
            Vector3 pos = Raise(board.GetProgressPosition(token.Color, targetProgress));
            option.Positions.Add(pos);
            option.Progress.Add(targetProgress);
            option.States.Add(TokenPlacementState.Track);
            option.FinalState = TokenPlacementState.Track;
            return option;
        }

        bool directionForward = true;
        for (int step = 0; step < roll; step++)
        {
            bool lastStep = step == roll - 1;
            int nextProgress;
            TokenPlacementState nextState;

            if (progress < lapCount - 1)
            {
                nextProgress = progress + 1;
                nextState = TokenPlacementState.Track;
            }
            else if (progress == lapCount - 1)
            {
                nextProgress = progress + 1;
                nextState = TokenPlacementState.Home;
            }
            else
            {
                if (homeCount <= 0)
                {
                    return null;
                }

                int homeIndex = progress - lapCount;
                int maxHome = homeCount - 1;

                if (directionForward)
                {
                    if (homeIndex < maxHome)
                    {
                        homeIndex++;
                    }
                    else
                    {
                        directionForward = false;
                        homeIndex--;
                    }
                }
                else
                {
                    if (homeIndex > 0)
                    {
                        homeIndex--;
                    }
                    else
                    {
                        directionForward = true;
                        homeIndex++;
                    }
                }

                homeIndex = Mathf.Clamp(homeIndex, 0, maxHome);
                nextProgress = lapCount + homeIndex;
                nextState = TokenPlacementState.Home;
            }

            if (nextProgress == finish && lastStep)
            {
                nextState = TokenPlacementState.Finished;
            }

            progress = nextProgress;
            option.Positions.Add(Raise(board.GetProgressPosition(token.Color, progress)));
            option.Progress.Add(progress);
            option.States.Add(nextState);
        }

        if (option.Positions.Count == 0)
        {
            return null;
        }

        option.FinalState = option.States.Last();
        return option;
    }

    private void ConfirmSelection()
    {
        if (selectedIndex < 0 || selectedIndex >= moveOptions.Count)
        {
            return;
        }

        var option = moveOptions[selectedIndex];
        state = TurnState.Animating;
        ui.SetStatus($"{currentPlayer} moving");
        ui.SetRollEnabled(false);
        UpdateSelectionHighlights(clearOnly: true);
        StartCoroutine(ResolveMove(option));
    }

    private IEnumerator ResolveMove(MoveOption option)
    {
        yield return option.Token.AnimateMove(option.Positions, option.Progress, option.States);

        HandleCaptures(option);

        if (option.FinalState == TokenPlacementState.Finished)
        {
            option.Token.SetProgress(option.Progress.Last(), TokenPlacementState.Finished);
        }

        if (CheckWin(option.Token.Color))
        {
            state = TurnState.GameOver;
            ui.SetStatus($"{option.Token.Color} wins!");
            ui.SetTurn("Game Over");
            ui.SetRollLabel("Restart (R)");
            yield break;
        }

        bool extraRoll = lastRoll == 6;
        AdvanceTurn(extraRoll);
    }

    private void HandleCaptures(MoveOption option)
    {
        var opponentTokens = GetTokensForPlayer(Opponent(option.Token.Color));
        foreach (var enemy in opponentTokens)
        {
            if (enemy == null)
            {
                continue;
            }

            if (enemy.PlacementState == TokenPlacementState.Base || enemy.PlacementState == TokenPlacementState.Finished)
            {
                continue;
            }

            if (enemy.Progress == option.Token.Progress && enemy.PlacementState == option.Token.PlacementState)
            {
                enemy.ReturnToBase();
                ui.SetStatus($"{option.Token.Color} captured an enemy!");
            }
        }
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
            ui.SetTurn($"{currentPlayer} extra roll");
            ui.SetStatus("Roll again!");
        }
        else
        {
            currentPlayer = Opponent(currentPlayer);
            state = TurnState.Idle;
            ui.SetRollEnabled(true);
            ui.SetRollLabel("Roll");
            ui.SetTurn($"{currentPlayer} to roll");
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

    private bool CheckWin(TokenColor color)
    {
        var tokens = GetTokensForPlayer(color);
        return tokens.All(t => t.PlacementState == TokenPlacementState.Finished);
    }

    private void RestartScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private List<Token> GetTokensForPlayer(TokenColor color)
    {
        return color == TokenColor.Red ? redTokens : blueTokens;
    }

    private TokenColor Opponent(TokenColor color)
    {
        return color == TokenColor.Red ? TokenColor.Blue : TokenColor.Red;
    }

    private Vector3 Raise(Vector3 pos)
    {
        return pos + Vector3.up * 0.3f;
    }
}
