# Hrubé zadání diplomové práce (verze 2, 25. 9. 2026)

> Změny proti verzi 1 (`Downloads/zadani-diplomove-prace.txt`):
> jádro práce je nově **srovnání metod doplňování chybějících dat** a **detekce vadných
> počítacích zařízení**; exporty Transportelly opraveny podle ověřeného vzorku (neobsahují
> obsazenost, km ani kWh); nasazení je jedna instance na dopravce v jeho síti; databáze je
> volitelně PostgreSQL nebo SQL Server; frontend React.

## Pracovní název
**Webová platforma pro automatizované zpracování a vizualizaci provozních dat MHD**

*(podtitul: Automatizované zpracování APC dat, doplňování chybějících měření a detekce
vadných zařízení s webovou mapovou a grafickou interpretací)*

---

## Anotace / Cíl práce

Cílem je navrhnout a implementovat **webovou aplikaci s backendem**, která:

1. **Převezme funkce desktopové aplikace ADA** (analýza APC dat – obsazenost, kvalita
   počítání, statistiky linek/zastávek/spojů) a **zautomatizuje jejich datovou část**:
   import, parsování, validaci, doplnění chybějících dat a výpočet ukazatelů provede
   backend bez ruční obsluhy.
2. **Zpracuje hotové výstupy z Transportelly**: exporty statistik jízdních řádů (XLSX:
   plánované a skutečné časy, zpoždění, doba pobytu v zastávce) backend načte,
   normalizuje a sjednotí s vlastním datovým modelem.
3. **Porovná metody doplňování chybějících měření** (APC dat) a vyhodnotí jejich přesnost
   na reálných datech dopravce.
4. **Automaticky rozpozná vadná nebo nespolehlivá počítací zařízení** z charakteru jejich
   dat a z chybových hlášení.
5. **Interpretuje výsledná data ve webovém frontendu**: mapa, grafy a maticové přehledy
   s prahováním.

Nástroj **nemá nahrazovat Power BI**, ale pracovat na podobném principu
(data → ukazatel → vizualizace), specializovaně pro doménu MHD.

### Výzkumné otázky
- **VO1:** Která metoda doplňování chybějících APC dat dosahuje nejmenší chyby (MAE/RMSE)
  oproti skutečně naměřeným hodnotám, ve srovnání se stávající heuristikou ADY
  (průměr jízd ve stejnou denní dobu)?
- **VO2:** Lze z dat samotných (bilance nástupů a výstupů, záporná obsazenost, nulové či
  „zaseknuté“ čítače) spolehlivě rozpoznat vadné zařízení, a s jakou přesností vůči
  chybovým hlášením zařízení?

---

## Vymezení vůči stávajícímu stavu

| | ADA (dnes) | Cílový nástroj |
|---|---|---|
| Platforma | desktop WPF, jeden počítač | webová aplikace + backend |
| Zpracování dat | ruční import/parsování/oprava | **automatizovaný pipeline** na backendu |
| Chybějící data | jedna heuristika, známé mezery | **porovnané a vyhodnocené metody** |
| Kvalita zařízení | ruční posouzení | **automatická detekce vadných zařízení** |
| Datová sada | jen APC | APC **+ výstupy statistik JŘ z Transportelly** |
| Přístup | jeden uživatel | víceuživatelský přes web, v síti dopravce |

---

## Teoretická část

1. **Datové zdroje a jejich modely**
   - Surová APC data (nástupy/výstupy, obsazenost, validita zařízení) – datový model ADY.
   - Exporty statistik JŘ z Transportelly – struktura, strojové čtení, ověřený rozsah dat.
   - Jízdní řád jako referenční rámec.
2. **Doplňování chybějících dat**
   - Přehled metod: průměr podle denní doby (stávající ADA), interpolace mezi zastávkami,
     historický profil spoje, metoda nejbližších sousedů, model strojového učení.
   - Metodika vyhodnocení: maskování skutečně naměřených jízd, metriky MAE/RMSE.
