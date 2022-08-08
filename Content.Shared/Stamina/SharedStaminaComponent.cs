using Content.Shared.Alert;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;
using Robust.Shared.Prototypes;
using Content.Shared.Actions.ActionTypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;



namespace Content.Shared.Stamina
{
    [Flags]
    public enum StaminaThreshold : byte
    {
        Collapsed = 0,
        Tired = 1 << 0,
        Normal = 1 << 1,
        Energetic = 1 << 2,
        Overcharged = 1 << 3,
    }

    [NetworkedComponent]
    public abstract class SharedStaminaComponent : Component
    {
        [ViewVariables(VVAccess.ReadWrite)]
        public StaminaThreshold CurrentStaminaThreshold;

        [ViewVariables(VVAccess.ReadWrite)]
        public StaminaThreshold LastStaminaThreshold;

        [ViewVariables(VVAccess.ReadWrite)]
        public float CurrentStamina;

        [ViewVariables(VVAccess.ReadWrite)]
        public float ActualRegenRate;

        // controls if the stamina can go to levels of energetic or overchargd
        [ViewVariables(VVAccess.ReadWrite)]
        public bool Stimulated = false;

        [DataField("slideCost")]
        public byte SlideCost = 200;

        [DataField("canSlide")]
        public bool CanSlide = true;

        [ViewVariables(VVAccess.ReadOnly)]
        public float SlideTime = 0f;

        public Dictionary<StaminaThreshold, float> StaminaThresholds { get; } = new()
        {
            { StaminaThreshold.Collapsed, 1000.0f },
            { StaminaThreshold.Tired, 750.0f },
            { StaminaThreshold.Normal, 500.0f },
            { StaminaThreshold.Energetic, 250.0f },
            { StaminaThreshold.Overcharged, 0.0f },
        };




    }
}
