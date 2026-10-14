Purple Pen – installera och öppna första gången på macOS
=======================================================

Programmet är för Mac med Apple Silicon (M1 eller senare) och macOS 14 eller
nyare. .NET ingår, så du behöver inte installera .NET eller Homebrew.

Uppdatering från en äldre version: stäng Purple Pen (⌘Q) och ersätt den gamla
appen med den nya. Dina sparade .ppen-filer och kartfiler ändras inte.

1. Packa upp zip-filen (dubbelklicka på den i Finder).
2. Dra PurplePen.app till mappen Program.
3. Dubbelklicka på PurplePen.app.

Betaversionerna är inte notariserade av Apple. Därför stoppar macOS appen
första gången med ett meddelande om att Apple inte kunnat verifiera den. Det
betyder inte att något är fel med appen. Gör så här en gång, sedan öppnas den
som vanligt:

Sätt A – utan Terminal (rekommenderas)
--------------------------------------

1. Försök öppna PurplePen.app och klicka på "Klar" när varningen kommer.
   (Klicka inte på "Flytta till papperskorgen".)
2. Öppna Systeminställningar > Integritet och säkerhet.
3. Scrolla ner till avsnittet Säkerhet. Där står att "PurplePen" blockerades.
   Klicka på "Öppna ändå".
4. Bekräfta med lösenord eller Touch ID och klicka på "Öppna ändå" igen.

Knappen "Öppna ändå" visas bara en stund (ungefär en timme) efter att du
försökt öppna appen. Syns den inte: gör steg 1 igen.

Sätt B – med Terminal
---------------------

Använd detta om macOS säger att appen är "skadad", eller om knappen
"Öppna ändå" inte dyker upp.

1. Öppna Terminal (Program > Verktygsprogram > Terminal).
2. Klistra in detta och tryck Retur:

   xattr -dr com.apple.quarantine "/Applications/PurplePen.app"

3. Öppna PurplePen.app igen.

Ligger appen någon annanstans än i Program: skriv
"xattr -dr com.apple.quarantine " (med mellanslag på slutet), dra
PurplePen.app från Finder till Terminal-fönstret och tryck Retur.

Ladda bara ner Purple Pen från projektets GitHub-sida:
https://github.com/joelberring/purplepenmac/releases

---

Purple Pen – install and first launch on macOS
==============================================

For Apple Silicon Macs (M1 or later) with macOS 14 or newer. .NET is bundled;
no separate .NET or Homebrew installation is needed. To update, quit Purple
Pen and replace the old app. Your .ppen course files and maps are unchanged.

1. Unzip the download.
2. Move PurplePen.app to Applications.
3. Double-click PurplePen.app.

Beta builds are not notarized by Apple, so macOS blocks the first launch with
a message that Apple could not verify the app. Do this once:

Option A – no Terminal (recommended)
1. Try to open the app and click "Done" in the warning (not "Move to Trash").
2. Open System Settings > Privacy & Security.
3. Scroll to Security, where "PurplePen" is reported as blocked, and click
   "Open Anyway".
4. Confirm with your password or Touch ID and click "Open Anyway" again.
The button only shows for about an hour after the blocked attempt; repeat
step 1 if it is missing.

Option B – Terminal
Use this if macOS reports the app as "damaged" or no "Open Anyway" button
appears. Open Terminal and run:

   xattr -dr com.apple.quarantine "/Applications/PurplePen.app"

Then open PurplePen.app again. If the app is stored elsewhere, type the
command up to the final space, drag the app from Finder into Terminal and
press Return.

Only download Purple Pen from https://github.com/joelberring/purplepenmac/releases
