# sidecontract — Progress

Updated: 2026-11-12 (Asia/Bangkok)
Repository: https://github.com/RobbyGrean/sidecontract

## Source of truth and working across computers

- GitHub is the shared source of truth. `main` remains the buildable sidecontract `1.0.0` baseline until unfinished work is integrated.
- Active work checkpoint: `codex/procurement-tor-wip`.
- On an existing clone, run `git fetch origin`, switch to `codex/procurement-tor-wip`, then `git pull --ff-only` before continuing.
- After each meaningful work session, update this file, commit, and push so another computer can resume from GitHub.

## Completed baseline

- Created a standalone sidecontract repository and Git history, separate from Jangmao70.
- Renamed product source and build files to Sidecontract/sidecontract names.
- Set app identity, installer AppId, and install folder to `sidecontract` / `%LOCALAPPDATA%\sidecontract`.
- Set the app and installer version to `1.0.0`; the public updater manifest points to this repository.
- Built the Windows executable and installer successfully for the baseline. The baseline artifacts and `update.json` are in `assets/downloads`.

## Current work in progress

The WIP branch contains a Procurement TOR change:

- Its latest template checkpoint removes 13 route-specific files under `Template/Procurement/3.TOR/` and adds `Template/Procurement/3. TOR.docx`.
- The new file is a populated, case-specific document with no app placeholder tokens. Its work type is not in `Config/Procurement/position_catalog.json`.
- The current app has 15 manifest routes. `ProcurementModule` matches each route by position, teaching flag, and two-school flag; each route also names a TOR/quote pair, hashes, and page counts. The new document cannot replace those existing routes as-is.
- `template_manifest.json` still references the 13 removed files and their hashes, so the WIP branch is not buildable as-is.
- The owner confirmed the document is intended to be public and plans to replace its filled values with `{tag}` placeholders later. That conversion is not complete yet.
- The renderer supports `{...}` tokens up to 80 characters and matches them across text runs in a paragraph. Procurement currently has 53 literal tag names plus generated person/committee tags; tokens without a `BuildValues` mapping remain unfilled and require app-side field mapping.
- Do not build or publish a new installer from this WIP state. The last successful build was the unchanged `main` baseline.

## Next steps

1. Wait for the owner to finish replacing the case-specific values with `{tag}` placeholders.
2. Reconcile the tagged TOR with the app route model and decide whether the existing 13 route variants remain supported or need an explicit migration.
3. Rebuild and verify all affected procurement document cases after integration.
4. Update release artifacts and merge the work into `main` only after it is buildable and verified.

## Work log

- 2026-11-12: Published the standalone sidecontract `1.0.0` baseline to `main`.
- 2026-11-12: Checkpointed the TOR file changes on `codex/procurement-tor-wip`.
- 2026-11-12: Confirmed the document is intended to be public; the owner plans to replace filled values with `{tag}` placeholders before app integration.
- 2026-11-12: Audited placeholder rendering; new tags need app-side value mapping, while tags split across Word text runs are supported.



