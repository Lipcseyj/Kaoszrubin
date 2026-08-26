namespace MazeGame.Application;

/// <summary>A Game játékhurkának transportfüggetlen host-publikálási felülete.</summary>
public interface ICoopHostLoop
{
    string ConnectionHint { get; }
    bool ShouldPublish(DateTime utcNow);
    bool TryPublish(SessionSnapshot snapshot);
}
