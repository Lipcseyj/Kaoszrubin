using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Domain.Combat;

public sealed record WeaponDefinition(string Id, string Name, string? WeaponTypeId, ValueRange? Damage,
    int MinimumStrength, bool IsTwoHanded, IReadOnlySet<string> AllowedClassIds, string Description, int BasePrice,
    ItemRarity Rarity = ItemRarity.Normal, string? BaseWeaponId = null, int MagicPower = 0, double Weight = 1,
    DamageType DamageType = DamageType.Bludgeoning, int MaximumTargets = 1, bool CanAttackFromRear = false,
    string? FamilyId = null, int MaximumDurability = 100) : IDurableItemDefinition
{
    public ItemCategory Category => ItemCategory.Weapon;
    public bool IsMonsterOnly => BasePrice <= 0 || FamilyId == "NATURAL";
    public bool CanBeEquippedBy(string characterClassId, int strength) =>
        !IsMonsterOnly && AllowedClassIds.Contains(characterClassId) && strength >= MinimumStrength;

    public string GetIcon() => Id?.ToUpperInvariant() switch
    {
        // Konkrét fegyverek
        "W005" => "●━",      // bunkó
        "W006" => "✹━",      // buzogány
        "W007" => "━┄✹",     // láncos buzogány
        "W002" => "─†",      // rövid kard
        "W004" => "──†",     // hosszú kard
        "W009" => "━━‡",     // pallos
        "W008" => "✦━",     // csatacsillag
        "W011" => "━━➤",     // lándzsa
        "W012" => "━━◢➤",    // alabárd

        // Általános fegyvercsaládok
        _ => FamilyId?.ToUpperInvariant() switch
        {
            "DAGGER" => "🗡️",
            "SWORD" => "⚔️",
            "AXE" => "🪓",
            "BLUNT" => "🔨",
            "POLEARM" => "🔱",
            "SHIELD" => "🛡️",
            "BOW" => "🏹",

            "NATURAL" => DamageType switch
            {
                DamageType.Slashing => "🐾",
                DamageType.Piercing => "🦷",
                DamageType.Bludgeoning => "👊",
                DamageType.Fire => "🔥",
                DamageType.Acid => "🧪",
                DamageType.Necrotic => "☠️",
                DamageType.Chaos => "🌀",
                _ => "⚔️"
            },

            _ => "⚔️"
        }
    };
}
