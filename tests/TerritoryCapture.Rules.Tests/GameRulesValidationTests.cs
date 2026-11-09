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
