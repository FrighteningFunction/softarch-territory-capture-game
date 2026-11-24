using System.Collections.Immutable;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.Rules.Tests;

public class GameRulesValidationTests
{
    private readonly GameRules _rules = new();

    [Fact]
    public void Validate_AcceptsValidGeneratedState()
    {
        var result = _rules.Create(
            new NewGameOptions([
                PlayerKind.Human,
                PlayerKind.Human
            ]),
            1234);

        Assert.Equal(RuleError.None, result.Error);

        var validation = _rules.Validate(result.State!);

        Assert.True(validation.IsValid);
        Assert.Empty(validation.Problems);
    }

    [Fact]
    public void Validate_RejectsInvalidBoardDimensions()
    {
        var state = CreateValidState() with
        {
            Width = 10
        };

        var result = _rules.Validate(state);

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems,
            problem => problem.Contains("Board dimensions"));
    }

    [Fact]
    public void Validate_RejectsIncorrectCellCount()
    {
        var state = CreateValidState() with
        {
            Cells = CreateValidState().Cells.RemoveAt(119)
        };

        var result = _rules.Validate(state);

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems,
            problem => problem.Contains("exactly 120 cells"));
    }

    [Fact]
    public void Validate_RejectsInvalidPlayerCount()
    {
        var state = CreateValidState() with
        {
            Players = ImmutableArray<PlayerState>.Empty
        };

        var result = _rules.Validate(state);

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems,
            problem => problem.Contains("between 2 and 4 players"));
    }

    [Fact]
    public void Validate_RejectsInvalidCurrentPlayerIndex()
    {
        var state = CreateValidState() with
        {
            CurrentPlayerIndex = 5
        };

        var result = _rules.Validate(state);

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems,
            problem => problem.Contains("Current player index"));
    }

    [Fact]
    public void Validate_RejectsNegativeTurnNumber()
    {
        var state = CreateValidState() with
        {
            TurnNumber = -1
        };

        var result = _rules.Validate(state);

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems,
            problem => problem.Contains("Turn number"));
    }

    [Fact]
    public void Validate_RejectsInvalidCellId()
    {
        var state = CreateValidState();
        var cells = state.Cells.ToBuilder();

        cells[1] = cells[1] with { Id = 0 };

        var result = _rules.Validate(state with
        {
            Cells = cells.ToImmutable()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems,
            problem => problem.Contains("Cell at index 1"));
    }

    [Fact]
    public void Validate_RejectsInvalidCellColor()
    {
        var state = CreateValidState();
        var cells = state.Cells.ToBuilder();

        cells[50] = cells[50] with
        {
            Color = (GameColor)999
        };

        var result = _rules.Validate(state with
        {
            Cells = cells.ToImmutable()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems,
            problem => problem.Contains("invalid color"));
    }

     [Fact]
    public void Validate_RejectsUnknownCellOwner()
    {
        var state = CreateValidState();
        var cells = state.Cells.ToBuilder();

        cells[50] = cells[50] with { OwnerId = 99 };

        var result = _rules.Validate(state with
        {
            Cells = cells.ToImmutable()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems,
            problem => problem.Contains("unknown player"));
    }

    [Fact]
    public void Validate_RejectsUnownedStartingCell()
    {
        var state = CreateValidState();
        var cells = state.Cells.ToBuilder();

        cells[0] = cells[0] with { OwnerId = null };

        var result = _rules.Validate(state with
        {
            Cells = cells.ToImmutable()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems,
            problem => problem.Contains("does not own their starting cell"));
    }

    [Fact]
    public void Validate_RejectsOwnerColorMismatch()
    {
        var state = CreateValidState();
        var cells = state.Cells.ToBuilder();

        cells[0] = cells[0] with { Color = GameColor.Blue };

        var result = _rules.Validate(state with
        {
            Cells = cells.ToImmutable()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems,
            problem => problem.Contains("color does not match"));
    }

    [Fact]
    public void Validate_RejectsDisconnectedTerritory()
    {
        var state = CreateValidState();
        var cells = state.Cells.ToBuilder();

        cells[50] = cells[50] with
        {
            Color = GameColor.Red,
            OwnerId = 0
        };

        var result = _rules.Validate(state with
        {
            Cells = cells.ToImmutable()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems,
            problem => problem.Contains("disconnected territory"));
    }

    [Fact]
    public void Validate_RejectsCapturableNeutralCell()
    {
        var state = CreateValidState();
        var cells = state.Cells.ToBuilder();

        // Cell 1 is adjacent to player 0's starting cell (0).
        cells[1] = cells[1] with
        {
            Color = GameColor.Red,
            OwnerId = null
        };

        var result = _rules.Validate(state with
        {
            Cells = cells.ToImmutable()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems,
            problem => problem.Contains("should already be captured"));
    }

    [Fact]
    public void Validate_AllowsUnreachableNeutralCellWithPlayerColor()
    {
        var state = CreateValidState();
        var cells = state.Cells.ToBuilder();

        cells[50] = cells[50] with
        {
            Color = GameColor.Red
        };

        var result = _rules.Validate(state with
        {
            Cells = cells.ToImmutable()
        });

        Assert.True(result.IsValid);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void Validate_RejectsRunningGameWithoutNeutralCells()
    {
        var state = CreateValidState();
        var cells = state.Cells
            .Select(cell => cell with
            {
                OwnerId = cell.Id == 119 ? 1 : 0,
                Color = cell.Id == 119 ? GameColor.Blue : GameColor.Red
            })
            .ToImmutableArray();

        var result = _rules.Validate(state with
        {
            Cells = cells
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems,
            problem => problem.Contains("Running game cannot have zero neutral cells"));
    }

    [Fact]
    public void Validate_RejectsFinishedGameWithNeutralCells()
    {
        var state = CreateValidState();

        var result = _rules.Validate(state with
        {
            Phase = GamePhase.Finished
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems,
            problem => problem.Contains("Finished game cannot have neutral cells"));
    }

    [Fact]
    public void Validate_RejectsDefaultCollections()
    {
        var state = CreateValidState() with
        {
            Cells = default,
            Players = default
        };

        var result = _rules.Validate(state);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Problems);
    }

    [Fact]
    public void Validate_RejectsNullPlayerEntry()
    {
        var state = CreateValidState();
        var players = state.Players.ToBuilder();

        players[1] = null!;

        var result = _rules.Validate(state with
        {
            Players = players.ToImmutable()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems,
            problem => problem.Contains("Player entry cannot be null"));
    }

    [Fact]
    public void Validate_RejectsNullCellEntry()
    {
        var state = CreateValidState();
        var cells = state.Cells.ToBuilder();

        cells[50] = null!;

        var result = _rules.Validate(state with
        {
            Cells = cells.ToImmutable()
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Problems,
            problem => problem.Contains("Cell entry cannot be null"));
    }

    private static GameState CreateValidState()
    {
        return new GameState(
            12,
            10,
            Enumerable.Range(0, 120)
                .Select(id => new CellState(
                    id,
                    id == 0 ? GameColor.Red :
                    id == 119 ? GameColor.Blue :
                    GameColor.Green,
                    id == 0 ? 0 :
                    id == 119 ? 1 :
                    null))
                .ToImmutableArray(),
            [new PlayerState(0, PlayerKind.Human, 0, GameColor.Red), new PlayerState(1, PlayerKind.Human, 119, GameColor.Blue)],
            0,
            0,
            GamePhase.Running);
    }
}
