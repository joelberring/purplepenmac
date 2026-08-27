# Purple Pen Next — lokal återstart

**Läge: 27 augusti 2026.** Detta är underlag för Codex lokalt på en Apple Silicon-Mac. Projektet ska byggas i ett eget git-repo och committas/pushas efter varje färdigt byggblock. Work-scratch ska inte användas som huvudkopia.

## Uppdrag

Utveckla ett modernt, öppet och i första hand macOS-anpassat banläggningsprogram för orientering, baserat på Purple Pens pågående Avalonia-port. Det ska också fungera på Windows och Linux. Grundidén är **inte** att skriva om Purple Pen eller kartmotorn, utan att färdigställa och förbättra den befintliga porten.

Huvudproblemet som ska lösas är trycksäkra ban-PDF:er, även när ingående OCAD-/OMAP-kartor har bristfälliga färginställningar. Appen ska därför kunna applicera en versionsmärkt tryckeriprofil vid export, utan att ändra originalkartan.

## Fasta tekniska beslut

- Bas: Purple Pens aktiva Avalonia-port i `src/AvPurplePen`.
- Behåll Purple Pens befintliga banlogik, variationer, kontrollbeskrivningar, IOF XML, OCAD/OMAP-läsning och MapModel.
- UI/rendering: Avalonia + Skia, inte en webbapp.
- Plattformar: macOS 13+ på Apple Silicon först; Windows/Linux behålls fungerande.
- Licens: behåll Purple Pens BSD 3-Clause-licens och alla notices. GPL-3.0-or-later kan endast användas för tydligt avskilda moduler om det blir nödvändigt. Byt inte namn till “Purple Pen” på ett sätt som antyder officiellt samband.
- Undvik ny kartmotor och undvik att bygga på OpenOrienteering Mapper; det är en reserverad fallback, inte den valda vägen.

## Sann och viktig status

Det fanns tidigare en lokal gren `purple-pen-next` med färgpreflight, tryckprofilsmodell och testresultat upp till 86/86. Scratch-städning tog bort arbetskopian och dessa ändringar var **inte pushade**. De ska behandlas som en specifikation, inte som kod som finns kvar.

Återskapad kod hade därefter inbyggda utkast till BL skog/sprint-profiler, men byggmiljön var inte återskapad och detta var inte verifierat. Börja därför med ett rent repo och implementera om i små, commitade steg.

Verifierat i förstudien:

- Avalonia-appen byggde på Linux.
- `.ocd` och `.omap` kunde läsas och enkla CMYK-PDF-prover skapades.
- PDF-resultatet var vektorbaserat i prototypen.

Inte verifierat/klart:

- PDF/X-4.
- ICC-profil som OutputIntent.
- äkta PDF-overprint (inte endast visuell blend mode).
- spotfärgs-/separationsvalidering.
- reproducerbart Apple-Silicon-bygge, kodsignering eller notarisering.

## Produktkrav: färg och export

### Icke-förstörande tryckeriprofil

En profil ska väljas per export/projekt och ligga ovanpå kartans färgtabell. Den ska kunna spara klubbens standard samt komma ihåg senaste val per projekt. Originalets `.ocd`/`.omap` får aldrig ändras vid vanlig export.

Profilen ska minst innehålla:

- id, namn, versionsdatum och källa;
- karttyp (skog/sprint); 
- regler för färgidentifiering: namn, OCAD-id och vid behov användarbekräftelse;
- effektiva CMYK-värden och process/spot-typ;
- relativ ritordning;
- overprint/knockout-avsikt;
- skrivare, papper och valfri ICC-profil;
- kravflaggor för PDF/X OutputIntent, inbäddad ICC och äkta overprint.

Appen ska före export visa källa kontra effektiva värden och tydligt rapportera avvikelser. Oklara matchningar ska kräva bekräftelse i stället för att gissas. Lägg senare till “Skriv till kopia av kartfil” som separat, uttryckligt kommando.

### BL-profiler

Bygg in minst två **versionsmärkta** profiler:

1. `BL skog`.
2. `BL sprint`.

Värden och färgordning ska hämtas från BL Idrottsservice aktuella tabeller vid implementation och URL/datum ska lagras som källa. Skriv inga påhittade CMYK-tal. Skogsprofilen ska särskilt kunna kontrollera BL:s krav att transparent banviolett ligger under brun, svart och 100 % blå där den regeln är tillämplig. BL kan ändra sina tabeller, så en ny inbyggd profil ska bli en ny version, aldrig en tyst ändring av gammal export.

Källor:

- https://www.bl-idrottsservice.se/farginstallningar-kartnormen/
- https://www.bl-idrottsservice.se/farginstallning-sprintnormen/
- https://www.bl-idrottsservice.se/baninritning-i-ocad/

### Produktionsmål

Slutmålet är vektorbaserad PDF/X-4 med:

