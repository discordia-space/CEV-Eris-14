namespace Content.Server.Damage.Components;

[RegisterComponent]
public sealed class PainDamageOnHitComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("damage")]
    public float Damage = 30f;
}
