# Unity Game Production-Readiness Checklist

> **Production rule:** A game is not ready because it works in the Unity Editor. It is ready only when a reproducible release candidate passes functional, performance, platform, compliance, recovery and operational testing on representative target hardware.

## How to use this checklist

- **P0 — Release blocker:** Must pass before public production release.
- **P1 — High priority:** May not block a limited alpha, but should block a commercial or broad public release.
- **P2 — Improvement:** Can be scheduled after launch if the risk is understood and accepted.
- Record an **owner**, **evidence**, **test build**, **date** and **result** for every P0 item.
- Do not approve a category based on verbal confirmation. Require screenshots, logs, profiler captures, test reports or signed QA results.

---

# 1. Release Scope and Ownership

- [ ] **P0** The production platforms are explicitly defined: Windows, macOS, Linux, Android, iOS, Web or console.
- [ ] **P0** Minimum and recommended hardware requirements are documented.
- [ ] **P0** Supported operating-system versions are documented.
- [ ] **P0** Supported screen resolutions, aspect ratios and refresh rates are documented.
- [ ] **P0** The target frame rate is defined for each device tier and platform.
- [ ] **P0** Maximum memory usage, build size, loading time and network bandwidth budgets are defined.
- [ ] **P0** Release scope is frozen: included features, maps, missions, modes, languages and platforms.
- [ ] **P0** Every production area has one accountable owner.
- [ ] **P0** A release-blocker severity definition exists.
- [ ] **P0** A formal go/no-go authority is assigned.
- [ ] **P1** Post-launch scope is separated from release scope.
- [ ] **P1** Known limitations are documented for players, support and QA.

---

# 2. Unity Project and Dependency Control

- [ ] **P0** The project uses an approved Unity version and the exact editor version is pinned.
- [ ] **P0** Production is not built from an experimental, alpha or unapproved editor version.
- [ ] **P0** All Unity packages are pinned to tested versions.
- [ ] **P0** No unnecessary package remains installed.
- [ ] **P0** Third-party assets and plugins have valid licences for production use.
- [ ] **P0** Native plugins are tested separately on every target architecture.
- [ ] **P0** Package upgrades are frozen during release-candidate validation.
- [ ] **P0** Project settings are stored in source control.
- [ ] **P0** Text-based asset serialisation is enabled.
- [ ] **P0** Visible Meta Files are enabled and all `.meta` files are committed.
- [ ] **P0** Platform-specific Build Profiles are configured and version-controlled where practical.
- [ ] **P0** Development, staging and production configurations cannot be accidentally mixed.
- [ ] **P0** Production API URLs, keys, environment flags and bundle identifiers are verified.
- [ ] **P1** A dependency inventory records package owner, version, licence and purpose.
- [ ] **P1** Deprecated APIs and packages have an explicit replacement plan.

---

# 3. Source Control, Backups and Reproducibility

- [ ] **P0** The complete project is stored in Git, Plastic SCM or another controlled repository.
- [ ] **P0** `Assets`, `Packages` and `ProjectSettings` are committed.
- [ ] **P0** Generated folders such as `Library`, `Temp`, `Logs` and local builds are excluded.
- [ ] **P0** Large binary assets use an appropriate large-file strategy.
- [ ] **P0** Protected production branches and review rules are enabled.
- [ ] **P0** Every release build is generated from a tagged commit.
- [ ] **P0** A clean checkout can produce the same build without relying on a developer's local machine state.
- [ ] **P0** Build number, semantic version, commit hash and environment are embedded in the game.
- [ ] **P0** Automated or documented backups exist for the repository and production data.
- [ ] **P0** At least one restore test has been completed successfully.
- [ ] **P1** A release branch and hotfix workflow are documented.
- [ ] **P1** Build artefacts and symbols are retained for each released version.

---

# 4. Code and Architecture Quality

- [ ] **P0** The project compiles with zero errors.
- [ ] **P0** Production builds contain no uncontrolled exceptions.
- [ ] **P0** Console warnings have been reviewed; ignored warnings are documented.
- [ ] **P0** Debug-only cheats, menus, endpoints and commands are removed or securely disabled.
- [ ] **P0** Secrets are not stored in source code, ScriptableObjects, Resources, StreamingAssets or client binaries.
- [ ] **P0** Runtime systems have clear startup, shutdown and scene-transition behaviour.
- [ ] **P0** Event subscriptions and callbacks are unsubscribed correctly.
- [ ] **P0** Static state is reset correctly when restarting sessions or disabling domain reload.
- [ ] **P0** Null-reference and destroyed-object paths are handled.
- [ ] **P0** Coroutines, async tasks, cancellation and object lifetimes are controlled.
- [ ] **P0** Main-thread blocking work has been identified and removed from gameplay-critical paths.
- [ ] **P0** Reflection-dependent code is tested against production code stripping.
- [ ] **P0** Platform-dependent compilation symbols are validated.
- [ ] **P1** Assemblies are separated with assembly definitions where this materially improves compile boundaries.
- [ ] **P1** High-risk systems have unit or integration tests.
- [ ] **P1** Public APIs and complex systems have maintainable documentation.
- [ ] **P2** Static analysis and formatting are automated.

