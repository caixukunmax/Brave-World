# Final Fix Round 4 — Map Generator Skill

## Summary

Addressed all Important issues and the feasible Minor issues identified in the fourth final whole-branch review. The changes harden path handling, backup isolation, sync validation, CLI input validation, and skill/CLI consistency around blueprints.

## Changes Made

### 1. Reject `.` and `..` as map names
- **File:** `clinetcsharp/tools/lib/map-core.js`
- Added explicit early check in `validateMapName` before the regex test.
- Names such as `v1.2` or `map.test` still pass because they are not exactly `.` or `..`.

### 2. Unique, out-of-tree backup directory per run
- **File:** `clinetcsharp/tools/generate-map.js`
- Replaced the fixed `.bak` suffix (which overwrote unrelated `.bak` files) with a unique temporary directory created via `fs.mkdtempSync(path.join(os.tmpdir(), 'mapgen-backup-'))`.
- Backed-up files are stored by basename inside that temp directory.
- `removeBackups` deletes the whole temp directory, so no stray `.bak` files are left in the map folder.

### 3. Containment check in `syncToServer`
- **File:** `clinetcsharp/tools/lib/sync.js`
- Reuses `validateMapName` and `assertContained` from `lib/map-core`.
- `syncToServer` now validates `mapName` and verifies `targetDir` stays inside `serverMapsDir` before creating directories or copying files.

### 4. Validate `--output-dir`
- **File:** `clinetcsharp/tools/generate-map.js`
- After resolving `outputDir`, the CLI checks it is contained within the resolved project root (`clinetcsharp/tools/../..`).
- Paths that escape the project tree are rejected with a clear error and `process.exit(1)`.

### 5. Blueprint persistence + skill consistency
- **Files:** `clinetcsharp/tools/generate-map.js`, `.superpowers/superpowers-main/skills/di-tu-sheng-cheng-qi/SKILL.md`
- When `--blueprint <path>` is provided, the source blueprint is copied into `mapDir/map-blueprint.json` after successful generation.
- When no `--blueprint` is provided, nothing is written and nothing is verified.
- Updated the skill's Post-Execution step 3 to state the conditional verification.

### 6. Accept valid decimal literals for ratios
- **File:** `clinetcsharp/tools/generate-map.js`
- `parseRatio` now accepts `.5` and `1.` via `Number()` plus a regex that allows optional integer/decimal parts.

### 7. Remove redundant size-cap check
- **File:** `clinetcsharp/tools/generate-map.js`
- Moved the size-cap enforcement into `parseSize` so it applies to both user-provided values and default values (e.g., oversized base maps in adjust mode).
- Removed the now-dead duplicate block after parsing.

### 8. Propagate malformed map JSON in sync
- **File:** `clinetcsharp/tools/lib/sync.js`
- `readMapData` no longer silently swallows JSON parse errors; it now lets them propagate so the caller can roll back and report the real problem.

### 9. Report file gitignore conflict
- **Files:** `.gitignore`, `.superpowers/sdd/.gitignore`
- Added exceptions for `final-fix-round4-report.md` so the report can be committed from `.superpowers/sdd/final-fix-round4-report.md`.

## Tests Added/Updated

- `clinetcsharp/tools/tests/cli.test.js`
  - Rejects map names `.` and `..`
  - Does not clobber unrelated `.bak` files in the map directory
  - Rejects an `--output-dir` outside the project root
  - Accepts decimal literals `.5` and `1.` for ratios
  - Copies the provided blueprint into the map directory
  - Does not create `map-blueprint.json` when no blueprint is provided

- `clinetcsharp/tools/tests/sync.test.js`
  - Rejects path traversal in `mapName`
  - Propagates malformed map JSON instead of silently swallowing

- `clinetcsharp/tools/tests/map-core.test.js`
  - Rejects `.` and `..` as map names
  - Accepts map names containing dots
  - Detects paths that escape a parent directory
  - Accepts paths contained within a parent directory

## Test Results

```
node --test tests/*.test.js
ℹ tests 67
ℹ suites 14
ℹ pass 67
ℹ fail 0
ℹ cancelled 0
ℹ skipped 0
ℹ todo 0
ℹ duration_ms ~2057
```

All existing tests continue to pass; no regressions detected.

## Files Changed

- `clinetcsharp/tools/generate-map.js`
- `clinetcsharp/tools/lib/map-core.js`
- `clinetcsharp/tools/lib/sync.js`
- `clinetcsharp/tools/tests/cli.test.js`
- `clinetcsharp/tools/tests/map-core.test.js`
- `clinetcsharp/tools/tests/sync.test.js`
- `.superpowers/superpowers-main/skills/di-tu-sheng-cheng-qi/SKILL.md`
- `.gitignore`
- `.superpowers/sdd/.gitignore`
- `.superpowers/sdd/final-fix-round4-report.md` (this report)

## Concerns

- `progress.md` under `.superpowers/sdd/` was already modified before this fix round started; it was left unstaged and is not part of this commit.
- The backup directory is created under `os.tmpdir()`. If the process is killed mid-run, a single small temp directory may be left behind, but it is uniquely named and outside the project tree.
- In non-`--force` mode, an existing `map-blueprint.json` in a directory that lacks `map.json` could be overwritten by a provided `--blueprint`. This is an edge case; the primary use case (new map directory or `--force`) is protected by the temp backups.
