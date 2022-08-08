
using Content.Shared.Stamina;
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
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.StatusEffect;
using Robust.Shared.GameObjects;
using Content.Shared.Movement;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Gravity;
using Robust.Shared.Serialization;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Client.Stamina
{
    public sealed class StaminaSystem : SharedStaminaSystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly StandingStateSystem _standing = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<SharedStaminaComponent, ComponentHandleState>(HandleCompState);
            //UpdatesOutsidePrediction = false;

            CommandBinds.Builder
                .Bind(ContentKeyFunctions.Slide, new PointerInputCmdHandler(HandleSlideAttempt))
                .Register<SharedStaminaSystem>();
        }

        private void HandleCompState(EntityUid uid, SharedStaminaComponent component, ref ComponentHandleState args)
        {
            _sawmill.Warning("Update received for client-stamina");
            if (args.Current is not StaminaComponentState state) return;
            component.CurrentStamina = state.CurrentStamina;
            component.CanSlide = state.CanSlide;
            component.SlideCost = state.SlideCost;
            component.ActualRegenRate = state.ActualRegenRate;
            component.Stimulated = state.Stimulated;

        }

        public override bool HandleSlideAttempt(ICommonSession? session, EntityCoordinates coords, EntityUid uid)
        {
            if (base.HandleSlideAttempt(session, coords, uid))
            {
                RaiseNetworkEvent(new StaminaSlideEvent(coords));
                return true;
            }
            return false;
        }

        public override void Update(float frameTime)
        {

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
            // Can't predict this part since it unsynchronizes.
            if (_timing.IsFirstTimePredicted)
                base.Update(frameTime);

        }
    }


}
