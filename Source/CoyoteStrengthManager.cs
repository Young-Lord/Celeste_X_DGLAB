using System;
using System.Threading.Tasks;
using Celeste;
using Monocle;

// MMHOOK-generated hooks; keep full qualification to avoid clashing with Celeste types.
using OnStrawberryOnCollect = global::On.Celeste.Strawberry.orig_OnCollect;

namespace Celeste.Mod.Celeste_X_DGLAB;

/// <summary>
/// Maps Celeste events to Coyote strength updates and syncs to DG-Lab HTTP API.
/// </summary>
internal sealed class CoyoteStrengthManager {
    internal static CoyoteStrengthManager Instance { get; private set; }

    private readonly DGLABCoyoteClient _client = new();
    private float _periodicSecondsAccumulator;
    private bool _lastDeathRetryWasDie;
    private long _lastDeathRetryUtcTicks;
    private long _nextInvalidClientIdLogUtcTicks;
    private long _strawberrySuppressIncreasesUntilUtcTicks;

    public void Load() {
        Instance = this;
        Everest.Events.Player.OnDie += OnPlayerDie;
        Everest.Events.Level.OnExit += OnLevelExit;
        Everest.Events.Level.OnTransitionTo += OnLevelTransitionTo;
        Everest.Events.Level.OnComplete += OnLevelComplete;
        Everest.Events.Level.OnAfterUpdate += OnLevelAfterUpdate;
        Everest.Events.Level.OnLoadLevel += OnLoadLevel;
        global::On.Celeste.Strawberry.OnCollect += StaticOnStrawberryCollect;
    }

    public void Unload() {
        Everest.Events.Player.OnDie -= OnPlayerDie;
        Everest.Events.Level.OnExit -= OnLevelExit;
        Everest.Events.Level.OnTransitionTo -= OnLevelTransitionTo;
        Everest.Events.Level.OnComplete -= OnLevelComplete;
        Everest.Events.Level.OnAfterUpdate -= OnLevelAfterUpdate;
        Everest.Events.Level.OnLoadLevel -= OnLoadLevel;
        global::On.Celeste.Strawberry.OnCollect -= StaticOnStrawberryCollect;
        _client.Dispose();
        Instance = null;
    }

    private static Celeste_X_DGLABModuleSettings S => Celeste_X_DGLABModule.Settings;
    private static Celeste_X_DGLABModuleSession St => Celeste_X_DGLABModule.Session;

    private void OnLoadLevel(Level level, Player.IntroTypes playerIntro, bool isFromLoader) {
        _lastDeathRetryWasDie = false;
        _periodicSecondsAccumulator = 0f;
    }

    private static void StaticOnStrawberryCollect(OnStrawberryOnCollect orig, Strawberry self) {
        orig(self);
        Instance?.AfterStrawberryCollect();
    }

    private void AfterStrawberryCollect() {
        if (!S.Enabled) {
            return;
        }

        if (S.StrawberryStrengthDecrease > 0) {
            ApplyDecrease(S.StrawberryStrengthDecrease);
        }

        var sec = S.StrawberrySuppressIncreaseSeconds;
        if (sec > 0) {
            _strawberrySuppressIncreasesUntilUtcTicks = DateTime.UtcNow.Ticks + sec * TimeSpan.TicksPerSecond;
        }
    }

    private void OnLevelAfterUpdate(Level level) {
        if (!S.Enabled) {
            return;
        }

        var interval = Math.Max(1, S.PeriodicDecayIntervalSeconds);
        _periodicSecondsAccumulator += Engine.DeltaTime;
        while (_periodicSecondsAccumulator >= interval) {
            _periodicSecondsAccumulator -= interval;
            ApplyPeriodicDecay();
        }
    }

    private void OnPlayerDie(Player player) {
        if (!S.Enabled) {
            return;
        }

        ApplyDeathOrRetry();
        _lastDeathRetryWasDie = true;
        _lastDeathRetryUtcTicks = DateTime.UtcNow.Ticks;
    }

