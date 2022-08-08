using Content.Server.Damage.Components;
using Content.Server.Damage.Events;
using Content.Server.Popups;
using Content.Server.Weapon.Melee;
using Content.Shared.Alert;
using Content.Shared.Rounding;
using Content.Shared.Stunnable;
using Robust.Shared.Collections;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Damage.Systems;

public sealed class PainSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stunSystem = default!;

    private const float UpdateCooldown = 2f;
    private float _accumulator;

    private const string CollideFixture = "projectile";

    /// <summary>
    /// How much of a buffer is there between the stun duration and when stuns can be re-applied.
    /// </summary>
    private const float StamCritBufferTime = 3f;

    private readonly List<EntityUid> _dirtyEntities = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PainDamageOnCollideComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<PainDamageOnHitComponent, MeleeHitEvent>(OnHit);
        SubscribeLocalEvent<PainComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<PainComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(EntityUid uid, PainComponent component, ComponentShutdown args)
    {
        SetPainAlert(uid);
    }

    private void OnStartup(EntityUid uid, PainComponent component, ComponentStartup args)
    {
        SetPainAlert(uid, component);
    }

    private void OnHit(EntityUid uid, PainDamageOnHitComponent component, MeleeHitEvent args)
    {
        if (component.Damage <= 0f) return;

        var ev = new PainDamageOnHitAttemptEvent();
        RaiseLocalEvent(uid, ref ev);

        if (ev.Cancelled) return;

        args.HitSoundOverride = ev.HitSoundOverride;
        var stamQuery = GetEntityQuery<PainComponent>();
        var toHit = new ValueList<PainComponent>();

        // Split Pain damage between all eligible targets.
        foreach (var ent in args.HitEntities)
        {
            if (!stamQuery.TryGetComponent(ent, out var stam)) continue;
            toHit.Add(stam);
        }

        foreach (var comp in toHit)
        {
            var oldDamage = comp.PainDamage;
            TakePainDamage(comp.Owner, component.Damage / toHit.Count, comp);
            if (comp.PainDamage.Equals(oldDamage))
            {
                _popup.PopupEntity(Loc.GetString("Pain-resist"), comp.Owner, Filter.Entities(args.User));
            }
        }
    }

    private void OnCollide(EntityUid uid, PainDamageOnCollideComponent component, StartCollideEvent args)
    {
        if (!args.OurFixture.ID.Equals(CollideFixture)) return;

        TakePainDamage(args.OtherFixture.Body.Owner, component.Damage);
    }

    private void SetPainAlert(EntityUid uid, PainComponent? component = null)
    {
        if (!Resolve(uid, ref component, false) || component.Deleted)
        {
            _alerts.ClearAlert(uid, AlertType.Pain);
            return;
        }

        var severity = ContentHelpers.RoundToLevels(MathF.Max(0f, component.CritThreshold - component.PainDamage), component.CritThreshold, 7);
        _alerts.ShowAlert(uid, AlertType.Pain, (short) severity);
    }

    public void TakePainDamage(EntityUid uid, float value, PainComponent? component = null)
    {
        if (!Resolve(uid, ref component, false) || component.Critical) return;

        var oldDamage = component.PainDamage;
        component.PainDamage = MathF.Max(0f, component.PainDamage + value);

        // Reset the decay cooldown upon taking damage.
        if (oldDamage < component.PainDamage)
        {
            component.PainDecayAccumulator = component.DecayCooldown;
        }

        var slowdownThreshold = component.CritThreshold / 2f;

        // If we go above n% then apply slowdown
        if (oldDamage < slowdownThreshold &&
            component.PainDamage > slowdownThreshold)
        {
            _stunSystem.TrySlowdown(uid, TimeSpan.FromSeconds(3), true, 0.8f, 0.8f);
        }

        SetPainAlert(uid, component);

        // Can't do it here as resetting prediction gets cooked.
        _dirtyEntities.Add(uid);

        if (!component.Critical)
        {
            if (component.PainDamage >= component.CritThreshold)
            {
                EnterStamCrit(uid, component);
            }
        }
        else
        {
            if (component.PainDamage < component.CritThreshold)
            {
                ExitStamCrit(uid, component);
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_timing.IsFirstTimePredicted) return;

        _accumulator -= frameTime;

        if (_accumulator > 0f) return;

        var stamQuery = GetEntityQuery<PainComponent>();

        foreach (var uid in _dirtyEntities)
        {
            // Don't need to RemComp as they will get handled below.
            if (!stamQuery.TryGetComponent(uid, out var comp) || comp.PainDamage <= 0f) continue;
            EnsureComp<ActivePainComponent>(uid);
        }

        _dirtyEntities.Clear();
        _accumulator += UpdateCooldown;

        foreach (var active in EntityQuery<ActivePainComponent>())
        {
            // Just in case we have active but not Pain we'll check and account for it.
            if (!stamQuery.TryGetComponent(active.Owner, out var comp) ||
                comp.PainDamage <= 0f)
            {
                RemComp<ActivePainComponent>(active.Owner);
                continue;
            }

            comp.PainDecayAccumulator -= UpdateCooldown;

            if (comp.PainDecayAccumulator > 0f) continue;

            // We were in crit so come out of it and continue.
            if (comp.Critical)
            {
                ExitStamCrit(active.Owner, comp);
                continue;
            }

            comp.PainDecayAccumulator = 0f;
            TakePainDamage(comp.Owner, -comp.Decay * UpdateCooldown, comp);
        }
    }

    private void EnterStamCrit(EntityUid uid, PainComponent? component = null)
    {
        if (!Resolve(uid, ref component) ||
            component.Critical) return;

        // To make the difference between a stun and a stamcrit clear
        // TODO: Mask?

        component.Critical = true;
        component.PainDamage = component.CritThreshold;
        component.PainDecayAccumulator = 0f;

        var stunTime = TimeSpan.FromSeconds(6);
        _stunSystem.TryParalyze(uid, stunTime, true);

        // Give them buffer before being able to be re-stunned
        component.PainDecayAccumulator = (float) stunTime.TotalSeconds + StamCritBufferTime;
    }

    private void ExitStamCrit(EntityUid uid, PainComponent? component = null)
    {
        if (!Resolve(uid, ref component) ||
            !component.Critical) return;

        component.Critical = false;
        component.PainDamage = 0f;
        SetPainAlert(uid, component);
    }
}
