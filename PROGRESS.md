# sidecontract — Progress

Updated: 2026-11-12 (Asia/Bangkok)
Repository: https://github.com/RobbyGrean/sidecontract

## Source of truth and working across computers

- GitHub is the shared source of truth. `main` remains the buildable sidecontract `1.0.0` baseline until unfinished work is integrated.
- Active work checkpoint: `codex/procurement-tor-wip`.
- On an existing clone, run `git fetch origin`, switch to `codex/procurement-tor-wip`, then `git pull --ff-only` before continuing.
- After each meaningful work session, update this file, commit, and push so another computer can resume from GitHub.
- Workspace check on 2026-11-12 found the selected checkout was linked to a local repository with Jangmao70 history, not this standalone project. No files in that checkout were changed in this session. Resume from the standalone clone and preserve its pending local changes.

## Completed baseline

- Created a standalone sidecontract repository and Git history, separate from Jangmao70.
- Renamed product source and build files to Sidecontract/sidecontract names.
- Set app identity, installer AppId, and install folder to `sidecontract` / `%LOCALAPPDATA%\sidecontract`.
- Set the app and installer version to `1.0.0`; the public updater manifest points to this repository.
- Static identity check on 2026-11-12 confirmed app data, installer ID/path, and updater downloads are scoped to sidecontract.
- Built the Windows executable and installer successfully for the baseline. The baseline artifacts and `update.json` are in `assets/downloads`.
- Rebuilt `sidecontract.exe` from current `main` on 2026-11-12 with `build-exe-sidecontract.ps1`; the build succeeded. The full installer was not regenerated.

## Current work in progress

The WIP branch contains a Procurement TOR change:

- Its latest template checkpoint removes 13 route-specific files under `Template/Procurement/3.TOR/` and adds `Template/Procurement/3. TOR.docx`.
- The new file is a populated, case-specific document with no app placeholder tokens. Its work type is not in `Config/Procurement/position_catalog.json`.
- The current app has 15 manifest routes. `ProcurementModule` matches each route by position, teaching flag, and two-school flag; each route also names a TOR/quote pair, hashes, and page counts. The new document cannot replace those existing routes as-is.
- `template_manifest.json` still references the 13 removed files and their hashes, so the WIP branch is not buildable as-is.
- The owner confirmed the document is intended to be public and plans to add `{tag}` placeholders later. TOR document edits are paused for now at the owner's request; no document, manifest, or release changes are underway.
- The renderer supports `{...}` tokens up to 80 characters and matches them across text runs in a paragraph. Procurement currently has 53 literal tag names plus generated person/committee tags; tokens without a `BuildValues` mapping remain unfilled and require app-side field mapping.
- Do not build or publish a new installer from this WIP state. The last successful build was the unchanged `main` baseline.

## Next steps

1. Resume from the standalone clone after preserving its existing local changes.
2. Keep TOR changes parked until the owner asks to resume document work.
2. Reconcile the tagged TOR with the app route model and decide whether the existing 13 route variants remain supported or need an explicit migration.
3. Rebuild and verify all affected procurement document cases after integration.
4. Update release artifacts and merge the work into `main` only after it is buildable and verified.

## Work log

- 2026-11-12: Published the standalone sidecontract `1.0.0` baseline to `main`.
- 2026-11-12: Checkpointed the TOR file changes on `codex/procurement-tor-wip`.
- 2026-11-12: Confirmed the document is intended to be public; the owner plans to replace filled values with `{tag}` placeholders before app integration.
- 2026-11-12: Audited placeholder rendering; new tags need app-side value mapping, while tags split across Word text runs are supported.
- 2026-11-12: Rebuilt the executable from current main; compilation succeeded.
- 2026-11-12: Owner asked to pause TOR document work; no feature coding or document edits were started in this session.
- 2026-11-12: Verified the public GitHub repository directly: `main` was at `8edf1b5` and `codex/procurement-tor-wip` was at `e50c827`. The selected workspace was a separate Jangmao70-linked checkout; no source or template files were changed. The standalone clone's existing local changes remain untouched.
- 2026-11-12: Audited the standalone app identity and replaced three residual `346` labels in two C# files (two user-facing error messages and one renderer marker). No templates were edited; no tests or build were run.
