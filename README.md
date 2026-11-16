# Territory capture game

BME Software Architectures group project. [Specification](docs/specification.tex).

[Architecture and four-person work plan](docs/work-plan.md): component ownership,
shared interfaces, independent tests and integration gates.

## Start development

Install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), matching `global.json` exactly (10.0.402). SDK roll-forward is disabled. `Directory.Build.targets` also pins the Blazor runtime and WebAssembly/ILLink/internal-assets tool packages to 10.0.13; SDK/workload defaults alone do not guarantee consistent locked restore. For a toolchain upgrade, update these pins together, regenerate both the solution and BrowserHarness lockfiles, and verify locked restore before committing. Run from the repository root:

```sh
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-restore
dotnet run --project src/TerritoryCapture.Web --no-restore
```

Open **http://localhost:5080**. The Debug app shows a labelled development preview: setup, human turn, computer turn and draw. These are predefined screens, not playable game logic. Release builds exclude the demo controller and show the starter page until real services are connected.

## Team handoff

CI also exercises locked restore with simulated newer Blazor pack defaults from `tests/build/newer-wasm-defaults.targets`. This regression check reproduces the hosted-runner `10.0.13` versus `10.0.15` mismatch without changing installed SDKs or updating lockfiles.

The contracts match the work plan; all four implementations use these types.

| Owner | Start here | First implementation task |
| --- | --- | --- |
| A | `src/TerritoryCapture.Rules` and its tests | Implement `IGameRules.Create`, hex adjacency and `ApplyMove` using the shared fixtures. |
| B | `src/TerritoryCapture.Application` and its tests | Implement `IGameController` start, human move and reset using test doubles. |
| C | `src/TerritoryCapture.Web` and its tests | Replace the preview with setup/game components against `IGameController`; register real services as they arrive. |
| D | `src/TerritoryCapture.Infrastructure` and its tests | Strategy, codec, browser storage and independent tests implemented. See the component README for `AddTerritoryCaptureInfrastructure` registration and the browser-test handoff. |

Each person adds their implementation and tests in their own projects, runs tests locally, and opens a PR describing what works and what tests passed. For a single component, for example:

```sh
dotnet test tests/TerritoryCapture.Rules.Tests
```

[Shared test support](tests/TerritoryCapture.TestSupport/README.md) provides hand-built states, expected outcomes, JSON and configurable dependency doubles. Rules and Application remain empty implementation projects; their tests verify starter assets. [Infrastructure](src/TerritoryCapture.Infrastructure/README.md) implements greedy selection and the version-1 JSON codec, tested independently against the shared fixtures and rules doubles. Preview tests verify the fake UI wiring. The real browser save adapter is covered by isolated unit tests and [published browser-harness checks](tests/browser/README.md). Three application smoke tests are prepared for integration after A/B/C deliver their components; they have not passed against the starter UI. [PR checks](.github/workflows/pr-checks.yml) restore the pinned dependencies, build the solution and run all test projects in Release mode on each pull request.

For final build checks:

```sh
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet publish src/TerritoryCapture.Web --configuration Release --no-restore
```

Publish produces static files in `src/TerritoryCapture.Web/bin/Release/net10.0/publish/wwwroot`. Serve that directory over HTTP; opening `index.html` directly is insufficient. For a local check with Python installed:

```sh
python3 -m http.server 5080 --directory src/TerritoryCapture.Web/bin/Release/net10.0/publish/wwwroot
```

Keep [the milestone plan](docs/work-plan.md#7-concrete-delivery-milestones-and-weekly-catchups) and the weekly status checklist as the handoff reference. Target submission-ready on November 23, with the November 30 deadline and one-week buffer.

## Build the specification

Requires Make, latexmk and TeX Live with Hungarian support.

Copy `docs/personal-data.example.tex` to `docs/personal-data.tex` and fill in
member details. The local file is Git-ignored; without it, placeholders are used.
Escape LaTeX special characters, e.g. `\_` in email addresses.

```sh
make -C docs
```

Output: `docs/build/specification.pdf`, including your local details.

## LaTeX Workshop

Use the default latexmk recipe and add this workspace setting:

```json
"latex-workshop.latex.outDir": "%DIR%/build"
```

Reload the window, then press **Ctrl+Alt+B**.
