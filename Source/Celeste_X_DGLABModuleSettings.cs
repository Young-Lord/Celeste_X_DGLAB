namespace Celeste.Mod.Celeste_X_DGLAB;

public enum DeathRetryStrengthMode {
    Add,
    Set,
}

public class Celeste_X_DGLABModuleSettings : EverestModuleSettings {

    public const int DGLABClientIdMaxLength = 36;

    [SettingSubHeader("Celeste X DGLAB — General")]
    public bool Enabled { get; set; } = true;

    [SettingSubHeader("DGLAB HTTP (Coyote Game Hub)")]
    [SettingSubText("Base URL, e.g. http://127.0.0.1:8920 — same as inject_th06.py --dglab-server")]
    public string DGLABServerUrl { get; set; } = "http://localhost:8920";

    [SettingMaxLength(36)]
    [SettingSubText("Optional. Empty = no Hub HTTP sync. Otherwise client id from Coyote Game Hub (at most 36 characters).")]
    public string DGLABClientId { get; set; } = "";

    [SettingSubHeader("Output strength bounds (mod scale)")]
    [SettingRange(0, 200, true)]
    public int StrengthMin { get; set; } = 0;

    [SettingRange(0, 200, true)]
    public int StrengthMax { get; set; } = 60;

    [SettingSubHeader("On death or Retry (LevelExit.Restart)")]
    [SettingSubText("Add: add to current strength. Set: set strength to a fixed value.")]
    public DeathRetryStrengthMode DeathRetryMode { get; set; } = DeathRetryStrengthMode.Add;

    [SettingRange(0, 200, true)]
    public int DeathRetryAddAmount { get; set; } = 15;

    [SettingRange(0, 200, true)]
    public int DeathRetrySetValue { get; set; } = 50;

    [SettingRange(0, 60000, true)]
    [SettingSubText("Skip the second trigger if death and Restart fire close together (ms). 0 = no dedupe.")]
    public int DeathRetryDedupeMilliseconds { get; set; } = 1000;

    [SettingSubHeader("On room transition")]
    [SettingRange(0, 500, true)]
    [SettingSubText("Subtract this much when entering another room (0 = off).")]
    public int RoomTransitionDecrease { get; set; } = 0;

    [SettingSubHeader("Periodic decay")]
    [SettingRange(1, 600, true)]
    [SettingSubText("Every N seconds…")]
    public int PeriodicDecayIntervalSeconds { get; set; } = 20;

    [SettingRange(1, 100, true)]
    [SettingSubText("…subtract this amount (1 = minus 1 per interval).")]
    public int PeriodicDecayAmount { get; set; } = 1;

    [SettingSubHeader("On map complete (area clear)")]
    [SettingRange(0, 9999, true)]
    public int MapCompleteDecrease { get; set; } = 999;

    [SettingSubHeader("On strawberry collect")]
    [SettingRange(0, 9999, true)]
    [SettingSubText("Subtract this much when collecting a strawberry (0 = off).")]
    public int StrawberryStrengthDecrease { get; set; } = 999;

    [SettingRange(0, 3600, true)]
    [SettingSubText("After collecting a strawberry, block strength increases from death/retry for this many seconds (0 = off).")]
    public int StrawberrySuppressIncreaseSeconds { get; set; } = 180;
}