---

# 5. Core Gameplay

- [ ] **P0** The complete core gameplay loop works from launch to exit.
- [ ] **P0** Every required level, mission, map and mode is completable.
- [ ] **P0** Win, loss, retry, restart, pause and quit flows work.
- [ ] **P0** The player cannot become permanently stuck through normal gameplay.
- [ ] **P0** Out-of-bounds conditions are detected and recovered.
- [ ] **P0** Spawn points and checkpoints are safe.
- [ ] **P0** Mission objectives update correctly and cannot enter contradictory states.
- [ ] **P0** Tutorials cannot deadlock the game.
- [ ] **P0** Difficulty progression has been tested by players outside the development team.
- [ ] **P0** Game balance has been tested with representative player skill levels.
- [ ] **P0** Rewards, scores, unlocks and progression cannot be duplicated through obvious exploits.
- [ ] **P1** Speed-running, unusual input order and repeated restart behaviour have been tested.
- [ ] **P1** Long sessions do not produce progressive degradation or broken state.

---

# 6. Save, Load and Player Data

- [ ] **P0** Save data works after quitting and reopening the application.
- [ ] **P0** Save operations are atomic or otherwise protected from partial writes.
- [ ] **P0** The game handles corrupt, missing, empty and incompatible save files.
- [ ] **P0** Save data has a schema or version number.
- [ ] **P0** Migration from every publicly released save version is tested.
- [ ] **P0** Save failure is surfaced to the player instead of silently losing progress.
- [ ] **P0** Reset-progress and delete-data functions work.
- [ ] **P0** Sensitive local data is protected appropriately.
- [ ] **P0** Cloud saves, if present, handle conflicts, offline changes and multiple devices.
- [ ] **P0** Settings and controls persist independently from gameplay progress where appropriate.
- [ ] **P0** Reinstall and application-update behaviour is understood on every platform.
- [ ] **P1** Backup or recovery behaviour is documented.
- [ ] **P1** Save files are tested under low-storage and forced-termination conditions.

---

# 7. Input and Device Support

- [ ] **P0** Keyboard and mouse controls work.
- [ ] **P0** All officially supported controllers work with correct glyphs.
- [ ] **P0** Touch controls work on supported mobile devices.
- [ ] **P0** Controls can be rebound where required by the product.
- [ ] **P0** Input settings can be restored to defaults.
- [ ] **P0** Input does not pass through UI into gameplay unintentionally.
- [ ] **P0** Controller disconnect and reconnect are handled.
- [ ] **P0** Focus loss, application switching and window deactivation are handled.
- [ ] **P0** Simultaneous input devices do not create unstable switching.
- [ ] **P0** Dead zones, sensitivity, inversion and acceleration settings are validated.
- [ ] **P0** Mobile orientation changes are either supported or explicitly locked.
- [ ] **P1** Common accessibility controllers and alternative input paths are considered.
- [ ] **P1** Haptics and vibration can be disabled.

---

# 8. User Interface and User Experience

- [ ] **P0** All buttons, toggles, dropdowns, sliders and text fields work.
- [ ] **P0** UI navigation works with mouse, keyboard, controller and touch where supported.
- [ ] **P0** No UI element is clipped at supported resolutions and aspect ratios.
- [ ] **P0** Safe areas are respected on phones and tablets.
- [ ] **P0** Text remains readable at minimum supported resolution.
- [ ] **P0** Loading, saving, downloading and network activity have visible status.
- [ ] **P0** Destructive actions require confirmation where appropriate.
- [ ] **P0** Error messages explain what happened and what the player can do next.
- [ ] **P0** Repeated button presses cannot duplicate transactions, scene loads or requests.
- [ ] **P0** Pause behaviour is correct for gameplay, audio, physics, UI and networking.
- [ ] **P0** Cursor lock, visibility and confinement work on desktop.
- [ ] **P0** First-run flow, permissions and onboarding are complete.
- [ ] **P1** UI scales correctly with user-configurable text size.
- [ ] **P1** Motion, flashing and camera effects have reduction options where appropriate.
- [ ] **P1** A consistent design system is used for spacing, typography and state feedback.

