using System.Collections.Immutable;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.Rules.Tests;

public class GameRulesMoveTests
{
    private readonly GameRules _rules = new();

   private static GameState CreateState(
    Action<CellState[]>? configureCells = null,
    int currentPlayerIndex = 0,
    long turnNumber = 0)
    {
        var cells = Enumerable.Range(0, 120)
            .Select(id => new CellState(
                id,
                id == 0
                    ? GameColor.Red
                    : id == 119
                        ? GameColor.Blue
                        : GameColor.Orange,
                id == 0
                    ? 0
                    : id == 119
                        ? 1
                        : null))
            .ToArray();

        configureCells?.Invoke(cells);

        var players = ImmutableArray.Create(
            new PlayerState(0, PlayerKind.Human, 0, GameColor.Red),
            new PlayerState(1, PlayerKind.Human, 119, GameColor.Blue));

        return new GameState(
            12,
            10,
            cells.ToImmutableArray(),
            players,
            currentPlayerIndex,
            turnNumber,
            GamePhase.Running);
    }

    [Fact]
    public void ApplyMove_CapturesConnectedNeutralCells()
    {
        var state = CreateState(cells =>
        {
            cells[1] = cells[1] with { Color = GameColor.Green };
            cells[2] = cells[2] with { Color = GameColor.Green };
        });

        var result = _rules.ApplyMove(state, 0, GameColor.Green);

        Assert.Equal(RuleError.None, result.Error);
        Assert.NotNull(result.State);
        Assert.Equal(2, result.CapturedCount);

        Assert.Equal(0, result.State!.Cells[0].OwnerId);
        Assert.Equal(0, result.State.Cells[1].OwnerId);
        Assert.Equal(0, result.State.Cells[2].OwnerId);

        Assert.Equal(GameColor.Green, result.State.Players[0].Color);
    }

    [Fact]
    public void ApplyMove_CannotCaptureThroughOpponentTerritory()
    {
        var opponentPath = new[]
        {
            119, 118, 117, 116, 115, 114, 113, 101,
            88, 76, 63, 51, 38, 26, 13, 1
        };

        var state = CreateState(cells =>
        {
            foreach (var cellId in opponentPath)
            {
                cells[cellId] = cells[cellId] with
                {
                    Color = GameColor.Blue,
                    OwnerId = 1
                };
            }

            cells[2] = cells[2] with
            {
                Color = GameColor.Green
            };
        });

        var result = _rules.ApplyMove(state, 0, GameColor.Green);

        Assert.Equal(RuleError.None, result.Error);
        Assert.NotNull(result.State);
        Assert.Equal(0, result.CapturedCount);

        Assert.Null(result.State!.Cells[2].OwnerId);
        Assert.Equal(1, result.State.Cells[1].OwnerId);
    }

   [Fact]
    public void ApplyMove_AllowsMoveWithoutCapturingCells()
    {
        var state = CreateState();

        var result = _rules.ApplyMove(state, 0, GameColor.Green);

        Assert.Equal(RuleError.None, result.Error);
        Assert.NotNull(result.State);
        Assert.Equal(0, result.CapturedCount);

        Assert.Equal(GameColor.Green, result.State!.Players[0].Color);
        Assert.Equal(1, result.State.CurrentPlayerIndex);
        Assert.Equal(1, result.State.TurnNumber);
        Assert.Equal(GamePhase.Running, result.State.Phase);
    }

   [Fact]
    public void ApplyMove_DoesNotModifyOriginalState()
    {
        var state = CreateState();

        var originalCells = state.Cells.ToArray();
        var originalPlayers = state.Players.ToArray();

        var result = _rules.ApplyMove(state, 0, GameColor.Green);

        Assert.Equal(RuleError.None, result.Error);

        Assert.True(state.Cells.SequenceEqual(originalCells));
        Assert.True(state.Players.SequenceEqual(originalPlayers));
        Assert.Equal(0, state.TurnNumber);
        Assert.Equal(0, state.CurrentPlayerIndex);
    }

