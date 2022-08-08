using Content.Shared.Sound;

namespace Content.Server.Damage.Events;

/// <summary>
/// Attempting to apply stamina damage on a melee hit to an entity.
/// </summary>
[ByRefEvent]
public struct PainDamageOnHitAttemptEvent
{
    public bool Cancelled;
    public SoundSpecifier? HitSoundOverride;
}