---

# 9. Accessibility

- [ ] **P0** Essential information is not communicated through colour alone.
- [ ] **P0** Text contrast is readable.
- [ ] **P0** Subtitles are available for required spoken information.
- [ ] **P0** Subtitle size and background are readable.
- [ ] **P0** Critical game actions are not dependent only on audio cues.
- [ ] **P0** Camera shake, motion blur and intense post-processing can be reduced or disabled where appropriate.
- [ ] **P0** Control rebinding covers every essential action.
- [ ] **P1** Colour-vision options have been tested.
- [ ] **P1** Difficulty or assistance settings are available where compatible with the game design.
- [ ] **P1** Menus have logical navigation order and clear focus indicators.
- [ ] **P2** Screen-reader support is evaluated where technically and commercially relevant.

---

# 10. Localisation

- [ ] **P0** All player-facing strings are externalised from code and prefabs.
- [ ] **P0** No untranslated placeholder or developer text appears.
- [ ] **P0** Variables, pluralisation, dates, time and numeric formats are correct.
- [ ] **P0** Text expansion has been tested.
- [ ] **P0** Fonts include every required character.
- [ ] **P0** Text is not clipped in any supported language.
- [ ] **P0** Right-to-left layout is tested if supported.
- [ ] **P0** Images containing text have localised variants or have been redesigned.
- [ ] **P0** Voice, subtitles and UI language combinations behave correctly.
- [ ] **P1** Store descriptions, support content and legal text use the same approved terminology.

---

# 11. Graphics and Rendering

- [ ] **P0** The rendering pipeline is final and consistent across scenes.
- [ ] **P0** Graphics APIs are explicitly tested on supported hardware.
- [ ] **P0** Shader compilation and shader-variant behaviour are tested in a clean build.
- [ ] **P0** No pink or missing-material objects appear.
- [ ] **P0** Materials, textures, meshes and animations use valid production references.
- [ ] **P0** Texture import settings are appropriate per platform.
- [ ] **P0** Texture compression is visually validated on target devices.
- [ ] **P0** Mesh compression and optimisation do not damage required detail.
- [ ] **P0** LOD groups transition acceptably.
- [ ] **P0** Occlusion culling does not hide visible geometry.
- [ ] **P0** Lighting, shadows and reflection probes are correct in every scene.
- [ ] **P0** Baked lighting is current and contains no invalid data.
- [ ] **P0** Post-processing is included in performance budgets.
- [ ] **P0** Camera clipping planes and depth precision are appropriate.
- [ ] **P0** Dynamic resolution or quality scaling behaves correctly if used.
- [ ] **P1** GPU instancing, static batching or SRP Batcher usage has been evaluated.
- [ ] **P1** Overdraw and transparent effects have been profiled.
- [ ] **P1** Graphics quality presets create meaningful performance differences.

---

# 12. Audio

- [ ] **P0** Music, sound effects, ambience, UI audio and voice all play correctly.
- [ ] **P0** Audio mixer routing is complete.
- [ ] **P0** Master, music, effects and voice volume settings work and persist.
- [ ] **P0** No clipping, distortion or unintended loudness spikes occur.
- [ ] **P0** Audio compression and load types are appropriate for each platform.
- [ ] **P0** Long audio files stream where appropriate.
- [ ] **P0** Simultaneous sound limits prevent excessive voice counts.
- [ ] **P0** Audio behaves correctly during pause, focus loss, backgrounding and scene loads.
- [ ] **P0** Missing audio devices are handled.
- [ ] **P1** Spatial audio and attenuation are validated at gameplay distances.
- [ ] **P1** Audio ducking and priority rules are tested.
- [ ] **P1** Legal rights exist for every music track, sound effect and voice recording.

---

# 13. Physics, Animation and AI

- [ ] **P0** Physics behaviour is tested at the production fixed timestep.
- [ ] **P0** Physics is stable at low and unstable frame rates.
- [ ] **P0** Collision layers and matrices are reviewed.
- [ ] **P0** Fast objects do not tunnel through critical colliders.
- [ ] **P0** Ragdolls, joints and vehicles cannot generate uncontrolled energy or NaN states.
- [ ] **P0** Animation transitions do not become stuck.
- [ ] **P0** Root motion and physics interaction are correct.
- [ ] **P0** Animator parameters return to valid states after interruption.
- [ ] **P0** Navigation data is current for every released scene.
- [ ] **P0** AI can recover from unreachable targets, blocked routes and missing references.
- [ ] **P0** AI processing remains within CPU budgets at maximum population.
- [ ] **P1** Determinism requirements are documented and tested where relevant.
- [ ] **P1** Extreme velocities, coordinates and timescales have been tested.

