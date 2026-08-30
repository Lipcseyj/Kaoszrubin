using KaoszRubin.Domain.Characters;

namespace KaoszRubin.Application;

/// <summary>A Game játékhurkának transportfüggetlen host-publikálási felülete.</summary>
public interface ICoopHostLoop
{
    string ConnectionHint { get; }
    bool ShouldPublish(DateTime utcNow);
    bool TryPublish(SessionSnapshot snapshot);
    bool TryPublishCharacterState(CharacterId characterId, string characterData, CharacterSyncReason reason);
}