3. **Kvalita měření a detekce vadných zařízení**
   - Typické vady APC (drift čítače, záporná obsazenost, nevyrovnaná bilance, výpadky).
   - Pravidlové a statistické detekční přístupy; ukazatel „zdraví“ zařízení v čase.
4. **Definice ukazatelů (KPI)**: dochvilnost/odchylka od JŘ, obsazenost a vytíženost,
   přepravní výkon, kvalita měření, frekvence a pokrytí zastávek.
5. **Principy zpracování a vizualizace**: automatizovaný datový pipeline (ETL),
   kartografická vizualizace, časové řady, maticové přehledy s prahováním; vymezení vůči
   Power BI.

## Praktická část

1. **Backend – automatizované zpracování dat**: příjem APC dat → parsování → validace →
   doplnění → výpočet ukazatelů; import a normalizace exportů Transportelly; sjednocený
   datový model a API.
2. **Modul kvality dat**: detekce vadných zařízení a jejich zobrazení v čase.
3. **Experiment doplňování dat**: implementace a srovnání metod na reálných datech.
4. **Frontend – webová vizualizace**: mapa linek a zastávek s hodnotami ukazatelů, grafy,
   matice zastávka × spoj s barevným prahováním, export výstupů.
5. **Případové studie** nad daty dopravce: problematické úseky, špičky obsazenosti,
   systematická zpoždění, kvalita APC zařízení.
6. **Vyhodnocení**: odpovědi na VO1 a VO2, porovnání s Power BI, přínos automatizace.

## Předpokládaná osnova
1. Úvod, motivace, vymezení vůči Power BI a vůči desktopové ADĚ
2. Analýza dat: surová APC data + výstupy statistik JŘ Transportelly
3. Metody doplňování dat, detekce vadných zařízení a definice ukazatelů
4. Návrh architektury webové platformy
5. Implementace automatizovaného zpracování dat a modulu kvality
6. Experiment: srovnání metod doplňování dat
7. Implementace webové vizualizace
8. Případové studie, vyhodnocení a závěr

## Předpokládané technologie
- **Backend:** .NET 10 (ASP.NET Core), EF Core; databáze **PostgreSQL nebo SQL Server**
  podle infrastruktury dopravce; plánované úlohy (Hangfire); zpracování XLSX (ClosedXML/NPOI).
- **Frontend:** React + TypeScript (Vite), Leaflet (mapa, WMS vrstvy), ECharts (grafy).
- **Nasazení:** jedna instance na dopravce v jeho síti (Docker), víceuživatelský přístup.

## Výstupy práce
Funkční webová platforma s automatizovaným zpracováním dat; vyhodnocené srovnání metod
doplňování chybějících dat; modul detekce vadných počítacích zařízení; parser výstupů
Transportelly; katalog ukazatelů s metodikou výpočtu; případové studie nad reálnými daty.

## Klíčová slova
MHD, APC, jízdní řád, doplňování chybějících dat, kvalita dat, detekce poruch, webová
aplikace, ETL, provozní ukazatele, vizualizace dat.

## Otevřené body pro vedoucího
- Doporučená literatura (povinné pole zadání) – potřeba doplnit, včetně české.
- Potvrdit zdroj obsazenosti/km/kWh: ověřený export Transportelly je neobsahuje.
- ~~Pravidla fakulty pro použití AI nástrojů a jejich uvedení v práci.~~ Vyřešeno: FI MU,
  „Pravidla a doporučení pro vypracování BP a DP na FI MU“ (17. 3. 2025), oddíl 1.2.3 — AI je
  povoleno; uvést nástroje a účel, doslovně převzaté části a převzetí odpovědnosti, na straně
  s prohlášením o autorství (viz `latex/prace.tex`).
- Data pro práci: pravděpodobně MDML (IRMA MATRIX) místo DPMB; potvrdit, až budou logy.
- Transportella: vyžádat export z nové Transportelly u MDML (DPMB-éra export neobsahuje
  obsazenost, km ani kWh).