---

# 14. Scenes, Assets and Content Integrity

- [ ] **P0** Every production scene is included in the correct build profile.
- [ ] **P0** Test scenes and developer assets are excluded.
- [ ] **P0** There are no missing scripts.
- [ ] **P0** There are no missing prefab, material, texture, animation or audio references.
- [ ] **P0** Duplicate assets and accidental high-resolution source files are reviewed.
- [ ] **P0** Resources-folder usage is intentional and measured.
- [ ] **P0** Addressables or AssetBundles load correctly from a clean install.
- [ ] **P0** Remote content has versioning, integrity checking and rollback capability.
- [ ] **P0** Content catalogue failures and unavailable CDN responses are handled.
- [ ] **P0** Assets are unloaded correctly after leaving maps or modes.
- [ ] **P0** A production build does not depend on files outside the packaged application or configured remote content.
- [ ] **P1** Asset naming and folder conventions are consistent.
- [ ] **P1** Unused assets and variants are removed from production content.

---

# 15. Loading, Streaming and Memory

- [ ] **P0** Startup time is measured on minimum-spec hardware.
- [ ] **P0** Scene and map loading time is measured.
- [ ] **P0** Loading screens never appear frozen.
- [ ] **P0** Large synchronous asset loads are removed from active gameplay.
- [ ] **P0** Memory usage is measured in a player build on target hardware.
- [ ] **P0** Peak memory remains below the platform-specific budget.
- [ ] **P0** Repeated scene changes do not continuously increase memory.
- [ ] **P0** Managed allocations and garbage-collection spikes are profiled.
- [ ] **P0** Texture, mesh, audio and render-target memory are reviewed.
- [ ] **P0** Object pooling is used only where profiling shows it is justified.
- [ ] **P0** Low-memory warnings or operating-system termination risks are handled where supported.
- [ ] **P0** Streaming systems recover from slow, interrupted or failed downloads.
- [ ] **P1** Memory snapshots are compared before and after long sessions.
- [ ] **P1** Cache size and cache invalidation rules are defined.

---

# 16. Performance

- [ ] **P0** Performance is measured in a standalone/device build, not only in the Editor.
- [ ] **P0** CPU, GPU, rendering, memory, audio, physics and network usage are profiled.
- [ ] **P0** Minimum-spec hardware sustains the declared performance target during worst-case gameplay.
- [ ] **P0** Frame-time spikes are measured, not hidden behind average FPS.
- [ ] **P0** The 1% low or comparable worst-frame metric is tracked.
- [ ] **P0** Maximum enemies, drones, particles, UI elements and effects are tested simultaneously.
- [ ] **P0** Performance is tested after extended play, not only immediately after launch.
- [ ] **P0** Thermal throttling is tested on mobile devices.
- [ ] **P0** Battery consumption is evaluated on mobile.
- [ ] **P0** Quality presets are validated on real low-, medium- and high-end hardware.
- [ ] **P0** VSync and frame-rate caps behave correctly.
- [ ] **P0** Resolution and display-mode changes do not break rendering or UI.
- [ ] **P1** Performance captures are stored as release evidence.
- [ ] **P1** Automated performance regression tests exist for critical scenarios.

### Required budget fields

- [ ] Target FPS: `________`
- [ ] Maximum main-thread frame time: `________ ms`
- [ ] Maximum GPU frame time: `________ ms`
- [ ] Maximum peak memory: `________ MB/GB`
- [ ] Maximum startup time: `________ seconds`
- [ ] Maximum map/scene load time: `________ seconds`
- [ ] Maximum production build size: `________ MB/GB`
- [ ] Minimum supported hardware: `________________________`

---

# 17. Networking and Online Services

Skip this section only if the game has no network dependency.

- [ ] **P0** The game handles no internet connection.
- [ ] **P0** The game handles slow, unstable and high-latency connections.
- [ ] **P0** Timeouts exist for every network request.
- [ ] **P0** Retries use controlled backoff and do not create request storms.
- [ ] **P0** Duplicate requests are idempotent where required.
- [ ] **P0** Authentication expiry and refresh are handled.
- [ ] **P0** Server maintenance and version incompatibility return clear player messages.
- [ ] **P0** Client and server versions have an explicit compatibility policy.
- [ ] **P0** Network disconnect during gameplay has defined recovery behaviour.
- [ ] **P0** Downloaded content is validated before use.
- [ ] **P0** TLS certificate and hostname validation are not bypassed.
- [ ] **P0** No privileged server secret exists in the client.
- [ ] **P0** Server-authoritative validation protects valuable state where cheating matters.
- [ ] **P0** Rate limiting, abuse prevention and basic denial-of-service protections exist.
- [ ] **P0** Backend dependency outages have a degraded-mode or clear failure path.
- [ ] **P1** Network simulation covers packet loss, jitter, latency and reconnects.
- [ ] **P1** Service-level monitoring and alerts are configured.

