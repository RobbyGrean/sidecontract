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

The WIP branch contains the Procurement TOR change:

- Thirteen route-specific files under `Template/Procurement/3.TOR/` are removed, and `Template/Procurement/3. TOR.docx` is added.
- Review shows the new file is a populated, case-specific document with no app placeholder tokens. Its work type is not in `Config/Procurement/position_catalog.json`.
- `Config/Procurement/template_manifest.json` still references the removed files and hashes, so the WIP branch is not buildable as-is.
- The new document is present in the public WIP branch. Whether it is intended for public distribution is awaiting the owner's confirmation.
- Do not build or publish a new installer from this WIP state. The last successful build was the unchanged `main` baseline.

## Next steps

1. Resolve whether the case-specific document should remain public and whether it is an example or a new supported procurement workflow.
2. If it is a new workflow, convert it into a reusable template and add the required role, fields, route, and manifest data. If it is only an example, keep it out of shipped templates and restore the existing route files.
3. Rebuild and verify all affected procurement document cases after integration.
4. Update release artifacts and merge the work into `main` only after it is buildable and verified.

## Work log

- 2026-11-12: Published the standalone sidecontract `1.0.0` baseline to `main`.
- 2026-11-12: Checkpointed the TOR file changes on `codex/procurement-tor-wip`.
- 2026-11-12: Inspected the new TOR and found it is case-specific, not a reusable app template; recorded the gap and pending publication decision.
