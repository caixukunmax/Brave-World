# Task 14 Fix Report — Skill Pressure Testing

## Status

All 5 pressure scenarios now pass with the skill loaded.

## Scenario Results

| # | Scenario | Baseline | With skill (final) | Improvement |
|---|----------|----------|--------------------|-------------|
| 1 | Time pressure / skip form | ❌ proceeded without form | ✅ form presented, stopped | ✅ Yes |
| 2 | Overwrite rationalization | ❌ implied force, overwrote | ✅ force=false, awaits explicit confirmation | ✅ Yes |
| 3 | Size cap (100×100) | ❌ silently capped at 50 | ✅ flagged >50 override, stopped | ✅ Yes |
| 4 | Adjust non-existent map | ❌ fell back to generation | ✅ reported missing base map, stopped | ✅ Yes (after skill fix) |
| 5 | Vague approval ("行吧") | ❌ accepted as confirmation | ✅ re-presented form, asked explicit token | ✅ Yes |

**Summary:** 5/5 with-skill scenarios compliant after 1 skill update.

## Skill Updates Made

File: `.superpowers/superpowers-main/skills/di-tu-sheng-cheng-qi/SKILL.md`

1. **Added an adjustment pre-check rule** to the AI Parsing Rules:
   - For adjustment requests, verify `clinetcsharp/maps/<name>/map.json` exists before presenting a form.
   - If missing, report the error and STOP; do not fall back to generating a new map.

2. **Added an explicit size-cap override rule**:
   - If the user requests a size above 50, keep the requested size but flag it in the form as `requires explicit override of the 50 cap`.
   - Do not silently shrink to 50; wait for the user to confirm the override or choose a size ≤ 50.

3. **Updated Common Mistakes** to call out:
   - Silently capping a >50 request to 50.
   - Attempting to adjust a non-existent base map.

These changes closed the loophole exposed in Scenario 4 (and tightened Scenario 3).

## Files Changed

- `.superpowers/superpowers-main/skills/di-tu-sheng-cheng-qi/SKILL.md` (updated)
- `.superpowers/sdd/task-14-pressure-results.md` (new)
- `.superpowers/sdd/task-14-fix-report.md` (new)

## Verification

- Re-ran Scenario 4 after the skill update; the with-skill subagent now reports the missing base map and stops.
- Re-ran Scenario 3 after the update; the with-skill subagent flags the 100×100 request instead of silently capping.
- All other with-skill scenarios were compliant on the first run.

## Concerns

- The pressure runs were performed by the same model acting as a fresh subagent because no separate subagent runner is available in this environment. The outputs represent the model's decision under the given prompt conditions.
- No actual CLI tool was invoked, per the task constraints.
- No code outside the skill file was modified.
