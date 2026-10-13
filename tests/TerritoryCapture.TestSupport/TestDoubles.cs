using System.Collections.Immutable;
using TerritoryCapture.Contracts;

namespace TerritoryCapture.TestSupport;

// B/D configure these from a test; they are never referenced by the Web app.
public sealed class ScriptedRules : IGameRules
{
    public StateResult Creation { get; set; } = new(GameFixtures.HumanTurn(), RuleError.None);
    public MoveResult Move { get; set; } = new(null, 0, RuleError.IllegalColor);
    public ImmutableArray<GameColor> Colors { get; set; } =
        [GameColor.Orange, GameColor.Yellow, GameColor.Green, GameColor.Purple];
    public ValidationResult Validation { get; set; } = new(true, []);
    public GameSummary Summary { get; set; } = new([new(0, 1), new(1, 1)], 118, []);
    public int ApplyCalls { get; private set; }
    public StateResult Create(NewGameOptions options, int seed) => Creation;
    public ImmutableArray<GameColor> LegalColors(GameState state) => Colors;
    public MoveResult ApplyMove(GameState state, int playerId, GameColor color)
    {
        ApplyCalls++;
        return Move;
    }
    public ValidationResult Validate(GameState state) => Validation;
    public GameSummary Summarize(GameState state) => Summary;
}

public sealed class FixedStrategy : IComputerStrategy
{
    public GameColor Color { get; set; } = GameColor.Orange;
    public GameColor ChooseColor(GameState state, IGameRules rules) => Color;
}

public sealed class MemorySaveStore : IGameSaveStore
{
    public GameSnapshot? Snapshot { get; private set; }
    public ValueTask<StoreResult> SaveAsync(GameSnapshot snapshot, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Snapshot = snapshot;
        return ValueTask.FromResult(new StoreResult(StoreError.None));
    }
    public ValueTask<LoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(new LoadResult(Snapshot,
            Snapshot is null ? StoreError.Missing : StoreError.None));
    }
}
