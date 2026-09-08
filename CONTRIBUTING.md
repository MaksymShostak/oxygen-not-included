# Contributing

Thanks for helping improve Delivery Temperature Limit (Supercooled).

Please keep discussion respectful, specific, and focused on improving the mod for players and maintainers.

## Choose the right route

- For a player-visible bug, use the [Temperature Limit bug form](https://github.com/MaksymShostak/oxygen-not-included/issues/new?template=temperature-limit-bug.yml) and the automated report flow in [SUPPORT.md](SUPPORT.md).
- For a feature idea, use the [feature form](https://github.com/MaksymShostak/oxygen-not-included/issues/new?template=temperature-limit-feature.yml) and describe the player problem and desired experience.
- For translations, follow the [Translation Guide](docs/guides/translating-delivery-temperature-limit-supercooled.md) and use the template at [`mods/delivery-temperature-limit-supercooled/translations/delivery_temperature_limit.pot`](mods/delivery-temperature-limit-supercooled/translations/delivery_temperature_limit.pot).
- For code or documentation, follow the [repository SDLC](docs/sdlc/howto.md): an accepted task or normal PR brief suffices for R0/R1; R2/R3 require a previously accepted baseline. Keep scope and compatibility expectations visible.

## Set up and validate a checkout

Set up repository controls using the [SDLC guide](docs/sdlc/howto.md). For mod work, follow the existing [getting-started](docs/guides/getting-started-with-oni-mod-pipeline.md) and [development](docs/guides/developing-oni-mods.md) guides. ONI Mod Pipeline is the repository's supported build, test, install, and release path. SDLC-only changes need the control/setup suites; they do not require game installation or in-game scenarios.

Run these commands from `mods/delivery-temperature-limit-supercooled`:

```text
oni-mod-pipeline diagnose
oni-mod-pipeline validate
oni-mod-pipeline build
oni-mod-pipeline test
```

`build` prints one exact `build-result.json` path. Paste that printed path when PowerShell prompts, then install that named result:

```powershell
$buildResultPath = Read-Host 'Paste the exact build-result.json path printed by build'
oni-mod-pipeline install --mod . --build-result $buildResultPath --target dev
```

Never select a result by timestamp, directory ordering, or a "latest" convention. Build again after source changes instead of reusing or editing an older result.

## Make a focused change

Keep each change focused on one agreed player or maintainer outcome. Preserve unrelated working-tree changes and use the adapted TDD procedure for changed behavior; use preservation checks for working imported code. Keep mod production source compatible with the repository's C# 8 ceiling. Configuration changes need the approval described in AGENTS.md; an existing approval remains valid within its scope.

## Prepare a pull request

Use the PR template's seven SDLC metadata fields and explain the accepted task or Issue, rationale, change and fresh affected test evidence. Include in-game ONI evidence when relevant. Describe compatibility, performance/allocation, save/persistence, UI, localization and documentation impact where applicable. Release changes must follow [Preparing ONI mod releases](docs/guides/preparing-oni-mod-releases.md).
