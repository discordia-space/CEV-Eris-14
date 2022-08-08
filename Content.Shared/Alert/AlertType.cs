namespace Content.Shared.Alert
{
    /// <summary>
    /// Every kind of alert. Corresponds to alertType field in alert prototypes defined in YML
    /// NOTE: Using byte for a compact encoding when sending this in messages, can upgrade
    /// to ushort
    /// </summary>
    public enum AlertType : byte
    {
        Error,
        LowOxygen,
        LowPressure,
        HighPressure,
        Fire,
        Cold,
        Hot,
        Weightless,
        Pain,
        Stun,
        Handcuffed,
        Buckled,
        HumanCrit,
        HumanDead,
        HumanHealth,
        PilotingShuttle,
        Overfed,
        Peckish,
        Starving,
        Overhydrated,
        Thirsty,
        Parched,
        Pain,
        Stamina,
        Pulled,
        Pulling,
        Magboots,
        Toxins,
        Muted,
        VowOfSilence,
        VowBroken,
        Debug1,
        Debug2,
        Debug3,
        Debug4,
        Debug5,
        Debug6
    }

}
