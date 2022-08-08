using JetBrains.Annotations;
using Content.Shared.Movement.Components;
using Content.Shared.Alert;
using Content.Shared.Movement.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Players;
using Content.Shared.Standing;
using Robust.Shared.Serialization;

namespace Content.Shared.Stamina
{
    [UsedImplicitly]
    public abstract class SharedStaminaSystem : EntitySystem
    {
        [Dependency] private readonly AlertsSystem _alerts = default!;
        [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
        [Dependency] private readonly SharedJetpackSystem _jetpack = default!;
        [Dependency] private readonly SharedContainerSystem _container = default!;
        [Dependency] private readonly StandingStateSystem _standing = default!;
        [Dependency] private readonly SharedPhysicsSystem _phys = default!;

        public ISawmill Sawmill = default!;
        private float _accumulatedFrameTime;
        private float _sliderFrameTime;
        private HashSet<SharedStaminaComponent> _slidingComponents = new HashSet<SharedStaminaComponent>();


        public override void Initialize()
        {
            base.Initialize();

            Sawmill = Logger.GetSawmill("stamina");
            SubscribeLocalEvent<SharedStaminaComponent, ComponentStartup>(OnComponentStartup);
            SubscribeLocalEvent<SharedStaminaComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);


        }
        public virtual void OnComponentStartup(EntityUid uid, SharedStaminaComponent component, ComponentStartup args)
        {
            component.CurrentStamina = component.StaminaThresholds[StaminaThreshold.Normal];
            component.CurrentStaminaThreshold = StaminaThreshold.Normal;
            // Necesarry for proper sliding
            EnsureComp<MovementIgnoreGravityComponent>(component.Owner);

        }

        #region ComponentState and Event
        [Serializable, NetSerializable]
        public sealed class StaminaComponentState : ComponentState
        {
            public float CurrentStamina;
            public bool CanSlide;
            public byte SlideCost;
            public float ActualRegenRate;
            public bool Stimulated;

            public StaminaComponentState(float currentStamina, bool canSlide, byte slideCost, float actualRegenRate, bool stimulated)
            {
                CurrentStamina = currentStamina;
                CanSlide = canSlide;
                SlideCost = slideCost;
                ActualRegenRate = actualRegenRate;
                Stimulated = stimulated;
            }


        }


        [Serializable, NetSerializable]
        public sealed class StaminaSlideEvent : EntityEventArgs
        {
            public EntityCoordinates Coords;
            public StaminaSlideEvent(EntityCoordinates coords)
            {
                Coords = coords;
            }
        }
        #endregion

        private void OnRefreshMovespeed(EntityUid uid, SharedStaminaComponent component, RefreshMovementSpeedModifiersEvent args)
        {
            if (_jetpack.IsUserFlying(component.Owner))
                return;

            var mod = component.CurrentStaminaThreshold <= StaminaThreshold.Collapsed ? 0.7f : (component.CurrentStaminaThreshold == StaminaThreshold.Tired ? 0.9f : 1f);
            args.ModifySpeed(mod, mod);
        }

