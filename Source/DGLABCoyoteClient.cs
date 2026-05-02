using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Celeste.Mod.Celeste_X_DGLAB;

/// <summary>
/// HTTP client for DG-Lab Coyote Game Hub (same API as inject_th06.py DGLABClient).
/// Uses absolute URIs only — HttpClient.BaseAddress must not change after the first request.
/// </summary>
internal sealed class DGLABCoyoteClient : IDisposable {
    private const string LogTag = "Celeste_X_DGLAB";
    private const double MinStrengthPushIntervalSeconds = 0.1;
    private static readonly long ConnectFailLogCooldownTicks = (long) (4.0 * TimeSpan.TicksPerSecond);
    private static readonly long SetStrengthSkipLogCooldownTicks = (long) (5.0 * TimeSpan.TicksPerSecond);

    private readonly HttpClient _http = new() {
        Timeout = TimeSpan.FromSeconds(8),
    };

    private readonly object _lock = new();
    private string _serverUrl = "http://localhost:8920";
    private string _clientId = "";
    private bool _connected;
    private long _lastStrengthPushUtcTicks;
    private int? _lastStrengthSent;
    private long _nextConnectFailLogUtcTicks;
    private long _nextSetStrengthSkipLogUtcTicks;
    private bool _loggedConnectSuccess;

    public string ClientId => _clientId;
    public bool Connected => _connected;

    public void Configure(string serverUrl, string clientId) {
        var nu = (serverUrl ?? "http://localhost:8920").TrimEnd('/');
        var nid = clientId?.Trim() ?? "";
        if (nu != _serverUrl || nid != _clientId) {
            _serverUrl = nu;
            _clientId = nid;
            _connected = false;
            _loggedConnectSuccess = false;
            lock (_lock) {
                _lastStrengthSent = null;
            }
            Logger.Log(LogLevel.Verbose, LogTag, $"DGLAB Configure: server={_serverUrl}, clientId length={_clientId.Length}");
        }
    }

    private Uri BuildUri(string relativePath) {
        relativePath = relativePath.TrimStart('/');
        var root = _serverUrl.TrimEnd('/') + "/";
        return new Uri(new Uri(root, UriKind.Absolute), relativePath);
    }