- CMYK bevarat utan färd via skärm-RGB;
- ICC OutputIntent;
- korrekt banpåtryck som egen lager-/spotfärgslogik när formatet kräver det;
- äkta PDF-overprint och kontrollerbara separationer;
- inbäddade typsnitt och exakt skala/utskrift.

Fram till dess måste UI vara strikt ärligt: profil som kräver en ouppfylld funktion ska stoppa produktions-export eller kräva ett medvetet, loggat undantag. Kalla aldrig en vanlig CMYK-PDF för PDF/X eller tryckerisäker.

## Arbetsordning

### 0. Gör projektet permanent först

1. Skapa privat GitHub-repo/fork, exempelvis `purple-pen-next`.
2. Lägg till Purple Pen som `upstream` remote och gör en egen arbetsgren.
3. Skriv `README` med uppströmscommit, licens, mål och hur projektet byggs.
4. Lägg GitHub Actions för minst build och test på macOS + Windows/Linux om möjligt.
5. Gör första pushen innan funktionsarbete startar.

Använd små commits. Efter varje verifierat block: `git status`, test, commit med tydlig text och `git push`. Kör aldrig lång vidareutveckling med osparade lokala ändringar.

### 1. Reproducerbart Apple-Silicon-bygge

Sätt upp .NET SDK enligt upstreams krav, restore/build/testa den relevanta Avalonia-lösningen och dokumentera exakta kommandon. Starta appen på M4. Fixa endast portabilitetsfel som krävs för bygget: paths, resurser, fonts, shortcuts och Retina. Ingen signering/notarisering ännu.

**Acceptans:** ren klon går att bygga/testa/starta på Mac och instruktionen fungerar från tom miljö.

### 2. Färgdatamodell och preflight

Återimplementera, med tester:

- `ColorPreflightReport` som läser öppnad kartas färgtabell;
- visning av namn, OCAD-id, källa/effectiv CMYK och overprint/knockout;
- kapabilitetsmodell för den faktiska PDF-motorn;
- JSON-format för versionshanterade tryckprofiler;
- meny/kommando ungefär `Arkiv → Skapa PDF:er → Färgkontroll före PDF…`;
- svenska och engelska resurser.

Gör ändringarna i separata commits och lägg riktiga enhetstester. Ignorera inte fel genom att skippa testprojekt; om upstream har t.ex. `AllRules.ruset`-varning, dokumentera den separat.

### 3. Profiler och exportdialog

Lägg profilval i PDF-exportdialogen. CMYK ska vara standard för ban-PDF, RGB ska finnas kvar för skärmbruk. Koppla preflight till valt färgläge och vald profil. Inbyggda BL-profiler och lokala klubbprofiler ska vara importerbara/exporterbara JSON-filer.

Implementera valideringsresultat med tre nivåer: information, varning och blockerande produktionskrav. En profil som kräver PDF/X/ICC/overprint får inte passera som godkänd om motorn saknar detta.

### 4. PDF-arkitekturspike (Sol-nivå)

Innan större implementation: undersök nuvarande PDF-kedja, välj minsta hållbara väg till PDF/X-4, ICC OutputIntent och övertryck. Prototypa på en eller två verkliga OCAD/OMAP-kartor. Kontrollera producerad PDF med lämpliga preflight-/separationsverktyg, inte enbart genom att den ser rätt ut på skärm.

Dokumentera formatval, beroenden/licenser, objektmodell för spot/overprint och teststrategi i `docs/pdf-color-architecture.md`. Ta ett GO/NO-GO-beslut innan motorn byggs om.

### 5. Produktions-PDF och verkliga prov

Implementera enligt beslutad arkitektur. Lägg regressionstester för exemplar av `.ocd` och `.omap`, och manuella acceptanstester hos BL/klubbens verkliga skrivare och papper. Först efter detta: paketering, kodsignering och notarisering.

## Modellfördelning

- **Terra High:** normalarbete — repo/CI, Avalonia-vyer, resurser, JSON, tester, UI-koppling och mekanisk implementation.
- **Sol High:** PDF/X, ICC, spot-/overprint-semantik, formatgränsfall, svåra renderings- och regressionsproblem samt arkitekturgranskning.

Codex ska arbeta en etapp åt gången, men kan genomföra flera små block om varje block byggs, testas, committas och pushas innan nästa börjar.

## Startuppdrag till lokal Codex

Kopiera detta i en ny Codex-session efter att repot klonats:

> Läs `CODEX_LOCAL_HANDOFF.md` i sin helhet. Vi bygger vidare på Purple Pens Avalonia-port i detta repo enligt dokumentets fasta beslut. Börja med etapp 0 och 1: kontrollera repo/upstream/licens, få ett reproducerbart Apple-Silicon-bygge, starta appen och skriv exakt buildinstruktion i README. Gör inga PDF-/färgfunktionsändringar förrän den basen är verifierad. Arbeta i små commitade block; efter varje block kör relevanta tester, gör en tydlig commit och pusha till origin. Rapportera konkret vad som passerade och vad som blockerar. Använd Terra High som standard; ta bara Sol för PDF- och färgarkitektur eller svåra fel.

