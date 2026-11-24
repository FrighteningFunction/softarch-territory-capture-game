using TerritoryCapture.Contracts;

namespace TerritoryCapture.TestSupport;

public class ControllableComputerStrategy : IComputerStrategy
{
    public Queue<GameColor> NextColors { get; } = new();

    public ManualResetEventSlim PauseEvent { get; } = new(true);

    public GameColor ChooseColor(GameState gameState, IGameRules gameRules)
    {
        PauseEvent.Wait();
        return NextColors.Count > 0 ? NextColors.Dequeue() : GameColor.Red;
    }
}