    public async Task<bool> ConnectAsync(CancellationToken ct = default) {
        try {
            if (string.IsNullOrEmpty(_clientId)) {
                return false;
            }
            if (_clientId.Length > Celeste_X_DGLABModuleSettings.DGLABClientIdMaxLength) {
                LogConnectFailureThrottled(
                    $"DGLABClientId too long (max {Celeste_X_DGLABModuleSettings.DGLABClientIdMaxLength} characters).");
                return false;
            }

            using var info = await ApiGetAsync("api/server_info", ct).ConfigureAwait(false);
            if (info == null) {
                LogConnectFailureThrottled("GET /api/server_info: no body or non-success");
                return false;
            }
            if (TryGetIntStatus(info) != 1) {
                LogJsonFailureThrottled("server_info", info, "status != 1");
                return false;
            }

            _connected = true;
            if (!_loggedConnectSuccess) {
                _loggedConnectSuccess = true;
                Logger.Log(LogLevel.Info, LogTag, $"DGLAB: HTTP session OK (server={_serverUrl}, clientId={_clientId}). Device must join this clientId in Coyote Game Hub or pushes will fail.");
            }
            return true;
        } catch (Exception ex) {
            LogConnectFailureThrottled($"ConnectAsync exception: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sets Coyote base strength (clamped 0–200 on wire, like Python).
    /// </summary>
    public void SetStrengthFireAndForget(int strength) {
        if (string.IsNullOrEmpty(_clientId)) {
            return;
        }
        if (!_connected) {
            LogSetStrengthSkipThrottled($"skipped: not connected");
            return;
        }

        strength = Math.Clamp(strength, 0, 200);
        var now = DateTime.UtcNow.Ticks;
        lock (_lock) {
            if ((now - _lastStrengthPushUtcTicks) / (double) TimeSpan.TicksPerSecond < MinStrengthPushIntervalSeconds) {
                return;
            }
            if (_lastStrengthSent == strength) {
                return;
            }
            _lastStrengthPushUtcTicks = now;
        }

        _ = Task.Run(async () => {
            try {
                var ok = await ApiPostStrengthAsync(strength, CancellationToken.None).ConfigureAwait(false);
                if (ok) {
                    lock (_lock) {
                        _lastStrengthSent = strength;
                    }
                    Logger.Log(LogLevel.Verbose, LogTag, $"DGLAB: POST strength set={strength} OK");
                }
            } catch (Exception ex) {
                Logger.Log(LogLevel.Warn, LogTag, $"DGLAB: POST strength exception: {ex.Message}");
            }
        });
    }

    private async Task<bool> ApiPostStrengthAsync(int strength, CancellationToken ct) {
        var path = $"api/v2/game/{Uri.EscapeDataString(_clientId)}/strength";
        var payload = new {
            strength = new { set = strength },
        };
        using var content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");
        using var resp = await _http.PostAsync(BuildUri(path), content, ct).ConfigureAwait(false);
        var body = await SafeReadBody(resp, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) {
            Logger.Log(LogLevel.Warn, LogTag, $"POST strength HTTP {(int) resp.StatusCode} {resp.ReasonPhrase}: {Truncate(body, 200)}");
            return false;
        }
        JsonDocument doc;
        try {
            doc = JsonDocument.Parse(body);
        } catch (Exception ex) {
            Logger.Log(LogLevel.Warn, LogTag, $"POST strength: invalid JSON ({ex.Message}) body={Truncate(body, 160)}");
            return false;
        }
        using (doc) {
            if (TryGetIntStatus(doc) != 1) {
                TryGetErrorFields(doc, out var code, out var msg);
                Logger.Log(LogLevel.Warn, LogTag, $"POST strength API status!=1 code={code} message={msg}");
                if (code == "ERR::GAME_NOT_FOUND" || code.Contains("GAME_NOT_FOUND", StringComparison.Ordinal)) {
                    Logger.Log(LogLevel.Warn, LogTag, "DGLAB: no active game session for this clientId — open Coyote Game Hub and connect the device to this client id.");
                }
                return false;
            }
        }
        return true;
    }

    private async Task<JsonDocument> ApiGetAsync(string relativePath, CancellationToken ct) {
        using var resp = await _http.GetAsync(BuildUri(relativePath), ct).ConfigureAwait(false);
        var body = await SafeReadBody(resp, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode) {
            Logger.Log(LogLevel.Verbose, LogTag, $"GET {relativePath} HTTP {(int) resp.StatusCode}: {Truncate(body, 200)}");
            return null;
        }
        if (string.IsNullOrEmpty(body)) {
            return null;
        }
        try {
            return JsonDocument.Parse(body);
        } catch (Exception ex) {
            Logger.Log(LogLevel.Warn, LogTag, $"GET {relativePath}: invalid JSON ({ex.Message}) body={Truncate(body, 160)}");
            return null;
        }
    }

    private static async Task<string> SafeReadBody(HttpResponseMessage resp, CancellationToken ct) {
        try {
            return await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false) ?? "";
        } catch {
            return "";
        }
    }

    private void LogConnectFailureThrottled(string detail) {
        var now = DateTime.UtcNow.Ticks;
        if (now < _nextConnectFailLogUtcTicks) {
            return;
        }
        _nextConnectFailLogUtcTicks = now + ConnectFailLogCooldownTicks;
        Logger.Log(LogLevel.Warn, LogTag, "DGLAB: " + detail);
    }

    private void LogJsonFailureThrottled(string which, JsonDocument doc, string hint) {
        TryGetErrorFields(doc, out var code, out var msg);
        LogConnectFailureThrottled($"{which}: {hint} code={code} message={msg}");
    }

    private void LogSetStrengthSkipThrottled(string detail) {
        var now = DateTime.UtcNow.Ticks;
        if (now < _nextSetStrengthSkipLogUtcTicks) {
            return;
        }
        _nextSetStrengthSkipLogUtcTicks = now + SetStrengthSkipLogCooldownTicks;
        Logger.Log(LogLevel.Warn, LogTag, "DGLAB: SetStrength " + detail);
    }

    private static void TryGetErrorFields(JsonDocument doc, out string code, out string msg) {
        code = "";
        msg = "";
        if (doc.RootElement.TryGetProperty("code", out var c)) {
            code = c.ValueKind == JsonValueKind.String ? (c.GetString() ?? "") : c.ToString();
        }
        if (doc.RootElement.TryGetProperty("message", out var m)) {
            msg = m.ValueKind == JsonValueKind.String ? (m.GetString() ?? "") : m.ToString();
        }
    }

    private static int TryGetIntStatus(JsonDocument doc) {
        if (!doc.RootElement.TryGetProperty("status", out var st)) {
            return -1;
        }
        return st.ValueKind switch {
            JsonValueKind.Number => st.GetInt32(),
            JsonValueKind.String => int.TryParse(st.GetString(), out var n) ? n : -1,
            _ => -1,
        };
    }

    private static string Truncate(string s, int max) {
        if (string.IsNullOrEmpty(s) || s.Length <= max) {
            return s;
        }
        return s.Substring(0, max) + "...";
    }

    public void Dispose() {
        _http.Dispose();
    }
}
