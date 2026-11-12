# sidecontract — Progress

Updated: 2026-11-12 (Asia/Bangkok)
Repository: https://github.com/RobbyGrean/sidecontract

## Source of truth and working across computers

- GitHub is the shared source of truth. Keep the released `main` branch on the last buildable version until unfinished work is integrated.
- Stable baseline: `main`, sidecontract `1.0.0`.
- Active work checkpoint: `codex/procurement-tor-wip`.
- On an existing clone, run `git fetch origin`, switch to `codex/procurement-tor-wip`, then `git pull --ff-only` before continuing.
- After each meaningful work session, update this file, commit the work, and push the branch so another computer can resume from GitHub.

## Completed baseline

- Created a standalone sidecontract repository and Git history, separate from Jangmao70.
- Renamed product source and build files to Sidecontract/sidecontract names.
- Set app identity, installer AppId, and install folder to `sidecontract` / `%LOCALAPPDATA%\sidecontract`.
- Set the app and installer version to `1.0.0`; the public updater manifest points to this repository.
- Built the Windows executable and installer successfully for the baseline. The baseline artifacts and `update.json` are in `assets/downloads`.

## Current work in progress

The checkpoint branch records the current Procurement TOR template change:

- The current working tree shows 13 separate TOR files deleted under `Template/Procurement/3.TOR/`.
- The current working tree shows a new `Template/Procurement/3. TOR.docx`.
- `Config/Procurement/template_manifest.json` still references the removed files and their individual hashes. The app/build is therefore not yet integrated with this template change.
- No build or document-generation verification has been run after this change. Keep it on the checkpoint branch until integration and verification are complete; do not publish it as a new app update yet.

## Next steps

1. Confirm how the new TOR document should cover the existing position, teaching, and two-school cases.
2. Update template selection, manifest paths/hashes, and any related UI or processing logic to match that decision.
3. Verify document generation for the required cases, then build the executable and installer.
4. Update release artifacts and merge into `main` only after the integrated version is verified.

## Work log

- 2026-11-12: Published the standalone sidecontract `1.0.0` baseline to `main`.
- 2026-11-12: Preserved the in-progress TOR template change on `codex/procurement-tor-wip` and documented its current integration gap for work across computers.