---

# 18. Security and Privacy

- [ ] **P0** No API secret, private key, signing password or administrator credential ships in the client.
- [ ] **P0** Sensitive traffic is encrypted in transit.
- [ ] **P0** Sensitive local data is encrypted or minimised based on risk.
- [ ] **P0** User input, filenames, URLs and remote content are validated.
- [ ] **P0** Download paths cannot overwrite arbitrary local files.
- [ ] **P0** Deep links and custom URL schemes validate all parameters.
- [ ] **P0** Debug consoles and remote administration interfaces are disabled in production.
- [ ] **P0** Analytics and crash reports do not collect unnecessary personal data.
- [ ] **P0** Consent flows match actual data collection.
- [ ] **P0** Account deletion and personal-data deletion processes work where required.
- [ ] **P0** A privacy policy reflects the released build, SDKs and services.
- [ ] **P0** Third-party SDK data collection is inventoried.
- [ ] **P0** Known vulnerable dependencies have been reviewed and remediated.
- [ ] **P1** A security contact and vulnerability-reporting process exist.
- [ ] **P1** A basic threat model covers accounts, payments, multiplayer, downloads and user-generated content.

---

# 19. Automated and Manual Testing

- [ ] **P0** Edit Mode tests pass.
- [ ] **P0** Play Mode tests pass.
- [ ] **P0** Platform player tests pass where applicable.
- [ ] **P0** Smoke tests cover application launch, core gameplay, save/load and exit.
- [ ] **P0** Regression tests cover previously fixed critical bugs.
- [ ] **P0** Every production platform has a documented manual test pass.
- [ ] **P0** Clean-install testing is complete.
- [ ] **P0** Upgrade-from-previous-version testing is complete.
- [ ] **P0** Offline, interrupted-download and reconnect testing is complete.
- [ ] **P0** Low-storage testing is complete.
- [ ] **P0** Suspend, resume, background and foreground testing is complete where applicable.
- [ ] **P0** Forced termination and crash-recovery behaviour are tested.
- [ ] **P0** Date, time, timezone and locale edge cases are tested where relevant.
- [ ] **P0** Input-spam and rapid-navigation tests are complete.
- [ ] **P0** A soak test runs for the longest realistic session duration.
- [ ] **P0** No unresolved blocker or critical bug remains.
- [ ] **P0** Every accepted known issue has a documented impact and workaround.
- [ ] **P1** Test coverage and escaped-defect trends are tracked.
- [ ] **P1** External testers have completed a release-candidate pass.

---

# 20. Build and Release Engineering

- [ ] **P0** Production builds are created using a documented or automated process.
- [ ] **P0** Development Build is disabled for the final player.
- [ ] **P0** Script Debugging and Autoconnect Profiler are disabled.
- [ ] **P0** Production scripting backend and architecture are correct.
- [ ] **P0** IL2CPP/Mono selection is tested and intentional.
- [ ] **P0** Code stripping level is tested against reflection, serialisation and plugin behaviour.
- [ ] **P0** Product name, company name, bundle identifier and package identifier are correct.
- [ ] **P0** Version and build numbers are correct and monotonic where required.
- [ ] **P0** Application icons, splash screens and launch screens are final.
- [ ] **P0** Signing certificates and provisioning profiles are valid.
- [ ] **P0** Production entitlement and permission files contain only required capabilities.
- [ ] **P0** Build output is scanned for accidental secrets and debug files.
- [ ] **P0** Build reports are reviewed for size regressions and unexpected content.
- [ ] **P0** Release artefact hashes are recorded.
- [ ] **P0** Debug symbols are archived for crash analysis but not exposed unnecessarily.
- [ ] **P0** A rollback build remains available.
- [ ] **P1** CI creates signed release candidates from release tags.
- [ ] **P1** Releasing the same version twice accidentally is prevented.

---

# 21. Logging, Crash Reporting and Analytics

