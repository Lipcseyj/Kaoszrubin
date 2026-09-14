# Roderic rework – questfeltételes ajtók

Elkészült: 2026-09-14.

## Működés

A MazeDoor külön tárolja a hagyományos DoorState állapotot, a RequiredQuest
futáskulcsot és a tartós QuestAccessGranted hozzáférést. Az IsQuestSealed jelzi
a még fel nem oldott questzárat.

A host QuestDoorAccessService szolgáltatása a nyitási kísérlet pillanatában
olvassa a QuestManager állapotát. Active, ReadyToTurnIn vagy Completed futás
enged hozzáférést; hiányzó, Locked, Available vagy Failed futás nem.
A már teljesített küldetés sem teszi elérhetetlenné a hozzá tartozó szobát.
PerNpcInstance esetben csak a pontos futáskulcs fogadható el.

A hozzáférés első megszerzése végleges. Későbbi feladás, leadás vagy az ajtó
visszazárása nem zárja vissza a questfeltételt, így a játékos nem rekedhet bent.
A questellenőrzés nem aktivál, nem növeli a progresst és nem jutalmaz.

A DoorInteractionController a kulcsfogyasztás és minden tolvaj-/erőpróba előtt
ellenőriz. Elutasításkor nincs kulcs-, étel- vagy vízfogyasztás és nincs dobás.
A MazeDoor közvetlen Open/Smashed állapotváltást sem enged, amíg questzár védi.
A hagyományos kulcsos zárás nem módosíthatja a fel nem oldott questajtót.
A fejlesztői falrombolás továbbra is külön debug-funkció.

## Konfiguráció és első bekötés

A MazeLevelConfiguration.QuestDoorRequirements a szoba ContentId-jához rendel
globális QuestId-t. Generált questajtó kizárólag SideBranch szabályú szobára
konfigurálható, így egy bejárat mögött van és nem vágja el a pálya többi részét.
A betöltő a quest létezését és globális scope-ját is ellenőrzi.

Az 5. pályán RODERIC_INSIGNIA a RodericFallenComradesInsignia küldetéshez kötött.
A szoba ajtaja Closed állapotban, fel nem oldott questzárral keletkezik.
A questfeltétel teljesülése után normál nyitás enged be, külön kulcs nélkül.
Roderic párbeszédei és küldetései ebben a lépésben nem változtak.

## Mentés és coop

- Mentésverzió: 23. A DoorSaveData.QuestGate stabil külső quest-ID-t és
  példányazonosítót, valamint a hozzáférés megszerzését őrzi.
- Régi mentések ajtói feltétel nélküliek maradnak; a már játszott pályára
  nem kerül utólag új akadály. A felfüggesztett kampány verziója is migrálódik.
- Nyitott/bezúzott, de fel nem oldott questajtó hibás mentési állapot.
- Coop-protokoll: 83. A WorldDoorSnapshot.IsQuestSealed a host által küldött
  megjelenítési adat; a kliens nem dönt a hozzáférésről.
- A meglévő delta-összehasonlítás a questzár változását is továbbítja.
  A host és a vendég fel nem oldott questajtónál kihagyja a kulcsválasztást.

## Ellenőrzés

Négy célzott teszt: állapotok és pontos NPC-példány; tiltott nyitás költség nélkül;
mentési és full/delta JSON-körút, hibás állapot elutasítása és régi verzió migrációja;
generált jelvényes szoba és érvénytelen szobakonfiguráció.
A többseedes szobaelhelyezési és meglévő coop-tesztek is sikeresek.

Teljes build: 0 warning, 0 hiba. Tesztkészlet: 288 PASS, 3 FAIL.
A három ismert durability-hibát a felhasználó kérésére nem javítottuk:
sav/káosz kopás, kaszttehetségek kopásmódosítása, sérült felszerelés harci módosítói.
Interaktív UI- és külön gépes coop-végigjátszás nem történt.

Következő előfeltétel: CSV-ből konfigurált, azonosítható questládák és konkrét
ládához kötött questobjective.