    private void OnLevelExit(Level level, LevelExit exit, LevelExit.Mode mode, Session session, HiresSnow snow) {
        if (!S.Enabled) {
            return;
        }

        // Pause/chapter Restart and golden-berry restart both leave via Restart-like exits.
        if (mode != LevelExit.Mode.Restart && mode != LevelExit.Mode.GoldenBerryRestart) {
            return;
        }

        var elapsedMs = (DateTime.UtcNow.Ticks - _lastDeathRetryUtcTicks) / TimeSpan.TicksPerMillisecond;
        if (_lastDeathRetryWasDie && elapsedMs < S.DeathRetryDedupeMilliseconds) {
            _lastDeathRetryWasDie = false;
            return;
        }

        ApplyDeathOrRetry();
        _lastDeathRetryWasDie = false;
        _lastDeathRetryUtcTicks = DateTime.UtcNow.Ticks;
    }

    private void OnLevelTransitionTo(Level level, LevelData next, Microsoft.Xna.Framework.Vector2 direction) {
        if (!S.Enabled || S.RoomTransitionDecrease == 0) {
            return;
        }

        ApplyDecrease(S.RoomTransitionDecrease);
    }

    private void OnLevelComplete(Level level) {
        if (!S.Enabled || S.MapCompleteDecrease == 0) {
            return;
        }

        ApplyDecrease(S.MapCompleteDecrease);
    }

    private void ApplyDeathOrRetry() {
        if (ShouldSuppressDeathRetryIncrease()) {
            return;
        }

        int v = St.CoyoteStrength;
        if (S.DeathRetryMode == DeathRetryStrengthMode.Add) {
            v += S.DeathRetryAddAmount;
        } else {
            v = S.DeathRetrySetValue;
        }

        CommitStrength(v);
    }

    private bool ShouldSuppressDeathRetryIncrease() {
        if (DateTime.UtcNow.Ticks >= _strawberrySuppressIncreasesUntilUtcTicks) {
            return false;
        }

        if (S.DeathRetryMode == DeathRetryStrengthMode.Add) {
            return S.DeathRetryAddAmount > 0;
        }

        return S.DeathRetrySetValue > St.CoyoteStrength;
    }

    private void ApplyPeriodicDecay() {
        if (!S.Enabled || S.PeriodicDecayAmount == 0) {
            return;
        }

        ApplyDecrease(S.PeriodicDecayAmount);
    }

    private void ApplyDecrease(int amount) {
        int v = St.CoyoteStrength - amount;
        CommitStrength(v);
    }

    private void CommitStrength(int value) {
        int min = Math.Min(S.StrengthMin, S.StrengthMax);
        int max = Math.Max(S.StrengthMin, S.StrengthMax);
        value = Calc.Clamp(value, min, max);
        St.CoyoteStrength = value;

        Logger.Log(LogLevel.Verbose, "Celeste_X_DGLAB", $"Coyote strength (mod scale) -> {value}");

        var hubId = (S.DGLABClientId ?? "").Trim();
        if (hubId.Length == 0) {
            return;
        }
        if (hubId.Length > Celeste_X_DGLABModuleSettings.DGLABClientIdMaxLength) {
            var now = DateTime.UtcNow.Ticks;
            if (now >= _nextInvalidClientIdLogUtcTicks) {
                _nextInvalidClientIdLogUtcTicks = now + 8L * TimeSpan.TicksPerSecond;
                Logger.Log(
                    LogLevel.Warn,
                    "Celeste_X_DGLAB",
                    $"DGLABClientId too long (max {Celeste_X_DGLABModuleSettings.DGLABClientIdMaxLength} chars). No HTTP sync.");
            }
            return;
        }

        _client.Configure(S.DGLABServerUrl, hubId);
        _ = Task.Run(async () => {
            try {
                if (!_client.Connected) {
                    await _client.ConnectAsync().ConfigureAwait(false);
                }
                _client.SetStrengthFireAndForget(St.CoyoteStrength);
            } catch {
                // ignored
            }
        });
    }
}
