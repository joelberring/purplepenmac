Purple Pen – första öppningen på macOS
======================================

Programmet innehåller .NET och behöver därför inte någon separat
.NET-installation.

1. Packa upp zip-filen.
2. Dra PurplePen.app till mappen Program/Applications.
3. Försök öppna PurplePen.app.

En signerad och notariserad utgåva ska öppnas normalt. En osignerad testutgåva
kan däremot få meddelandet att appen inte kan verifieras eller är “damaged”.

Om macOS blockerar en osignerad testutgåva
------------------------------------------

1. Öppna Terminal (Program > Verktygsprogram > Terminal).
2. Klistra in detta kommando och tryck Retur:

   xattr -dr com.apple.quarantine "/Applications/PurplePen.app"

3. Öppna PurplePen.app igen i Finder.

Om appen ligger någon annanstans än Program, ändra sökvägen i kommandot. Ett
enkelt sätt är att skriva först:

   xattr -dr com.apple.quarantine 

och sedan dra PurplePen.app från Finder till Terminal-fönstret. Terminal
fyller då i rätt sökväg automatiskt. Tryck sedan Retur.

Kör inte kommandot om appen redan öppnas normalt.

---

Purple Pen – first launch on macOS
==================================

The app is self-contained and does not require a separate .NET installation.

1. Unzip the download.
2. Move PurplePen.app to Applications.
3. Try to open PurplePen.app.

A signed and notarized release should open normally. An unsigned test build
may instead be reported as unverifiable or “damaged”.

Only if macOS blocks an unsigned test build, open Terminal and run:

   xattr -dr com.apple.quarantine "/Applications/PurplePen.app"

Then open PurplePen.app again. If it is stored elsewhere, type the command up
to the final space, drag the app from Finder into Terminal, and press Return.
Do not run the command when the app already opens normally.
