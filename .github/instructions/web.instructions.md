---
applyTo: "src/web/**"
---
# Web (React + Vite) conventions in MatchDirector

- React 19 with TypeScript and function components. Types for server JSON live in `src/types.ts` and mirror the C# contracts (camelCase).
- Live data comes only from `useMatchFeed` (SignalR `/hubs/match` plus `/api/state`). Overlays with the same `overlayId` are replaced in place.
- Personalisation goes through `view.ts` (`buildView`, `lineFor`, `visible`) so every component reads the same viewer mode; preferences live in `prefs.ts` and can be set by URL (`?mode=club&club=RDM`).
- Never compute football statistics in the browser; only format numbers the server sent.
- Styling: CSS custom properties in `index.css` (`--home`, `--away`, `--accent`, ...), no inline colours. Layouts must work from 390 px wide.
- Keep `npm run lint` (oxlint) and `npm run build` clean.
