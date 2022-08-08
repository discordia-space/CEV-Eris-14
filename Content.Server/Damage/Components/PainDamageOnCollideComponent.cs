using Robust.Shared.GameStates;

namespace Content.Server.Damage.Components;

/// <summary>
/// Applies Pain damage when colliding with an entity.
/// </summary>
[RegisterComponent]
public sealed class PainDamageOnCollideComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("damage")]
    public float Damage = 55f;
}
