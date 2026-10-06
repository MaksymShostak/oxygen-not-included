# Delivery Temperature Limit (Supercooled): Steam Copy, Voice and Information-Architecture Review

## Archive status

Archived on 6 October 2026.
This is a historical assessment of the September 2026 drafts and public documentation as observed on 6 September 2026.

The main recommendations are now reflected in the tracked [README](../../README.md), [Workshop description](../../mods/delivery-temperature-limit-supercooled/STEAM_DESCRIPTION.bbcode), and [change notes](../../mods/delivery-temperature-limit-supercooled/STEAM_CHANGE_NOTES.bbcode): independent **At least** / **Below** fields and **Clear**, English plus 18 translated locales, the **Clean Colony State Between Loads** setting and its persistence distinction, and precise language about local reports and best-effort redaction.
The Workshop copy rewrite was committed on 8 September 2026 in [6f430b4](https://github.com/MaksymShostak/oxygen-not-included/commit/6f430b458b9750132556c50d84f5c9bbbe1ef7ac).

This archive records the original rationale and proposed copy.
It does not establish that every finding is closed or that the live Workshop page has been reverified.
The original review text and exported citation markers are preserved below; those markers refer to the original research session and are not standalone source links.

---

## Executive assessment

The most important finding is that **“consistent” should not mean “the Steam description and the latest change notes contain the same feature list.”** They have different jobs.

The Workshop description should be the evergreen answer to _“What does this mod do today, why would I install it, and what is special about Supercooled?”_ The change notes should answer _“What changed in this particular release, and is my existing colony safe?”_ That distinction matters here because Storage Tile support, for example, was already shipped in the 26 August 2026 release; the current Workshop page records it as part of v2026.8.26.
Repeating it in the next release notes as though it were newly added would actually make the documentation **less** accurate. citeturn21view0

What does need to become identical is the **semantic contract**: names, behaviour, UI terminology, compatibility claims, localisation count, save behaviour and descriptions of performance changes.
At the moment there are several places where that contract drifts.

There is also a third documentation surface beyond the two uploaded files: the public GitHub README.
As of 6 September 2026 it still says entering one temperature auto-populates the other and that `Del` clears the limits, while the uploaded Steam description and change notes describe independent **At least** / **Below** fields and a dedicated **Clear** button.
The same README's later “Supercooled” section has already moved on to some of the newer architecture.
In other words, the README presently contradicts itself as well as the staged Steam copy. citeturn21view1

The live Workshop description is another generation behind: it still advertises auto-population/`Del`, the earlier UI implementation, Game Update 737790, and the pre-overhaul feature wording.
It was last updated on 26 August. citeturn21view0 So what you really have is not a two-document problem but a **four-surface source-of-truth problem**:

| Surface                    | What it should own                              | Current issue                                                                                    |
| -------------------------- | ----------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| Steam Workshop description | Current product behaviour                       | Uploaded draft is mostly current, but contains repetition and a few overclaims/ambiguous phrases |
| Steam change notes         | Delta for one release                           | Good personality, but some terminology differs from the description                              |
| GitHub README              | Current capabilities + developer/support detail | Contains stale UI instructions and stale localisation inventory                                  |
| Release/build metadata     | Exact compatibility facts                       | Build claims should be precise rather than future-looking                                        |

My recommended editorial model is therefore:

**one canonical feature vocabulary → different views for different audiences.**

The biggest copy changes I would make are:

**Keep the opening concept.**
Nisbet + hot Igneous Rock + Sleet Wheat is excellent ONI material.
Sleet Wheat really is extremely temperature-sensitive, and Storage Bins and Storage Tiles are genuine game objects; Storage Tiles are particularly relevant in cramped rocket interiors.
The scenario therefore has the important property of an ONI joke: it is ridiculous _and mechanically believable_. citeturn20search0turn20search5turn20search19

**Make the description substantially less repetitive.**
UI behaviour currently appears in Key Features, Supercooled and Mod Settings.
Localisation appears in Key Features, Supercooled and its own section.
Construction appears in Key Features and Settings.
Those repetitions create most of the divergence risk.

**Let the Supercooled section become deliberately more technical.**
Your instinct is right.
It should explain _why this continuation exists_ and how its implementation differs, rather than giving every change a separate comedy routine.

**Replace “18 languages” with “English + 18 translated locales/localisations”.**
Your uploaded list represents 19 selectable locales in total: English plus 18 translated variants.
Because Simplified/Traditional Chinese and Portuguese/Portuguese-Brazil are separate locales, “18 languages” is ambiguous whichever way it is counted.

**Collapse Languages into one compact line near Compatibility.**
There is no need for a 17-row section.

**Tone down absolute engineering claims.** “Rock-solid”, “preventing crashes”, “zero overhead”, “future-proofed” and “Build 744825+” are unnecessarily strong. Build 744825 is in fact the latest Klei release shown in the official update index as of 6 September 2026, so **“Tested on ONI build 744825”** is both stronger and more defensible than “latest updates (744825+)”. citeturn18view6

The result should feel less like release-marketing copy wearing an ONI hat and more like an ONI system calmly explaining why Meep has once again put 95 °C rock somewhere agriculturally catastrophic.

## Where the two drafts should converge — and where they should not

The cleanest way to merge them is to establish the following as the canonical feature model.

| Capability              | Canonical message                                                                                                              | Workshop description                      | Current release notes                                   |
| ----------------------- | ------------------------------------------------------------------------------------------------------------------------------ | ----------------------------------------- | ------------------------------------------------------- |
| Temperature filtering   | Storage/delivery targets can accept materials only within configured minimum and/or maximum temperatures                       | **Core feature**                          | Mention only if behaviour changed                       |
| Routing                 | Out-of-range material is excluded when delivery errands are evaluated                                                          | **Core feature**                          | Mention because the evaluation/performance path changed |
| Storage coverage        | Storage Bins, Storage Tiles, Refrigerators and compatible storage targets; Storage Tiles work in rockets                       | **Core feature / Supercooled difference** | **Do not call new now**; it shipped 26 Aug              |
| Construction filtering  | Optional filtering of materials delivered to blueprints                                                                        | **Core feature + option**                 | Mention only when changed                               |
| Building side-screen    | Independent **At least** and **Below** thresholds, external °C/°F/K labels, Clear, copy settings, live status                  | **Core UX feature**                       | Yes — UI overhaul is part of this release               |
| Keyboard handling       | Tab/Enter/Escape work in fields; WASD/Space are not swallowed                                                                  | One short UX statement                    | Yes — changed this release                              |
| Options dialog          | Dedicated dialog, previews/conversion, Reset to Defaults, keyboard navigation                                                  | Settings section                          | Yes — changed this release                              |
| Warning evaluation      | Optional temperature-aware “Lacks Resources” calculation                                                                       | Settings section                          | Yes — its evaluation path changed                       |
| Performance             | No colony/asteroid-wide inventory scan for each decision; targets retain their permitted range for direct checks               | Technical Supercooled section             | Yes — changed this release                              |
| Runtime cleanup         | Transient state is cleared between colony sessions without erasing saved limits                                                | Technical Supercooled section             | Yes — changed this release                              |
| Modded storage          | Compatible custom storage targets are discovered dynamically                                                                   | Technical Supercooled section             | Only if discovery changed this release                  |
| Fast Track              | Compatible path when Fast Track is present                                                                                     | Compatibility/technical section           | Yes if integration changed or was added                 |
| Localisation            | English + 18 translated locales, following ONI's selected language                                                             | One-line compatibility fact               | Yes — this release adds them                            |
| Support reports         | Reports are generated locally; Extended may contain a bounded, best-effort-redacted log excerpt; nothing uploads automatically | Support section                           | Yes — if introduced this release                        |
| Existing saves/settings | Existing saved temperature and construction settings remain intact                                                             | Brief compatibility assurance             | **Definitely in release notes**                         |
| Tested game version     | Tested on ONI build 744825                                                                                                     | Small compatibility line                  | Optional release-note footer                            |

This reveals several specific fixes.

### “Fresh Start on Every Load” should be renamed

This is one of the most important wording changes.

Taken literally, **Fresh Start on Every Load** sounds alarmingly like _settings are discarded_.
The change notes then have to reassure players with “Your Bins Are Safe”.
Both statements are technically compatible because the first means transient runtime tracking while the second means persistent save data, but users should not have to reverse-engineer that distinction.

I would rename it:

> **Clean Colony State Between Loads:** Transient runtime tracking is cleared when you return to the main menu or load another colony.
> Saved temperature limits and mod settings remain untouched.
> No haunted Storage Bins; no accidental exorcism of your actual settings.

That is simultaneously clearer, more technically exact and more ONI-like.

### “Global Colony Localization” should disappear

“Global Colony Localization” and especially “deployed automatically into your game installation” sound like enterprise deployment software.
They also make the mod sound as though it modifies the game's installation rather than simply loading its own language resources.

Use:

> **Built-In Localisation:** English plus 18 translated locales are bundled with the mod and follow ONI's selected language automatically.
> Meep's interpretive-dance translation programme has been officially decommissioned.

The public repository currently advertises only nine active `.po` catalogues (`de`, `es`, `fr`, `ko`, `pt`, `pt_BR`, `uk`, `zh`, `zh_tw`).
If the uploaded 18-localisation set is the release candidate, the README should be updated in the same release; otherwise a user following the Workshop's GitHub link will immediately encounter a conflicting language count. citeturn21view1

### “Errand and Warning Decisions” belongs in both documents, but differently

This is useful information from the change notes that is underrepresented in the description.

For the release notes, the implementation-level wording works:

> Delivery errands and temperature-aware resource warnings now use the same direct target-range checks rather than repeatedly surveying colony inventory.

For the evergreen description, the user benefit is more important:

> **Temperature-Aware Errands:** Deliveries respect each target's allowed range, and the optional “Lacks Resources” warning can do the same.

The joke comes afterwards:

> Colony management has finally established that “available somewhere on the planetoid” and “safe to put next to the Sleet Wheat” are different categories.

### “Support report” privacy wording should become more exact

The staged description calls the output “privacy-safe” and an Extended report's log excerpt “sanitized”.
Your public README is more careful: it says the ordinary report does not read `Player.log`; the Extended report contains a bounded, **best-effort-redacted** copy; and nothing is uploaded automatically. citeturn19view5

The README language is actually the best wording to propagate.
“Privacy-safe” is an absolute.
“Local, review-before-sharing, with best-effort redaction” tells users exactly what happens.

I would use:

> Reports are created locally and nothing is uploaded automatically.
> The standard report does not read `Player.log`; the clearly labelled Extended report can include a bounded, best-effort-redacted log excerpt for harder failures.
> Review the file, then attach it to the GitHub issue form.

That is unusually good support UX for a Workshop mod; there is no reason to weaken it with vague marketing terminology.

### “Tested on current builds” should be date-stable

Klei's official update index currently lists **744825, released 28 July 2026**, as the newest release. citeturn18view6 Therefore:

> **Tested with ONI build 744825**

is preferable to:

> **Fully Tested on Current Game Builds: Tested and verified against the latest game updates (Build 744825+).**

The latter contains three promises you do not need: “fully”, “current” and “+”.
The first becomes stale as soon as Klei publishes anything; the plus sign effectively promises compatibility with a future build you have not seen.

A static tested-build number remains truthful after the next update.

### Storage Tile is cumulative, not new

The live Workshop history confirms that Storage Tile support, including rocket interiors, was added in v2026.8.26 in response to ShyLion's request. citeturn21view0 It absolutely belongs in the evergreen description's Supercooled comparison, but unless this upcoming release changes that implementation again, it **should not** be copied into these change notes.

This is the clearest example of why the solution is **semantic convergence, not textual duplication**.

## Reproducing Klei's ONI voice rather than merely adding jokes

The official ONI copy has a very recognisable mechanism.
It usually states the actual simulation mechanic clearly, then lands a short, dry punchline.
The Steam description moves from serious explanations of survival, stress, temperature, fluids and power into tiny reversals such as forgetting to breathe, keeping Duplicants happy “whatever the cost”, or potentially powering a colony with bodily emissions. citeturn19view6

That suggests a useful rule for the mod:

> **Mechanic first. Duplicant disaster second.**

Your strongest existing lines already follow it.

“Storage Tiles now support delivery temperature limits, including aboard rockets” is the fact; Duplicants putting volcano-fresh cargo under the floor and producing an “orbital sauna” is the payoff.

“WASD, Space and Escape are no longer captured” is the fact; being able to pan over in time to watch a Duplicant run into magma is the payoff.

That is much closer to authentic ONI than putting whimsical pseudo-lore into the factual clause itself.

A good target is roughly **one punchline per feature**, and often less.
Let the joke be the final sentence rather than forcing three jokes through every bullet.

### Use the simulation itself as the comedy engine

The most convincing references are those where experienced players instantly recognise the physical consequence.

Your Sleet Wheat premise is particularly good because Sleet Wheat only grows in a very cold temperature window, while Igneous Rock is explicitly the solid form associated with cooled magma.
A hot rock being transported into that cold agricultural system therefore reads as an actual ONI engineering failure, not just random game-name insertion. citeturn20search0turn20search7

Good recurring material for this mod includes:

| ONI reference                 | Why it works here                                               | Example punchline                                                                                  |
| ----------------------------- | --------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| Sleet Wheat                   | Heat-sensitive farming directly relates to delivery temperature | “Sleet Wheat has requested that Supply errands undergo thermal screening.”                         |
| Igneous Rock / Magma          | Perfect hot-solid example                                       | “Volcano-fresh is not a recognised storage category.”                                              |
| Storage Bin                   | Your central affected building                                  | “The label said ‘Raw Minerals’, not ‘portable space heater’.”                                      |
| Storage Tile                  | New Supercooled capability and rocket relevance                 | “Under the floor is not a thermal exemption.”                                                      |
| Thermo Sensor                 | Exact UI conceptual analogue                                    | “Like a Thermo Sensor, except it judges the delivery before Nisbet puts it down.”                  |
| Rockets                       | Excellent constrained-space heat joke                           | “Because an orbital sauna should at least be intentional.”                                         |
| “Lacks Resources”             | Actual UI concept and option                                    | “The colony may have 800 tonnes of rock. Unfortunately, all of it is trying to become magma.”      |
| Printing Pod                  | Natural framing device for releases/localisation                | “The Printing Pod has received updated paperwork.”                                                 |
| Cycle 1000                    | Familiar shorthand for late-game complexity                     | “The mod no longer performs an asteroid census every time Meep picks up a rock.”                   |
| Duplicant path/errand choices | Universally relatable player pain                               | “Temperature has been fixed. Opinions about which rock is ‘nearest’ remain outside project scope.” |

Storage Tiles genuinely target tight spaces such as rocket interiors, strengthening the “orbital sauna” gag rather than making it arbitrary. citeturn20search5

### Remove invented ONI-ish objects where a real one is better

A few current phrases feel _adjacent_ to ONI rather than _inside_ it.

**“Cryogenic chambers”** should go.
It sounds science-fictional but not specifically ONI.
“Sleet Wheat farms”, “deep-freeze food storage” or simply “cold rooms” gives players an actual mental picture.

**“Space stations”** in the change notes should become **“planetoids and rockets”**.
The latter is much closer to the game's normal player vocabulary.

**“Storage lockers”** should normally be **Storage Bins**.
Reserve “custom storage containers” for modded buildings.

**“Universal linguistic frequency modulators”** is funny in a generic mad-science way but less distinctly Klei than a deadpan administrative failure.
The joke can simply be that Meep was previously reduced to interpretive dance.

And I would remove:

> “run as cleanly as a Thermo Regulator in a vacuum”

The image sounds ONI-ish at first glance, but knowledgeable ONI players are precisely the audience likely to start thinking about whether the Thermo Regulator has any way to dump its own heat in that vacuum.
A technical section should not accidentally launch a thermodynamics argument.

Something like this is safer and funnier:

> The Supercooled backend has been substantially rebuilt.
> Most importantly, it no longer asks the entire planetoid what temperature every loose rock is before deciding whether Nisbet may put one in a bin.

### Reduce corporate/software-release vocabulary

These phrases are internally reasonable but externally sound more like a SaaS changelog than ONI:

> - Global Colony Localization
> - Modernized Mod Options Terminal
> - Rock-Solid Startup & Stability
> - Fully Tested on Current Game Builds
> - zero overhead
> - deployed automatically
> - preventing crashes

Prefer concrete descriptions:

> - Built-In Localisation
> - Rebuilt Mod Options
> - Safer Startup Checks
> - Tested on Build 744825
> - uses the standard path when Fast Track is absent
> - bundled with the mod
> - guards known startup/refresh paths

The comedy then supplies the flavour.
The engineering wording supplies trust.

I would also reduce the emoji count.
Klei's official Steam description gets its personality from headings such as “Avoid Boiling with Thermodynamics” and from the prose underneath, not from decorating every heading with a symbol. citeturn19view6 Compatibility badges already provide plenty of visual structure.
Keeping perhaps one visual marker for compatibility/localisation is fine; an emoji on every heading makes the page feel more like a generic Workshop listing than Klei-authored copy.

## Language compatibility and Steam Workshop best practice

Your instinct that the current language section has become too long is correct.

The issue is not that seventeen visible rows are technically wrong.
It is that **language support is metadata**, whereas your page currently gives it the same visual weight as the actual mod behaviour.
A prospective subscriber generally wants three things from that information:

> Does my language work?  
> Does the mod choose it automatically?  
> Which variants are supported?

Those can be answered in one or two lines.

There is precedent within the ONI Workshop ecosystem for exactly that treatment.
For example, Chain Tool presents its version followed immediately by a compact `Translations: en, uk, ru, de, zh, fr, ko` line rather than allocating a large section to each language; Errand Notifier follows the same pattern. citeturn18view5turn21view2

### Fix the count first

The uploaded language inventory consists of:

English, Simplified Chinese, Traditional Chinese, Czech, French, German, Greek, Hungarian, Italian, Japanese, Korean, Polish, European Portuguese, Brazilian Portuguese, Spanish, Thai, Turkish, Ukrainian and Vietnamese.

That is **19 locales in total**.

So I would stop saying “fully localised into 18 languages”.
The cleanest formulation is:

> **Localisation:** English + 18 translated locales, selected automatically from ONI's language setting.

Then, if you want explicit discovery:

> Simplified & Traditional Chinese, Czech, French, German, Greek, Hungarian, Italian, Japanese, Korean, Polish, Portuguese & Brazilian Portuguese, Spanish, Thai, Turkish, Ukrainian and Vietnamese.

This makes the arithmetic unambiguous: English is the source/default; there are eighteen additional locale catalogues.

It also avoids “fully fluent”, which is charming in the change note but makes a quality claim on behalf of every community translation.

### My preferred Steam treatment

I would eliminate the standalone **Supported Languages** section and put this directly beneath the DLC compatibility badges:

```text
[b]Localisation:[/b] English + 18 translated locales, selected automatically from ONI's language setting.
Simplified & Traditional Chinese, Czech, French, German, Greek, Hungarian, Italian, Japanese, Korean, Polish, Portuguese & Brazilian Portuguese, Spanish, Thai, Turkish, Ukrainian and Vietnamese.
```

That is compact enough that I would **not** hide the names behind a spoiler/collapsible device.
A wrapped line is easier to scan, searchable, and immediately answers a user's language question.

For the **change notes**, because localisation itself is new in this release, listing the added locales once is justified.
Future releases should mention only newly added or materially updated translations.

### In-game localisation and Workshop-page localisation are different things

Steam's Workshop API actually supports language-specific **Workshop title and description metadata**.
`SetItemUpdateLanguage` designates the language for the title and description being uploaded, and English is assumed when none is supplied. citeturn19view0turn19view1

That is separate from the mod's own ONI translation catalogues.

Your current Workshop item demonstrates the distinction nicely: the Steam interface can be viewed in Vietnamese while the mod's Workshop description itself still appears in English. citeturn19view2turn19view3

So there are really two possible localisation tiers:

**Now:** keep the English Workshop description and state compactly that the _in-game mod UI_ supports English + 18 translated locales.

**Later, optionally:** if your publishing workflow gains support for Steam's language-specific Workshop metadata, publish translated Workshop descriptions through that facility instead of turning the English description into a multilingual wall of text.
Steam's API explicitly supports this model. citeturn19view0turn19view1

That is substantially cleaner than putting eighteen complete translated descriptions into one English Workshop page.

## Recommended section order and information hierarchy

For the Workshop description, I would use this order:

| Position  | Section                                     | Purpose                                                                   |
| --------- | ------------------------------------------- | ------------------------------------------------------------------------- |
| First     | **Opening problem + one-sentence solution** | “I know this exact ONI pain; what fixes it?”                              |
| Second    | **Compatibility & Localisation**            | “Will it work in my game?”                                                |
| Third     | **What It Does**                            | Evergreen user-facing capabilities                                        |
| Fourth    | **What Supercooled Changes**                | Why this fork/continuation exists; technical enough for experienced users |
| Fifth     | **Mod Options**                             | What can be configured                                                    |
| Sixth     | **Support & Diagnostics**                   | What to do if something breaks                                            |
| Seventh   | **Credits & Lineage**                       | Attribution and historical context                                        |
| Last line | Disclaimer                                  | Legal/community status                                                    |

That is a better decision funnel than the current placement of **Credits before Settings**.
Attribution is important, especially for a maintained continuation, but most Workshop visitors deciding whether to subscribe need configuration and troubleshooting before project lineage.

I would also rename **Key Features** to **What It Does**.
Klei's own headings tend to describe an activity or outcome rather than announcing that the following content is “features”. citeturn19view6

Similarly:

> “What's New in the ‘Supercooled’ Version?”

should become:

> **What Supercooled Changes**

or:

> **Why “Supercooled”?**

“What's New” sounds like a changelog and therefore competes conceptually with Steam's actual Change Notes tab. **What Supercooled Changes** tells the reader this is a persistent comparison with the predecessor.

For the change notes, the best order is different:

| Position  | Content                                 |
| --------- | --------------------------------------- |
| Opening   | One short themed “colony broadcast”     |
| Main body | New/changed user-visible behaviour      |
| Then      | Performance/integration changes         |
| Then      | Existing-save reassurance               |
| Then      | Support/privacy note if changed         |
| Finish    | One Known Issue joke + one closing line |

I would not create separate categories for five tiny subtypes of change.
A release note with nine bullets and four headings becomes harder to read than a release note with nine well-written bullets.

### The description should become shallower

Right now the reader encounters **Key Features → Supercooled → Credits → Settings → Support → Languages**, with UI concepts recurring across several of them.

The proposed model gives each fact exactly one “home”:

**What It Does** owns _behaviour_.

**What Supercooled Changes** owns _implementation/history/performance_.

**Mod Options** owns _toggles_.

**Compatibility & Localisation** owns _environment compatibility_.

**Support** owns _diagnostics/privacy_.

This should make future change-note updates much easier because you no longer have to remember that the same Clear button is described three different ways.

## Recommended merged Workshop copy

Below is the direction I would actually publish.
I have intentionally made the opening and user-facing portions playful, while letting **What Supercooled Changes** become more engineering-oriented.
I would retain your existing DLC badge image row and attribution links.

```text
Tired of Nisbet hauling a 95 °C chunk of Igneous Rock into the Storage Bin beside your carefully chilled Sleet Wheat? Have your Duplicants discovered that "Raw Minerals" and "portable space heater" are apparently the same storage category?

[b]Delivery Temperature Limit (Supercooled)[/b] lets you set a safe temperature range for materials delivered to storage, buildings, and — optionally — construction.

Hot rock stays with the hot rock. Cold supplies stay cold. Duplicants remain free to make entirely different mistakes.

[h1]Compatibility & Localisation[/h1]

[keep existing Base Game + DLC compatibility badge row]

[b]Localisation:[/b] English + 18 translated locales, selected automatically from ONI's language setting.
Simplified & Traditional Chinese, Czech, French, German, Greek, Hungarian, Italian, Japanese, Korean, Polish, Portuguese & Brazilian Portuguese, Spanish, Thai, Turkish, Ukrainian and Vietnamese.

[b]Tested with ONI build 744825.[/b]

[h1]What It Does[/h1]
[list]
[*] [b]Temperature-Limited Storage:[/b] Give supported delivery targets a minimum temperature, a maximum temperature, or both. Storage Bins, Storage Tiles, Refrigerators, and compatible storage buildings can all check the thermometer before accepting a delivery.

[*] [b]Temperature-Aware Errands:[/b] Materials outside a target's allowed range are excluded from its delivery errands. "It's the nearest rock" is no longer a valid defence when the nearest rock is trying to cook the Sleet Wheat.

[*] [b]Optional Construction Limits:[/b] Apply the same rules to construction materials so Duplicants do not carry furnace-hot building material into the cold room you have spent forty cycles cooling.

[*] [b]Thermo Sensor-Style Controls:[/b] Set independent [b]At least[/b] and [b]Below[/b] thresholds directly from the building side-screen, with °C / °F / K labels, copy-settings support, a one-click [b]Clear[/b] button, and a live plain-language summary of the active range.

[*] [b]Keyboard-Friendly UI:[/b] Tab between fields, Enter to commit and Escape to cancel without sacrificing WASD panning or Space to pause. The camera controls have been formally released from temperature-box custody.
[/list]

[h1]What Supercooled Changes[/h1]

[b]Supercooled[/b] is a maintained and substantially refactored continuation of the original Delivery Temperature Limit mod. The basic idea is unchanged; quite a lot underneath it is not.

[list]
[*] [b]Direct Temperature Checks:[/b] The old colony-wide inventory scanning path has been removed. Eligible targets keep their configured temperature range available for errand checks directly, reducing work in large late-game colonies. The mod no longer conducts an asteroid census every time Nisbet picks up a rock.

[*] [b]Storage Tile Support:[/b] Storage Tiles support the same temperature limits, including inside rockets. "Under the floor" is not a thermal exemption. Thanks to [url=SHYLION_LINK]ShyLion[/url] for helping prevent the next unexpected orbital sauna.

[*] [b]Clean Colony State Between Loads:[/b] Transient runtime tracking is cleared between colony sessions while saved temperature limits and mod settings remain intact. No haunted Storage Bins; no accidental exorcism of your actual settings.

[*] [b]Rebuilt Temperature UI:[/b] The side-screen now uses independent [b]At least[/b] and [b]Below[/b] values, external unit labels, live range summaries, warning feedback for inverted limits, dedicated clearing, and reliable keyboard handling.

[*] [b]Safer Startup & Storage Discovery:[/b] Defensive checks protect the mod's game hooks during startup and colony refreshes, while compatible storage targets introduced by other mods can be discovered dynamically.

[*] [b]Fast Track Compatibility:[/b] Integrates with Peter Han's [url=FAST_TRACK_LINK][i]Fast Track[/i][/url] when it is present and uses the normal game path when it is not.

[*] [b]Built-In Localisation:[/b] English plus 18 translated locales are bundled with the mod and follow ONI's selected language automatically. Meep's interpretive-dance translation programme has been retired.
[/list]

[h1]Mod Options[/h1]

The in-game Mod Options dialog provides:
[list]
[*] [b]Temperature-Aware "Lacks Resources":[/b] Choose whether the warning also considers your temperature limits. Disable it if you prefer the lightest possible late-game warning checks.

[*] [b]Construction Material Limits:[/b] Choose whether temperature restrictions also apply to materials delivered to blueprints.

[*] [b]Temperature Previews:[/b] Preview values in Celsius, Fahrenheit, or Kelvin with automatic conversion, and reset the mod's options to their defaults in one click.
[/list]

[h1]Support & Diagnostics[/h1]

If something goes thermally sideways, open Mod Options and choose [b]Create Support Report[/b].

Reports are generated locally and nothing is uploaded automatically. The standard report does not read [i]Player.log[/i]; the clearly labelled [b]Extended Support Report[/b] can include a bounded, best-effort-redacted log excerpt for harder failures.

Review the report on your computer, then attach it to the GitHub bug-report form.

Duplicants cannot currently be included in the diagnostic bundle, despite repeated requests from colony management.

[h1]Credits & Lineage[/h1]

This mod continues work begun by the ONI modding community:

[list]
[*] [b]Original Concept & Code:[/b] llunak — [i]Delivery Temperature Limit[/i].
[*] [b]Intermediate Maintenance:[/b] [sd] QooLiO — [i]Delivery Temperature Limit [Fixed][/i].
[*] [b]Supercooled Edition:[/b] Maintained, refactored, and optimised by Maksym Shostak.
[/list]

[i]Delivery Temperature Limit (Supercooled) is a community mod and is not affiliated with, sponsored by, or endorsed by Klei Entertainment.[/i]
```

A few details in that draft are deliberate.

I have changed **95 C** to **95 °C**.
Your live Steam listing already renders it as 95°C, and the typographic degree symbol is both cleaner and more like an actual temperature readout. citeturn21view0

I kept Sleet Wheat in the first paragraph because it is an especially good mechanically grounded reference: the plant's viable upper temperature is only 5 °C, so the reader instantly understands why a hot delivery is catastrophic. citeturn20search0

I kept Storage Tile + rocket humour because Storage Tiles are specifically useful in tight areas such as rocket interiors, and your August Workshop conversation shows that rocket deliveries were the actual user report which led to the feature. citeturn20search5turn21view0

I removed a separate multilingual bullet from **What It Does**.
Localisation is not part of the mod's gameplay proposition; it is compatibility metadata and a Supercooled improvement.
Two mentions are enough.

I also removed “Real-Time Temperature Previews” as a fourth top-level settings feature because it is part of the **options-dialog experience**, not a separate behavioural toggle.
This makes Settings tell users what they can actually configure rather than cataloguing every piece of UI furniture.

## Recommended revised change note

The current change note has better personality than the description in several places.
In particular, **“Your Bins Are Safe”**, the nearest-rock Known Issue and the molten-tungsten closing line are all worth keeping.
What I would change is mostly precision: fix the localisation count, align UI terminology, replace “space stations”, clarify transient-vs-persistent state, and avoid repeating features that already shipped in August.

I would publish something close to this:

```text
[h3]Universal Translators and Calibrated Thermometers[/h3]

Colony broadcast update: Meep's attempt to explain temperature limits to Nisbet by waving a red-hot Obsidian boulder has been judged "informative, but unnecessarily hazardous."

The mod now speaks 18 additional locales, the Options dialog has been rebuilt, temperature controls have had a proper calibration pass, and late-game errands spend considerably less time asking every loose rock on the planetoid for its paperwork.

[b]Changes[/b]
[list]
[*] [b]The Printing Pod Has Been Studying:[/b] Added 18 translated locales: Simplified & Traditional Chinese, Czech, French, German, Greek, Hungarian, Italian, Japanese, Korean, Polish, Portuguese & Brazilian Portuguese, Spanish, Thai, Turkish, Ukrainian, and Vietnamese. The mod follows ONI's selected language automatically. Meep's interpretive-dance translation service is now considered deprecated.

[*] [b]Rebuilt Mod Options:[/b] The settings screen now uses a dedicated dialog with live °C / °F / K previews and conversion, a one-click [b]Reset to Defaults[/b] button, and keyboard navigation with Tab, Enter, and Escape.

[*] [b]Thermo Sensor-Grade Temperature Controls:[/b] The building side-screen now has independent [b]At least[/b] and [b]Below[/b] thresholds, unit labels outside the input fields, a dedicated [b]Clear[/b] button, live plain-language range status, and warning feedback for inverted limits.

[*] [b]The Camera Has Been Released:[/b] Temperature fields no longer swallow WASD panning, Space to pause, or Escape. Duplicants may still sprint towards magma, but you can once again pan over in time to witness the decision.

[*] [b]Less Asteroid Staring:[/b] Eligible buildings keep their configured temperature range available for direct errand checks instead of repeatedly scanning colony inventory. Delivery decisions and the optional temperature-aware [b]Lacks Resources[/b] warning use the streamlined path as colonies spread across planetoids and rockets.

[*] [b]Clean State Between Colonies:[/b] Transient runtime tracking is cleared when leaving or changing colonies. Saved temperature limits and mod settings are not cleared. Storage Bins from abandoned colonies have therefore lost their ability to haunt new ones.

[*] [b]Fast Track Compatibility:[/b] The mod coordinates with current releases of Peter Han's [i]Fast Track[/i] when present and uses the normal game path otherwise.

[*] [b]Local Support Reports:[/b] Support reports can now be generated from the Options dialog without uploading anything automatically. The standard report does not read [i]Player.log[/i]; the clearly labelled Extended report can include a bounded, best-effort-redacted excerpt for harder failures.
[/list]

[b]Existing Colonies[/b]
[list]
[*] [b]Your Bins Are Safe:[/b] Existing temperature limits, construction settings, copied building settings, and saved colonies carry over unchanged. The filing cabinet survived the renovation.
[/list]

[b]Known Issues[/b]
[list]
[*] Duplicants still have opinions about which rock is nearest. Temperature was the only behaviour approved for this maintenance window.
[/list]

Tested with Oxygen Not Included build 744825.

Every colony remains its own lovingly overcomplicated thermodynamic incident. No Duplicants were asked to benchmark molten tungsten.
```

I prefer **“Calibrated Thermometers”** to **“Calibrated Terminals”** in the title because temperature measurement is the actual subject of the update; “terminal” is generic sci-fi language.
The original phrase is not bad, but this one primes the reader for the specific feature.

I would also **not add Storage Tile support, custom-mod-storage discovery or generic startup fixes to these particular change notes unless they genuinely changed again in this build**.
Storage Tile support demonstrably belongs to the 26 August release history already. citeturn21view0 The evergreen description is exactly where cumulative Supercooled improvements belong.

The one area where the changelog can afford to be more whimsical than the description is its framing.
Someone reading Change Notes has already subscribed or is already interested; you no longer need every sentence to perform conversion work.
That makes **“The Camera Has Been Released”**, “filing cabinet”, “nearest rock” and molten tungsten good places to spend the comedy budget.

## Keeping the message consistent after this release

The public repository already has a sophisticated release pipeline, but its README says the final authenticated Workshop Publish action remains manual. citeturn21view1 That manual boundary is exactly where copy drift can re-enter, so I would add a documentation acceptance check to the release candidate rather than relying on memory.

Conceptually, maintain one tiny canonical feature ledger with fields such as:

| Field                 | Example                                        |
| --------------------- | ---------------------------------------------- |
| `feature_id`          | `temperature_side_screen`                      |
| `current_name`        | `Thermo Sensor-Style Controls`                 |
| `current_behaviour`   | Independent At least/Below; Clear; live status |
| `since_release`       | upcoming release                               |
| `description_visible` | yes                                            |
| `changelog_visible`   | only in introducing release                    |
| `settings_visible`    | no                                             |
| `technical_detail`    | unit labels outside fields; keyboard semantics |
| `joke_anchor`         | Thermo Sensor / camera controls                |
| `localisation_key`    | n/a                                            |

The release process can then enforce the human-level rules even without generating all prose automatically:

**Terminology test:** no remaining `min/max auto-populates`, `Del to clear`, `space stations`, “18 languages”, or stale build number.

**Persistence test:** every mention of runtime cleanup explicitly distinguishes it from saved settings.

**Localisation test:** Steam copy, README and packaged catalogue count agree.

**Compatibility test:** say **“tested on build X”**, not “works with X+” or “future-proof”.

**Delta test:** the change note contains only features changed since the previous Workshop release; cumulative Supercooled capabilities stay in the description.

**Tone test:** each bullet must make sense with its joke deleted.
If deleting the joke also deletes important information, the comedy has become entangled with the specification.

That last rule is particularly well aligned with Klei's own ONI writing.
The official game copy can explain thermodynamics, gas/liquid simulations, power grids and stress plainly, then cap the explanation with something cheerfully bleak or absurd. citeturn19view6 The funniest version of this mod's documentation therefore is not the one with the greatest quantity of jokes.
It is the one where the mod behaves like a perfectly serious piece of colony bureaucracy tasked with solving one tiny thermodynamic problem in a universe populated entirely by Duplicants.

That gives you a very strong recurring voice:

> **Colony management has reviewed the incident. The simulation was working correctly. Nisbet was not.**