- [ ] **P0** Production logs use appropriate severity levels.
- [ ] **P0** Repetitive logs cannot flood disk, console or network.
- [ ] **P0** Logs do not contain passwords, tokens, personal data or private content.
- [ ] **P0** Crash reporting is integrated and verified with a deliberate test crash.
- [ ] **P0** Native symbols and managed symbols can be matched to each release.
- [ ] **P0** Version, platform, device and scene context are attached to diagnostic events.
- [ ] **P0** Critical failures can be diagnosed without reproducing them on a developer machine.
- [ ] **P0** Analytics event names and properties are documented.
- [ ] **P0** Analytics events have been verified in the production or pre-production environment.
- [ ] **P0** Consent and opt-out behaviour work correctly.
- [ ] **P0** Operational dashboards distinguish versions and platforms.
- [ ] **P1** Performance telemetry detects regressions after release.
- [ ] **P1** Alerts exist for crash spikes, login failures and backend errors.

---

# 22. Platform Gate — Windows

- [ ] **P0** x64 production build launches on a clean supported Windows installation.
- [ ] **P0** Required runtimes and redistributables are included or documented.
- [ ] **P0** Install, update, repair and uninstall flows are tested.
- [ ] **P0** Installation does not require administrator access unless justified.
- [ ] **P0** The game works from paths containing spaces and non-ASCII characters.
- [ ] **P0** The game works for a non-administrator Windows account.
- [ ] **P0** Windowed, borderless and fullscreen modes behave correctly.
- [ ] **P0** Multi-monitor behaviour is acceptable.
- [ ] **P0** Alt+Tab, focus loss and display changes are handled.
- [ ] **P0** Common GPU vendors and driver versions are tested.
- [ ] **P0** Antivirus and reputation false positives are checked.
- [ ] **P0** Crash dumps and Player logs can be located and interpreted.
- [ ] **P1** Command-line options are documented and validated if supported.
- [ ] **P1** Steam, Epic or other store integrations are tested in production-like branches.

---

# 23. Platform Gate — Android

- [ ] **P0** Correct package name, version code and version name are configured.
- [ ] **P0** A release keystore is secured and backed up.
- [ ] **P0** Production builds are signed with the correct key.
- [ ] **P0** Required CPU architectures are included.
- [ ] **P0** Target and minimum Android API levels meet current distribution requirements.
- [ ] **P0** Runtime permissions are requested only when needed and denial is handled.
- [ ] **P0** Back-button behaviour is correct.
- [ ] **P0** App switching, phone calls, screen lock and resume are tested.
- [ ] **P0** Low-memory termination and process recreation are tested.
- [ ] **P0** Multiple chipsets, GPU families, aspect ratios and device tiers are tested.
- [ ] **P0** Thermal and battery behaviour are acceptable.
- [ ] **P0** Installation size and download size meet the distribution strategy.
- [ ] **P0** Play Store testing tracks or equivalent pre-release distribution are used.
- [ ] **P0** Store data-safety declarations match actual SDK behaviour.
- [ ] **P1** Foldable, tablet and external-controller behaviour are tested if supported.

---

# 24. Platform Gate — iOS/iPadOS

- [ ] **P0** Correct bundle identifier, version and build number are configured.
- [ ] **P0** Production signing, certificates and provisioning are valid.
- [ ] **P0** Required device architectures are included.
- [ ] **P0** Minimum supported iOS/iPadOS version is explicit.
- [ ] **P0** Permission usage descriptions are accurate and complete.
- [ ] **P0** Permission denial and later settings changes are handled.
- [ ] **P0** Backgrounding, interruptions, screen lock and resume are tested.
- [ ] **P0** Safe areas, notches and Dynamic Island layouts are tested where relevant.
- [ ] **P0** iPhone and iPad layouts are tested if both are supported.
- [ ] **P0** Memory pressure and operating-system termination are tested.
- [ ] **P0** Device thermal behaviour and battery usage are acceptable.
- [ ] **P0** TestFlight release candidate matches the intended App Store build.
- [ ] **P0** Privacy manifest and store privacy declarations match the application and SDKs.
- [ ] **P0** App-review notes and test accounts are prepared where needed.
- [ ] **P1** Controller, keyboard and pointer support are tested if advertised.

---

# 25. Platform Gate — Web

