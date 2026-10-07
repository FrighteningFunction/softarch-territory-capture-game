.PHONY: all detailed clean

all:
	latexmk -pdf -interaction=nonstopmode -halt-on-error -outdir=build specification.tex

detailed:
	latexmk -pdf -interaction=nonstopmode -halt-on-error -outdir=build specification-detailed.tex

clean:
	latexmk -C -outdir=build specification.tex