    [Fact]
    public void ApplyMove_FinishesGameWhenAllCellsAreCaptured()
    {
        var state = CreateState(cells =>
        {
            for (var i = 0; i < cells.Length; i++)
            {
                if (cells[i].OwnerId is null)
                {
                    cells[i] = cells[i] with
                    {
                        Color = GameColor.Green
                    };
                }
            }
        });

        var result = _rules.ApplyMove(state, 0, GameColor.Green);

        Assert.Equal(RuleError.None, result.Error);
        Assert.NotNull(result.State);
        Assert.Equal(118, result.CapturedCount);

        Assert.Equal(GamePhase.Finished, result.State!.Phase);
        Assert.Equal(1, result.State.TurnNumber);
        Assert.Equal(0, result.State.CurrentPlayerIndex);

        Assert.All(
            result.State.Cells,
            cell => Assert.NotNull(cell.OwnerId));
    }
    
    [Fact]
    public void ApplyMove_ReturnsInvalidStateForInvalidCurrentPlayerIndex()
    {
        var state = CreateState() with
        {
            CurrentPlayerIndex = 5
        };

        var result = _rules.ApplyMove(state, 0, GameColor.Green);

        Assert.Equal(RuleError.InvalidState, result.Error);
        Assert.Null(result.State);
        Assert.Equal(0, state.TurnNumber);
    }

    [Fact]
    public void ApplyMove_RejectsInvalidGamePhase()
    {
        var state = CreateGeneratedState() with
        {
            Phase = (GamePhase)999
        };

        var result = _rules.ApplyMove(
            state,
            state.Players[0].Id,
            GameColor.Purple);

        Assert.Equal(RuleError.InvalidState, result.Error);
        Assert.Null(result.State);
    }

    [Fact]
    public void ApplyMove_RejectsNullPlayerEntry()
    {
        var state = CreateGeneratedState();
        var players = state.Players.ToBuilder();

        players[1] = null!;

        var result = _rules.ApplyMove(
            state with { Players = players.ToImmutable() },
            state.Players[0].Id,
            GameColor.Purple);

        Assert.Equal(RuleError.InvalidState, result.Error);
    }

    [Fact]
    public void ApplyMove_RejectsNullCellEntry()
    {
        var state = CreateGeneratedState();
        var cells = state.Cells.ToBuilder();

        cells[50] = null!;

        var result = _rules.ApplyMove(
            state with { Cells = cells.ToImmutable() },
            state.Players[0].Id,
            GameColor.Purple);

        Assert.Equal(RuleError.InvalidState, result.Error);
    }

    [Fact]
    public void ApplyMove_RejectsIncorrectCellIds()
    {
        var state = CreateGeneratedState();
        var cells = state.Cells.ToBuilder();

        cells[50] = cells[50] with { Id = 49 };

        var result = _rules.ApplyMove(
            state with { Cells = cells.ToImmutable() },
            state.Players[0].Id,
            GameColor.Purple);

        Assert.Equal(RuleError.InvalidState, result.Error);
    }

    [Fact]
    public void LegalColors_ReturnsEmptyForNullPlayerEntry()
    {
        var state = CreateGeneratedState();
        var players = state.Players.ToBuilder();

        players[1] = null!;

        var result = _rules.LegalColors(state with
        {
            Players = players.ToImmutable()
        });

        Assert.Empty(result);
    }

    [Fact]
    public void LegalColors_ReturnsEmptyForInvalidPlayerColor()
    {
        var state = CreateGeneratedState();
        var players = state.Players.ToBuilder();

        players[0] = players[0] with
        {
            Color = (GameColor)999
        };

        var result = _rules.LegalColors(state with
        {
            Players = players.ToImmutable()
        });

        Assert.Empty(result);
    }
    private GameState CreateGeneratedState()
    {
        var result = _rules.Create(
            new NewGameOptions(
                ImmutableArray.Create(
                    PlayerKind.Human,
                    PlayerKind.Human)),
            seed: 42);

        Assert.Equal(RuleError.None, result.Error);
        return result.State!;
    }
}
