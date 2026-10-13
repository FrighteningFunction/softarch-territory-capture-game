# Territory capture game

BME Software Architectures group project. [Specification](docs/specification.tex).

[Architecture and four-person work plan](docs/work-plan.md): component ownership,
shared interfaces, independent tests and integration gates.

## Start development

Install the [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), matching `global.json` (10.0.402, later patches in that feature band accepted). Run from the repository root:

```sh
dotnet restore --locked-mode
dotnet build --no-restore
dotnet test --no-restore
dotnet run --project src/TerritoryCapture.Web --no-restore
```

Open **http://localhost:5080**. The Debug app shows a labelled development preview: setup, human turn, computer turn and draw. These are predefined screens, not playable game logic. Release builds exclude the demo controller and show the starter page until real services are connected.

## Team handoff

The contracts match the work plan; all four implementations use these types.

| Owner | Start here | First implementation task |
| --- | --- | --- |
| A | `src/TerritoryCapture.Rules` and its tests | Implement `IGameRules.Create`, hex adjacency and `ApplyMove` using the shared fixtures. |
| B | `src/TerritoryCapture.Application` and its tests | Implement `IGameController` start, human move and reset using test doubles. |
| C | `src/TerritoryCapture.Web` and its tests | Replace the preview with setup/game components against `IGameController`; register real services as they arrive. |
| D | `src/TerritoryCapture.Infrastructure` and its tests | Implement greedy selection and the JSON codec, then the browser save adapter. |

Each person adds their implementation and tests in their own projects, runs tests locally, and opens a PR describing what works and what tests passed. For a single component, for example:

```sh
dotnet test tests/TerritoryCapture.Rules.Tests
```

[Shared test support](tests/TerritoryCapture.TestSupport/README.md) provides hand-built states, expected outcomes, JSON and configurable dependency doubles. Existing tests verify these starter assets and preview wiring; they do not establish that gameplay has been implemented. Rules, Application and Infrastructure are empty implementation projects ready for their owners. Browser automation and the coordinator's basic test pipeline are not included in this starter.

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