- [ ] **P0** The build loads over the production hosting configuration, not only from localhost.
- [ ] **P0** HTTPS, compression and correct MIME types are configured.
- [ ] **P0** Loading progress and failure messages are visible.
- [ ] **P0** Initial download size is measured and reduced to an acceptable budget.
- [ ] **P0** Browser cache and content-version invalidation work.
- [ ] **P0** Supported desktop and mobile browsers are explicitly listed and tested.
- [ ] **P0** Browser memory limits are respected.
- [ ] **P0** Browser tab backgrounding and restoration are handled.
- [ ] **P0** Fullscreen and pointer-lock flows work after required user gestures.
- [ ] **P0** Keyboard shortcuts do not conflict unacceptably with browser behaviour.
- [ ] **P0** IndexedDB or other local persistence works and failure is handled.
- [ ] **P0** Cross-origin requests, CDN headers and remote asset access work.
- [ ] **P0** Mobile browser limitations are documented honestly.
- [ ] **P0** The production build is tested after clearing all browser data.
- [ ] **P1** Slow-network testing covers first load and content updates.
- [ ] **P1** A fallback experience exists for unsupported browsers or devices.

---

# 26. Store, Legal and Compliance

- [ ] **P0** The game title, logo, screenshots and marketing claims match the released product.
- [ ] **P0** Rights exist for all code, art, fonts, music, voice, maps, data and trademarks.
- [ ] **P0** Open-source licence obligations are satisfied.
- [ ] **P0** Third-party notices are included where required.
- [ ] **P0** Age rating questionnaires are accurate.
- [ ] **P0** Privacy policy and terms are published and accessible.
- [ ] **P0** Cookie or tracking consent is implemented where applicable.
- [ ] **P0** Purchases, subscriptions and refunds follow platform rules if monetisation is present.
- [ ] **P0** Prices, currencies, tax treatment and entitlements are correct.
- [ ] **P0** Ads and rewarded ads behave correctly and do not block progression.
- [ ] **P0** User-generated content has moderation, reporting and removal processes if present.
- [ ] **P0** Export-control, sanctions and regional restrictions are reviewed where relevant.
- [ ] **P0** Map, imagery and geospatial-data attribution requirements are satisfied.
- [ ] **P1** Legal documents are versioned and linked to release versions.

---

# 27. Support and Live Operations

- [ ] **P0** A working support contact is visible to players.
- [ ] **P0** Support can identify the player's game version, platform and relevant logs.
- [ ] **P0** A known-issues page or internal knowledge base exists.
- [ ] **P0** Incident severity and escalation rules are defined.
- [ ] **P0** Server, CDN, authentication and storage services are monitored.
- [ ] **P0** Backups and restore procedures exist for production services.
- [ ] **P0** A rollback process is documented and tested.
- [ ] **P0** A hotfix can be built, tested, signed and distributed.
- [ ] **P0** Remote configuration has validation, access control and rollback.
- [ ] **P0** Feature flags default safely when configuration is unavailable.
- [ ] **P0** Maintenance messages can be displayed without shipping a new client where feasible.
- [ ] **P1** Launch-day staffing and decision authority are defined.
- [ ] **P1** Community, review and support channels have moderation coverage.

---

# 28. AeroTerra / Large-World Drone Simulator Gate

This section is specifically relevant to AeroTerra-style drone simulation using real maps, streamed terrain and platform-specific previews.

## Maps and Geospatial Systems

- [ ] **P0** Every map provider and dataset is licensed for the intended public use.
- [ ] **P0** Required map and imagery attribution is visible and correct.
- [ ] **P0** Map API keys are restricted by application, domain, platform or service where supported.
- [ ] **P0** The client does not contain unrestricted privileged map-service credentials.
- [ ] **P0** Real-map loading works on Windows, Android, iOS and Web targets that are advertised.
- [ ] **P0** No-connection, weak-connection and rate-limit behaviour is handled clearly.
- [ ] **P0** Missing terrain tiles, imagery tiles and 3D-building tiles do not crash the game.
- [ ] **P0** Tile retries, cancellation and cache limits are controlled.
- [ ] **P0** Long-distance coordinate precision is tested.
- [ ] **P0** Floating-origin or equivalent large-world precision management is stable.
- [ ] **P0** Physics, particles, trails, cameras and mission markers remain correct after origin shifts.
- [ ] **P0** Terrain collider loading and visual tile loading stay synchronised sufficiently for gameplay.
- [ ] **P0** Spawn locations use validated altitude and coordinate systems.
- [ ] **P0** Latitude, longitude, altitude, heading and local Unity coordinates convert correctly.
- [ ] **P0** Dateline, hemisphere, timezone and negative-coordinate cases are tested where relevant.
- [ ] **P0** Zagreb, Barcelona, Dubai, London, New York, Paris, Riyadh and Tokyo content passes the same validation criteria.
- [ ] **P1** Tile cache can be cleared and repaired.
- [ ] **P1** Download bandwidth and cache storage can be limited by the player.

