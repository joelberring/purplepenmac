Purple Pen – första öppningen på macOS
======================================

Detta är en testversion. Den innehåller .NET och behöver därför inte någon
separat .NET-installation.

1. Packa upp zip-filen.
2. Dra PurplePen.app till mappen Program/Applications.
3. Försök öppna PurplePen.app.

MacOS kan visa att appen är “damaged” eller inte kan verifieras. Det beror på
att denna testversion ännu inte är signerad och notariserad av Apple.

Så öppnar du den ändå
---------------------

1. Öppna Terminal (Program > Verktygsprogram > Terminal).
2. Klistra in detta kommando och tryck Retur:

   xattr -dr com.apple.quarantine "/Applications/PurplePen.app"

3. Öppna PurplePen.app igen i Finder.

Om appen ligger någon annanstans än Program, ändra sökvägen i kommandot. Ett
enkelt sätt är att skriva först:

   xattr -dr com.apple.quarantine 

och sedan dra PurplePen.app från Finder till Terminal-fönstret. Terminal
fyller då i rätt sökväg automatiskt. Tryck sedan Retur.

Vi arbetar på en Apple-signerad version. Då behövs inte denna procedur.

---

Purple Pen – first launch on macOS
==================================

This is a test build. It is self-contained and does not require a separate
.NET installation.

1. Unzip the download.
2. Move PurplePen.app to Applications.
3. Try to open PurplePen.app.

macOS may say that the app is “damaged” or cannot be verified. This test build
is not yet signed and notarized by Apple.

To open it anyway, open Terminal and run:

   xattr -dr com.apple.quarantine "/Applications/PurplePen.app"

Then open PurplePen.app again. If it is stored elsewhere, type the command up
to the final space, drag the app from Finder into Terminal, and press Return.
