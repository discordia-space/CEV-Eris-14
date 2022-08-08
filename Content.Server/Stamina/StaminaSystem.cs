using JetBrains.Annotations;
using Robust.Shared.Random;
using Content.Shared.Movement.Components;
using Content.Shared.Alert;
using Content.Shared.Movement.Systems;
using Content.Shared.Input;
using Robust.Shared.Containers;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Players;
using Robust.Shared.Timing;
using Content.Shared.MobState.Components;
using Content.Server.MobState;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffect;
using Robust.Shared.GameObjects;
using Content.Shared.Movement;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Gravity;
using Content.Server.Gravity;
using Content.Shared.Stamina;
using Robust.Shared.Serialization;
using Robust.Shared.GameStates;
using Content.Shared.Stamina;

namespace Content.Server.Stamina
{
    [UsedImplicitly]
    public sealed class StaminaSystem : SharedStaminaSystem
    {
        [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
        [Dependency] private readonly StandingStateSystem _standing = default!;


        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<StaminaComponent, ComponentStartup>(OnComponentStartup);
            SubscribeLocalEvent<SharedStaminaComponent, ComponentGetState>(GetCompState);
            SubscribeNetworkEvent<StaminaSlideEvent>(OnStaminaUpdate);

        }

        private void GetCompState(EntityUid uid, SharedStaminaComponent component, ref ComponentGetState args)
        {
            _sawmill.Warning("Update sent for client-stamina");
            args.State = new StaminaComponentState(
                component.CurrentStamina,
                component.CanSlide,
                component.SlideCost,
                component.ActualRegenRate,
                component.Stimulated);
        }


        private void OnStaminaUpdate(StaminaSlideEvent message, EntitySessionEventArgs eventArgs)
        {
            if (TryComp(eventArgs.SenderSession.AttachedEntity, out StaminaComponent? stam))
            {
                HandleSlideAttempt(eventArgs.SenderSession, message.Coords, (EntityUid) eventArgs.SenderSession.AttachedEntity);
            }

        }


        public void OnComponentStartup(EntityUid uid, StaminaComponent component, ComponentStartup args)
        {
            base.OnComponentStartup(uid, component, args);
            RefreshRegenRate(component);
        }

        public void UpdateEffects(StaminaComponent component)
        {
            //base.UpdateEffects(component);
            switch (component.CurrentStaminaThreshold)
            {
                case StaminaThreshold.Overcharged:
                    component.BaseRegenRate = 0f;
                    RefreshRegenRate(component);
                    return;

                case StaminaThreshold.Energetic:
                    component.BaseRegenRate = 2.5f;
                    RefreshRegenRate(component);
                    return;

                case StaminaThreshold.Normal:
                    component.BaseRegenRate = 5f;
                    RefreshRegenRate(component);
                    return;
                case StaminaThreshold.Tired:
                    component.BaseRegenRate = 10f;
                    RefreshRegenRate(component);
                    return;

                case StaminaThreshold.Collapsed:
                    component.BaseRegenRate = 25f;
                    RefreshRegenRate(component);
                    return;

                default:
                    _sawmill.Error($"No thirst threshold found for {component.CurrentStaminaThreshold}");
                    throw new ArgumentOutOfRangeException($"No thirst threshold found for {component.CurrentStaminaThreshold}");
            }
        }

        public void ResetStamina(StaminaComponent component)
        {
            component.CurrentStamina = component.StaminaThresholds[StaminaThreshold.Normal];
            component.CurrentStaminaThreshold = StaminaThreshold.Normal;
            component.LastStaminaThreshold = StaminaThreshold.Normal;
            component.Dirty();
        }

        public void RefreshRegenRate(StaminaComponent component)
        {
            component.ActualRegenRate = (component.BaseRegenRate + component.RegenRateAdded) * component.RegenRateMultiplier;
            component.Dirty();
        }

        public override void Update(float frameTime)
        {

            base.Update(frameTime);
            _sliderFrameTime += frameTime;

            if (_sliderFrameTime > 0.1)
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

                            _sawmill.Error("$Trying to remove Slider");

                        }

                    }
                }

                _slidingComponents.RemoveWhere((x) =>
                {
                    if (x.SlideTime < 0f)
                    {
                        _sawmill.Error("$Slider removed");
                        return true;
                    }
                    return false;
                });

                _sliderFrameTime = 0;
            }

        }

    }
}
