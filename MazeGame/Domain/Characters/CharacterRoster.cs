namespace MazeGame.Domain.Characters;

/// <summary>A már generált, használható karakterek listája. Új játék kezdetén üres.</summary>
public sealed class CharacterRoster
{
    private readonly List<LiveCharacter> _characters = [];
    public IReadOnlyList<LiveCharacter> Characters => _characters;
    public void Add(LiveCharacter character) => _characters.Add(character);
}
