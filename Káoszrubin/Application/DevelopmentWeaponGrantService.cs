using KaoszRubin.Domain.Characters;
using KaoszRubin.Domain.Combat;
using KaoszRubin.Domain.Inventory;

namespace KaoszRubin.Application;

public static class DevelopmentWeaponGrantService
{
    /// <summary>Véletlen tesztfegyverek, a hátizsák szabad helyei és kötegkapacitása erejéig.</summary>
    public static IReadOnlyList<WeaponDefinition> Grant(LiveCharacter character,
        IEnumerable<WeaponDefinition> weapons, Random random)
    {
        var candidates = weapons.Where(weapon => weapon.WeaponTypeId != "WT003" && weapon.Damage is not null &&
            !SpellcastingRules.IsRestrictedFromTradingAndGeneration(weapon)).ToArray();
        var granted = new List<WeaponDefinition>();
        Give(ItemRarity.Normal, false, 2);
        Give(ItemRarity.Normal, true, 2);
        Give(ItemRarity.Magic, null, 1);
        Give(ItemRarity.Legendary, null, 1);
        return granted;

        void Give(ItemRarity rarity, bool? twoHanded, int count)
        {
            var pool = candidates.Where(weapon => weapon.Rarity == rarity &&
                (twoHanded is null || weapon.IsTwoHanded == twoHanded.Value)).ToList();
            for (var index = 0; index < count; index++)
            {
                var fitting = pool.Where(character.CanAddToBackpack).ToArray();
                if (fitting.Length == 0) break;
                var weapon = fitting[random.Next(fitting.Length)];
                if (character.AddToBackpack(weapon)) granted.Add(weapon);
                pool.Remove(weapon);
            }
        }
    }
}
