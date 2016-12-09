
LIB=BoxEditor/bin/Debug/BoxEditor.dll
LIBTS=BoxEditor/bin/Debug/BoxEditor.ts
LIBJS=BoxEditor/bin/Debug/BoxEditor.js

NGRAPHICSLIB=BoxEditor/bin/Debug/NGraphics.dll
NETJS=/Users/fak/Dropbox/Projects/Netjs/Netjs/bin/Debug/Netjs.exe

all: $(LIB) $(LIBJS)

$(LIB): BoxEditor
	xbuild

$(LIBTS): $(LIB)
	mono $(NETJS) $(LIB) $(NGRAPHICSLIB)

$(LIBJS): $(LIB)
	tsc -t ES5 BoxEditor/bin/Debug/mscorlib.ts $(LIBTS) --out $(LIBJS)
