# Territory capture game

BME Software Architectures group project. [Specification](docs/specification.tex).

[Architecture and four-person work plan](docs/work-plan.md): component ownership,
shared interfaces, independent tests and integration gates.

## Build

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
