using JetBrains.Annotations;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.Stamina;
using Robust.Shared.GameStates;

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
                    Sawmill.Error($"No thirst threshold found for {component.CurrentStaminaThreshold}");
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

    }
}
