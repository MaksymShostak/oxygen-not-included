# Security policy

This policy covers the Delivery Temperature Limit (Supercooled) mod for Oxygen Not Included, the ONI Mod Pipeline tool and the repository's build, test and release tooling.
The mod runs inside the player's game process; the pipeline runs on the maintainer's or contributor's machine.
The MIT boundary is this repository; continued upstream code and copied third-party helpers retain their original notices.

## Reporting a vulnerability

Use the repository's [private vulnerability report](https://github.com/MaksymShostak/oxygen-not-included/security/advisories/new).
Include the mod version or commit, ONI build and DLC set, the pipeline command or in-game action, smallest useful reproduction, observed result and security impact.
Keep sensitive data and exploit details in that private report; do not use the public bug form for security findings.
The destination channel was verified enabled on 16 September 2026.
No response-time or supported-release commitment is implied.

## Trust boundaries and required properties

Treat save data, other mods, `Player.log` contents, Workshop listing text and translation files as untrusted.
The player chooses whether a support report is generated, reviewed and attached; the mod never decides for them.
The pipeline operator chooses the mod root, profile, build result and install target; repository content must never select paths outside those choices.
The tracked profile, metadata, lock files and package allowlist are trusted release inputs, subject to supply-chain review.

- The mod must perform no network requests, telemetry, uploads or dynamic code loading.
  Its only network-adjacent action is asking the operating system to open the fixed GitHub bug-form URL.
- Support reports must be written only to the local report folder, must not read `Player.log` unless the extended report is explicitly chosen, and must disclose every excluded, redacted or truncated fact.
  Redaction is best-effort; the report must never claim complete removal of sensitive values.
- Harmony patches must fail closed: a patch that cannot be verified must leave the game path unchanged rather than partially applied.
  Transient runtime state must be cleared between colony sessions without altering saved limits or mod settings.
- Pipeline install and release operations must act only on paths derived from the operator's explicit arguments and the validated profile.
  Release candidates must be immutable; hashes and the release-content digest must be recomputed from actual bytes, never edited.
- Locked restore must be honoured; a build that would change the dependency closure must fail rather than silently resolve.
  Only the package allowlist may enter Workshop runtime content.
- Workshop upload remains a deliberate manual step with the operator's own Steam identity; the pipeline and CI must hold no Steam or publishing credentials.

## Findings and limitations

Assess reachable consequences: mod-triggered network or filesystem activity beyond the report folder, code execution or path traversal driven by save data, other mods or listing text, report contents that leak data the policy says is excluded, or compromise of the built DLL or release candidate are reportable concerns.
Severity depends on the actual game build, active mod set and deployment.
A crash or lost feature caused by another mod's incompatibility is a bug, not evidence of a security issue, unless it enables one of the consequences above.
No finding class is waived by this document.

The extended report reads at most the most recent 6 MiB of `Player.log` and applies path-prefix redaction only; arbitrary text written by the game or other mods can remain.
[SUPPORT.md](SUPPORT.md) records what each report kind collects and excludes.
The mod cannot bound what other Harmony mods do in the same process.

Tests, the pipeline's evidence files and a local build do not establish independent security approval, Workshop publication or release readiness.
The ONI Mod Pipeline is a maintainer tool; it is not part of the published mod's runtime behaviour.
