# D's browser checks

Two separate Playwright projects keep independent evidence separate from application integration.

- **infrastructure** publishes a test-only Blazor app with real D strategy/codec/storage/DI and hand-built fixture transitions/rules doubles. It checks actual WebAssembly, trimmed serialization, real JS interop, overwrite/reload/exact restore, quota failure, denied storage, missing/corrupt/future saves. It does not establish gameplay or automatic controller turns.
- **application** checks a human move, automatic computer turns, and save/reload/resume through the real published application. This is a required integration gate, not a skipped test or a replacement fake. It will fail against the current starter until A/B/C land their implementations and C adds the DOM hooks below. No production arbitrary-state import is required.

## Independent infrastructure run

Prerequisites: .NET SDK pinned in root `global.json`, Node 22+ with npm, Python 3, and Playwright Chromium system dependencies. From the repository root:

```sh
dotnet restore TerritoryCapture.sln --locked-mode
dotnet restore tests/TerritoryCapture.BrowserHarness --locked-mode
dotnet publish tests/TerritoryCapture.BrowserHarness -c Release --no-restore
cd tests/browser
npm ci
npx playwright install chromium
# On a Linux machine missing browser libraries, an administrator can run:
# npx playwright install-deps chromium
npm test
```

Playwright serves only the **published** harness over HTTP at a fixed local origin. It starts/stops the server and creates fresh browser contexts for each test. BrowserHarness lives outside the production solution and has no production references pointing to it. Test doubles cannot be registered by release game startup. Do not deploy BrowserHarness as the game.

Optional browser/cache locations can be set with `PLAYWRIGHT_BROWSERS_PATH` and `npm_config_cache`; use the same browser path for install and test. If the Playwright browser CDN is unavailable, set `CHROMIUM_EXECUTABLE_PATH` to an installed Chromium binary. This run used `/usr/bin/chromium` (Debian Chromium 154.0.8037.57) after the official CDN returned HTTP 403. Failure traces are saved under `test-results`. The fixture-move button is just a hand-written known successor, not an implementation of Rules or Application.

## Application integration run

Publish and serve the real game (first terminal, repository root):

```sh
dotnet publish src/TerritoryCapture.Web -c Release --no-restore
python3 -m http.server 5188 --bind 127.0.0.1 --directory src/TerritoryCapture.Web/bin/Release/net10.0/publish/wwwroot
```

Then in `tests/browser` run `npm run test:app`. Set `APP_BASE_URL` if serving a different origin. This does not start a development server or silently substitute the harness. Use `npx playwright test --list --project=application` to inspect the three tests without claiming they passed. The application project does not start or require BrowserHarness.

### Handoff to C: stable read-only DOM hooks

These proposed hooks make the application checks independent of localization and SVG layout. They must be connected to the **real** facade view/commands; review them when C's UI arrives. Add `data-testid` attributes:

| Hook | Contract |
| --- | --- |
| `player-count` | Select with values `2`, `3`, `4`. |
| `player-kind-0` through `player-kind-3` | Select for each configured player, values `Human`, `Computer`. |
| `start-game`, `save-game`, `resume-game` | Buttons invoking the respective facade commands. |
| `choose-color` | Each color button; native `disabled` when illegal/busy/computer turn. |
| `board` | Board container with numeric `data-current-player-id` and `data-turn-number`. |
| `cell` | Each of 120 hexes with numeric `data-cell-id`, numeric `data-color` (palette enum), and `data-owner-id` (empty for neutral). |
| `player-score` | Each player's score with numeric `data-player-id` and `data-score`. |
| `operation-status` | Save feedback; `data-result="success"` only after a successful store result. |

No snapshot injection, arbitrary-state imports or hidden test-only commands are added to production. Before final acceptance also record actual stable Chrome/Edge checks as required by the plan. Chromium harness results do not establish those checks.