## Drone Flight

- [ ] **P0** Drone acceleration, braking, yaw, pitch, roll and altitude controls are stable at the production timestep.
- [ ] **P0** Drone behaviour is stable across the supported frame-rate range.
- [ ] **P0** Ground effect, wind, weather and payload changes behave consistently if simulated.
- [ ] **P0** Maximum speed, altitude, range and battery limits are enforced as designed.
- [ ] **P0** Battery depletion has a controlled gameplay outcome.
- [ ] **P0** Collision and crash detection work at maximum speed.
- [ ] **P0** The drone cannot pass through terrain or buildings during tile transitions.
- [ ] **P0** Reset, respawn and recovery never place the drone inside geometry.
- [ ] **P0** Front, rear and thermal camera modes switch reliably.
- [ ] **P0** Camera feeds and render textures are released correctly.
- [ ] **P0** Thermal rendering has an acceptable fallback on unsupported hardware.
- [ ] **P0** Input works with keyboard, mouse, controller and supported touch controls.
- [ ] **P0** Camera shake, speed blur and other high-motion effects can be reduced.
- [ ] **P1** Flight telemetry can be exported or attached to bug reports.
- [ ] **P1** Optional realistic and assisted control modes are separately balanced and tested.

## Missions and Modes

- [ ] **P0** Training, cargo, combat and racing modes have complete entry, gameplay, success, failure and exit flows.
- [ ] **P0** Cargo attachment, transport and delivery cannot enter invalid states.
- [ ] **P0** Payload use is synchronised with inventory, UI and mission objectives.
- [ ] **P0** Racing checkpoints validate direction and order correctly.
- [ ] **P0** Mission markers remain geographically aligned after origin shifts or map reloads.
- [ ] **P0** Weather changes do not invalidate missions or make required objectives impossible without a defined failure path.
- [ ] **P0** Narration cannot overlap uncontrollably or repeat on every frame/trigger.
- [ ] **P1** Mission data is configurable without code changes.
- [ ] **P1** Mission replay and restart do not leak objects, memory or subscriptions.

---

# 29. Release-Candidate Sign-off

A release candidate may be approved only when all statements below are true.

- [ ] All **P0** checklist items applicable to the game are complete.
- [ ] Zero unresolved blocker bugs remain.
- [ ] Zero unresolved critical bugs remain, unless formally accepted by the release authority with documented impact.
- [ ] The exact release artefact passed QA; it was not rebuilt after approval.
- [ ] Clean-install and upgrade testing passed.
- [ ] Minimum-spec performance passed.
- [ ] Crash reporting, logs and symbols were verified.
- [ ] Store or distribution metadata matches the build.
- [ ] Legal, privacy, licences and attribution passed review.
- [ ] Support, monitoring, backup and rollback are operational.
- [ ] The release commit, build number, artefact hash and approval record are archived.

## Final approval record

| Field | Value |
|---|---|
| Product | |
| Version | |
| Build number | |
| Commit/tag | |
| Target platforms | |
| Release artefact hash | |
| QA owner | |
| Technical owner | |
| Product owner | |
| Legal/privacy approval | |
| Go/no-go decision | |
| Decision date | |
| Accepted known issues | |
| Rollback version | |

---

# Recommended Release Dashboard

Track these values for each release candidate:

| Metric | Target | Actual | Pass/Fail |
|---|---:|---:|---|
| Blocker bugs | 0 | | |
| Critical bugs | 0 | | |
| Automated test pass rate | 100% of required tests | | |
| Crash-free test sessions | Project-defined | | |
| Minimum-spec FPS | Project-defined | | |
| 1% low FPS or worst-frame metric | Project-defined | | |
| Peak memory | Below platform budget | | |
| Startup time | Below project budget | | |
| Map load time | Below project budget | | |
| Clean-install test | Pass | | |
| Upgrade test | Pass | | |
| Offline/reconnect test | Pass | | |
| Save migration test | Pass | | |
| Legal/licence review | Pass | | |
| Rollback test | Pass | | |

---

## Hard Go/No-Go Rule

**Do not release** when any of the following is true:

- The game can lose or corrupt player progress.
- A core mode or required mission cannot be completed.
- The production build crashes during a normal or repeatable flow.
- Minimum-spec hardware cannot maintain the declared performance target.
- The release cannot be reproduced from source control.
- Signing credentials, source secrets or privileged service credentials are exposed.
- Required licences, map attribution, privacy disclosures or store declarations are missing.
- There is no verified rollback, hotfix or diagnostic path.
- QA tested a different binary from the one intended for release.
