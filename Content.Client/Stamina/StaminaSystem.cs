using Content.Shared.Stamina;
using Content.Shared.Movement.Components;
using Content.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Robust.Shared.Players;
using Robust.Shared.Timing;
using Content.Shared.Standing;
using Robust.Shared.GameStates;

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
            // Can't predict this part since it unsynchronizes.
            if (_timing.IsFirstTimePredicted)
                base.Update(frameTime);

        }
    }
}
