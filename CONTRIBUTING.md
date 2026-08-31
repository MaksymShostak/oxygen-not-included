# Contributing

Thanks for helping improve Delivery Temperature Limit (Supercooled).

Please keep discussion respectful, specific, and focused on improving the mod for players and maintainers.

## Choose the right route

- For a player-visible bug, use the [Temperature Limit bug form](https://github.com/MaksymShostak/oxygen-not-included/issues/new?template=temperature-limit-bug.yml) and the automated report flow in [SUPPORT.md](SUPPORT.md).
- For a feature idea, use the [feature form](https://github.com/MaksymShostak/oxygen-not-included/issues/new?template=temperature-limit-feature.yml) and describe the player problem and desired experience.
- For a code or documentation change, open or link an issue before substantial work so scope and compatibility expectations are visible.

## Set up and validate a checkout

Follow the existing [getting-started](docs/guides/getting-started-with-oni-mod-pipeline.md) and [development](docs/guides/developing-oni-mods.md) guides. ONI Mod Pipeline is the repository's supported build, test, install, and release path.

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

Keep each change focused on one agreed player or maintainer outcome. Preserve unrelated working-tree changes, add or update a failing test before behavior code when practical, and keep production source compatible with the repository's C# 8 ceiling. Discuss dependency, build, test, formatting, CI, repository-policy, release-process, or other configuration changes in the linked issue before editing those files.

## Prepare a pull request

Link the issue, explain the player or maintainer rationale, summarize the focused change, and include fresh automated test evidence plus relevant in-game ONI evidence. Explicitly describe compatibility, performance/allocation, save/persistence, UI, localization, and documentation impact, writing `None` where a category does not apply. Release changes must follow [Preparing ONI mod releases](docs/guides/preparing-oni-mod-releases.md).