        public virtual bool HandleSlideAttempt(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
        {
            if (TryComp(session?.AttachedEntity, out SharedStaminaComponent? stam) && stam.CanSlide)
            {
                if (_jetpack.IsUserFlying(stam.Owner) || _container.IsEntityInContainer(stam.Owner))
                    return false;
                if (TryComp(stam.Owner, out PhysicsComponent? physics) && TryComp(stam.Owner, out StandingStateComponent? state)
                    && TryComp(stam.Owner, out MovementIgnoreGravityComponent? grav) && TryComp(stam.Owner, out SharedPlayerInputMoverComponent? input))
                {
                    // too little to slide.
                    if ((Math.Abs(physics.LinearVelocity.X) + Math.Abs(physics.LinearVelocity.Y)) < 2f)
                        return false;
                    _phys.SetLinearVelocity(physics, physics.LinearVelocity * 4);
                    physics.LinearDamping += 1.5f;
                    physics.BodyType = Robust.Shared.Physics.BodyType.Dynamic; // Necesarry for linear dampening to be applied
                    stam.SlideTime = (Math.Abs(physics.LinearVelocity.X) + Math.Abs(physics.LinearVelocity.Y)) / 16;
                    // no moving !!
                    input.CanMove = false;
                    _standing.Down(stam.Owner, true, false, state);
                    UpdateStamina(stam, stam.SlideCost);
                    grav.Weightless = true;
                    _slidingComponents.Add(stam);
                    return true;
                }

                return false;
            }
            return false;
        }


        public void UpdateEffects(SharedStaminaComponent component)
        {
            short alertSeverity = (short) ((int) component.StaminaThresholds[component.CurrentStaminaThreshold] / 250);
            _alerts.ShowAlert(component.Owner, AlertType.Stamina, alertSeverity);
        }

        public bool IsSliding(SharedStaminaComponent component)
        {
            if (_slidingComponents.Contains(component))
                return true;
            return false;
        }

        public StaminaThreshold GetStaminaThreshold(SharedStaminaComponent component, float amount)
        {
            StaminaThreshold result = StaminaThreshold.Overcharged;
            var value = component.StaminaThresholds[StaminaThreshold.Collapsed];
            foreach (var threshold in component.StaminaThresholds)
            {
                if (threshold.Value <= value && threshold.Value >= amount)
                {
                    result = threshold.Key;
                    value = threshold.Value;
                }
            }

            return result;

        }
        public void UpdateStamina(SharedStaminaComponent component, float amount)
        {
            component.CurrentStamina = Math.Clamp(component.CurrentStamina + amount, 0f, component.StaminaThresholds[StaminaThreshold.Collapsed]);
            StaminaThreshold last = component.CurrentStaminaThreshold;
            component.CurrentStaminaThreshold = GetStaminaThreshold(component, component.CurrentStamina);
            if (component.CurrentStaminaThreshold != component.LastStaminaThreshold)
            {
                UpdateEffects(component);
                component.LastStaminaThreshold = component.CurrentStaminaThreshold;
            }

        }

        public override void Update(float frameTime)
        {
            _accumulatedFrameTime += frameTime;
            _sliderFrameTime += frameTime;

            if (_sliderFrameTime > 0.5)
            {

                foreach (SharedStaminaComponent slidingStamina in _slidingComponents)
                {
                    slidingStamina.SlideTime -= _sliderFrameTime;
                    if (slidingStamina.SlideTime < 0)
                    {
                        if (TryComp(slidingStamina.Owner, out MovementIgnoreGravityComponent? gravity) && TryComp(slidingStamina.Owner, out StandingStateComponent? state) &&
                           TryComp(slidingStamina.Owner, out PhysicsComponent? physics) && TryComp(slidingStamina.Owner, out SharedPlayerInputMoverComponent? input))
                        {
                            gravity.Weightless = false;
                            _standing.Stand(state.Owner, state);
                            physics.BodyType = Robust.Shared.Physics.BodyType.KinematicController;
                            physics.LinearDamping -= 1.5f;
                            input.CanMove = true;
                            _movement.RefreshMovementSpeedModifiers(slidingStamina.Owner);

                        }

                    }
                }

                _slidingComponents.RemoveWhere((x) =>
                {
                    return x.SlideTime < 0 ? true : false;
                });

                _sliderFrameTime = 0;
            }

            if (_accumulatedFrameTime > 1)
            {
                foreach (var component in EntityManager.EntityQuery<SharedStaminaComponent>())
                {
                    if (component.CurrentStamina < component.StaminaThresholds[StaminaThreshold.Normal] && !component.Stimulated)
                    {
                        UpdateStamina(component, component.ActualRegenRate);
                        continue;
                    }
                    UpdateStamina(component, -component.ActualRegenRate);

                }
                _accumulatedFrameTime -= 1;
            }
        }
    }
}
