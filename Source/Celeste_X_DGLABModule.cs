using System;

namespace Celeste.Mod.Celeste_X_DGLAB;

public class Celeste_X_DGLABModule : EverestModule {
    public static Celeste_X_DGLABModule Instance { get; private set; }

    public override Type SettingsType => typeof(Celeste_X_DGLABModuleSettings);
    public static Celeste_X_DGLABModuleSettings Settings => (Celeste_X_DGLABModuleSettings) Instance._Settings;

    public override Type SessionType => typeof(Celeste_X_DGLABModuleSession);
    public static Celeste_X_DGLABModuleSession Session => (Celeste_X_DGLABModuleSession) Instance._Session;

    public override Type SaveDataType => typeof(Celeste_X_DGLABModuleSaveData);
    public static Celeste_X_DGLABModuleSaveData SaveData => (Celeste_X_DGLABModuleSaveData) Instance._SaveData;

    private CoyoteStrengthManager _coyote;

    public Celeste_X_DGLABModule() {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(Celeste_X_DGLABModule), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(Celeste_X_DGLABModule), LogLevel.Info);
#endif
    }

    public override void Load() {
        _coyote = new CoyoteStrengthManager();
        _coyote.Load();
    }

    public override void Unload() {
        _coyote?.Unload();
        _coyote = null;
    }
}