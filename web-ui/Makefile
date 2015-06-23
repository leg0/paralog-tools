TYPINGS = jquery.d.ts knockout.d.ts sammyjs.d.ts

vpath %.d.ts typings/jquery
vpath %.d.ts typings/knockout
vpath %.d.ts typings/sammyjs

TSC_FLAGS += --noEmitOnError

MODEL_JS = model.js
INDEX_HTML = index.html
LOGBOOK_CSS = logbook2.css

ifeq ($(DEBUG),yes)
	TSC_FLAGS += --sourceMap
else
	TSC_FLAGS += --removeComments
	MODEL_JS += model.min.js.gz
	INDEX_HTML += index.html.gz
	LOGBOOK_CSS += logbook2.css.gz
endif

JS_MINIFY := minify

.PHONY: all
all: static-package.tar.bz2 web-ui

model.js: $(TYPINGS) Scripts/typings model.ts
	tsc model.ts

%.d.ts:
	tsd install $*

Scripts:
	mkdir Scripts

Scripts/typings: Scripts
	if [ ! -e Scripts/typings ]; then ln -s $(PWD)/typings Scripts/typings; fi

%.min.js.gz: %.js
	$(JS_MINIFY) $<
	gzip --keep $*.min.js
	
%.html.gz: %.html
	gzip --keep $<

%.css.gz: %.css
	gzip --keep $<

web-ui: web-ui.go
	go build web-ui.go

static-package.tar.bz2: $(MODEL_JS) $(INDEX_HTML) $(LOGBOOK_CSS) img/busy.gif
	tar jcf static-package.tar.bz2 $(INDEX_HTML) $(MODEL_JS) img/busy.gif $(LOGBOOK_CSS)

.PHONY: clean
clean:
	rm -rf web-ui model.js model.min.js *.map *.gz Scripts typings package.tar.bz2
