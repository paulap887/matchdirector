---
description: Add a new personalised viewer mode to the broadcast UI.
---
Add a viewer mode called `${input:mode}` for ${input:audience}.

1. `src/web/src/types.ts`: extend the `Mode` union.
2. `src/web/src/prefs.ts`: accept the mode from the URL (`?mode=...`).
3. `src/web/src/view.ts`: decide which overlay line it reads (`lineFor`) and which overlays it shows (`visible`).
4. `src/web/src/App.tsx`: add the tab to `MODES` and any controls the mode needs; add its `mode.<name>` label and any new text to `src/web/src/i18n.ts` in every language.
5. Components: change layout only through the `View` passed in; do not compute statistics in the browser.
6. If the mode needs a new narration line, add it to the narrator's JSON reply, the template fallback and the verifier's checked lines in `src/MatchDirector.Agents/Narration`.

Keep it usable at 390 px wide, then run `npm run lint` and `npm run build`.
