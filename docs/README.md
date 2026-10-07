# Követelményspecifikáció

A magyar dokumentum forrása: [specifikacio.tex](specifikacio.tex).
A követelmények egyenkénti indoklása: [kovetelmenyek-indoklasa.md](kovetelmenyek-indoklasa.md).
A külön kezelt belső munkaterv: [munkaterv.md](munkaterv.md).
A verziókövetés alapja a LaTeX-forrás; a generált PDF és a fordítás
segédfájljai a Git által figyelmen kívül hagyott `build/` könyvtárba kerülnek.

A specifikáció a `task.md` feladatát és a csapat által választott Blazor
WebAssembly platformot követi. Az indoklás és a **150 óra munka + 20 óra
tartalék** munkaterv önálló fájl, nem része a specifikáció PDF-jének.
A dokumentum a követelmények mellett csak a fő műszaki döntéseket rögzíti;
a részletes rendszerterv a megvalósítás során külön készül el.

## Fordítás

A repó gyökeréből, TeX Live/MiKTeX és `latexmk` telepítése után:

```sh
make -C docs
```

LaTeX-csomagok: magyar Babel nyelvi támogatás, Latin Modern, geometry,
microtype, array/booktabs/longtable/tabularx, enumitem, amsmath/amssymb,
TikZ, xcolor, iftex és hyperref. A dokumentum UTF-8 kódolású.
A Tectonic/XeLaTeX/LuaLaTeX Unicode-betűkezelést és fontspecet használ;
pdfLaTeX esetén T1 kódolás és a magyar Babel nyelvi támogatás szükséges.

Ha csak `pdflatex` áll rendelkezésre, a hivatkozások miatt kétszer kell fordítani:

```sh
mkdir -p docs/build
cd docs
pdflatex -interaction=nonstopmode -halt-on-error -output-directory=build specifikacio.tex
pdflatex -interaction=nonstopmode -halt-on-error -output-directory=build specifikacio.tex
```

Alternatívaként a Tectonic automatikusan kezeli a szükséges újrafordításokat:

```sh
mkdir -p docs/build
tectonic --keep-logs --outdir docs/build docs/specifikacio.tex
```

A Tectonic első futása internetkapcsolatot igényelhet a TeX-csomagokhoz.
Az eredmény mindhárom esetben `docs/build/specifikacio.pdf`.

## Beadás előtti kitöltés és egyeztetés

- A `.tex` elején töltsétek ki a csapat nevét, a négy nevet és Neptun-kódot,
  valamint a konzulens nevét tartalmazó makrókat.
- Egyeztessétek a konzulenssel az alapváltozat terjedelmét, a helyi
  többjátékos módot, az induló azonos színű komponensek automatikus
  elfoglalását és a semleges mezők elfogyására épülő befejezést.
- Egyeztetés után frissítsétek a dokumentum verzióját, dátumát és státuszát.
  A külön munkatervben szereplő A–D munkamegosztás becslés; a konkrét
  csapat feladatvállalása szerint módosítható a teljes keret megtartásával.

A 2026-os [hallgatói útmutató](https://www.aut.bme.hu/Upload/Course/VIAUMA06/publikus_anyagok/HaziFeladatHallgatoiUtmutato_2026.pdf)
szerint a konzulenssel egyeztetett specifikáció beadási határideje
**2026. október 12., éjfél**; a végleges feladaté **2026. november 30., éjfél**.
A kész alkalmazást a végleges feltöltés előtt be is kell mutatni.
A [tantárgyi oldal](https://www.aut.bme.hu/Course/VIAUMA06) és az útmutató
egyaránt ezeket a házi feladatos dátumokat tartalmazza.

Az útmutatóban az adatbázis és az autentikáció általános értékelési példaként
szerepel. A specifikáció a helyi, memóriában futó játékhoz nem vállal ilyen
funkciókat; a bemenetellenőrzést, biztonságos szövegmegjelenítést és a webes
kiszolgálás feltételeit a feladat tényleges működéséhez igazítja.
