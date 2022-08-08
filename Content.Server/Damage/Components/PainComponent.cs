using Content.Server.Damage.Systems;
using Robust.Shared.GameStates;

namespace Content.Server.Damage.Components;

/// <summary>
/// Add to an entity to paralyze it whenever it reaches critical amounts of Pain DamageType.
/// </summary>
[RegisterComponent]
public sealed class PainComponent : Component
{
    /// <summary>
    /// Have we reached peak Pain damage and been paralyzed?
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("critical")]
    public bool Critical;

    /// <summary>
    /// How much Pain reduces per second.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("decay")]
    public float Decay = 3f;

    /// <summary>
    /// How much time after receiving damage until Pain starts decreasing.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("cooldown")]
    public float DecayCooldown = 5f;

    /// <summary>
    /// How much Pain damage this entity has taken.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("PainDamage")]
    public float PainDamage;

    /// <summary>
    /// How much Pain damage is required to entire stam crit.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("excess")]
    public float CritThreshold = 100f;

    /// <summary>
    /// Next time we're allowed to decrease Pain damage. Refreshes whenever the stam damage is changed.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("decayAccumulator")]
    public float PainDecayAccumulator;
}
