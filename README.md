# Celeste X DGLAB

Everest code mod for **Celeste** that drives **DGLAB Coyote** output strength from in-game events (death, retries, room transitions, strawberries, etc.) via the **Coyote Game Hub** HTTP API.

## Requirements

- **Celeste**
- **Everest** — `EverestCore` **≥ 1.5935.0** (see `everest.yaml`)

## Installation

1. Download or build the mod so you have `bin/Celeste_X_DGLAB.dll` next to `everest.yaml`.
2. Place the mod folder under your Celeste `Mods` directory (same layout as this repo: `everest.yaml` at the root of the mod folder).
3. Enable **Celeste X DGLAB** in the Everest mod options if needed.

## DGLAB / Coyote Game Hub

The mod talks to the Hub over HTTP (e.g. default base URL `http://127.0.0.1:8920`).

1. Run **Coyote Game Hub** so the HTTP API is listening.
2. Open Coyote Game Hub Web UI, connect, copy `Client ID`.
3. In the mod settings:
   - **DGLAB server URL** — base URL of the Hub (e.g. `http://localhost:8920`).
   - **DGLAB client id** — the client id from the Hub (e.g., `48023e65-ad41-4487-9d03-88d4df6be3fe`).

Strength values use the mod’s **0–200** scale and are clamped by **Strength min** / **Strength max** before being sent.

## Behavior overview

| Trigger | Effect |
|--------|--------|
| **Death** | Applies death/retry rule (Add or Set). |
| **Retry / golden restart** (`LevelExit.Restart` / `GoldenBerryRestart`) | Same rule; optional **dedupe** window after a death avoids double-firing. |
| **Room transition** | Optional fixed decrease when entering another room. |
| **Periodic decay** | Every *N* seconds, subtract an amount (runs on level update). |
| **Chapter / area complete** | Optional large decrease when the level completes the area. |
| **Strawberry collected** | Optional decrease; optional **cooldown** where death/retry **does not increase** strength (Add mode blocked; Set mode blocked only if the target would be **higher** than current). Room decay, transitions, map-complete decrease, and strawberry decrease still apply. |

Session strength is stored in **Everest session data** for this mod (`CoyoteStrength`).

## Settings (in-game)

All options live under the mod’s Everest settings menu. Important groups:

- **General** — master **Enabled** toggle.
- **DGLAB HTTP** — server URL and client id.
- **Output strength bounds** — **Strength min** / **Strength max** (mod scale).
- **On death or Retry** — **Add** vs **Set**, amounts, **Death retry dedupe** (ms).
- **On room transition** — subtract per transition (0 = off).
- **Periodic decay** — interval (seconds) and amount per tick.
- **On map complete** — decrease on area clear (0 = off).
- **On strawberry collect** — **Strawberry strength decrease** (0 = off).
- **Strawberry suppress increase seconds** — after picking a berry, block death/retry **increases** for this many seconds (0 = off). Each new berry **resets** this timer from the moment of collection.

## Building from source

Prerequisites: **.NET SDK** compatible with `net8.0`, and a Celeste install with **Everest** so `Celeste.dll`, `FNA.dll`, and **`MMHOOK_Celeste.dll`** exist (paths are resolved via `Celeste_X_DGLAB.csproj` / `CelestePrefix`).
That is, you need a valid Celeste install with **Everest** installed, so `Celeste.dll`, `FNA.dll`, and `MMHOOK_Celeste.dll` exist.

```bash
cd Source
dotnet build -c Release
```

For development:

```bash
# Debug build + Install to Celeste Mods directory
./build.sh

# Release build + Install to Celeste Mods directory
./build.sh -r
```

Outputs go to `Source/bin/Release/net8.0/` and are copied to `bin/` by the project; release builds can package `Celeste_X_DGLAB.zip` when configured.

## License

Released under the [MIT License](LICENSE).

## Credits

- [CelesteMod.Templates](https://github.com/EverestAPI/CelesteModTemplate), MIT License.
