import type { welcome as welcomeEn } from '../en/welcome'

export const welcome: Record<keyof typeof welcomeEn, string> = {
  'welcome.topBar': 'STL // STRATEGY TRADING LAB',
  'welcome.hero.title': 'Opera segons regles, no per emocions.',
  'welcome.hero.lead':
    'Un laboratori d’estratègies transparent. Tria plantilles provades, executa simulacions en temps real sense risc i rep avisos de senyals sense connexió al broker.',

  'welcome.features.eyebrow': 'CAPACITATS PRINCIPALS',
  'welcome.features.title': 'Com funciona Strategy Trading Lab',
  'welcome.features.lead':
    'Tres pilars disciplinats per ajudar-te a construir, provar i executar estratègies sistemàtiques als mercats moderns.',

  // Pillar 1: Strategy builder
  'welcome.builder.tag': '01 // CONSTRUEIX',
  'welcome.builder.badge': 'PLANTILLES I REGLES',
  'welcome.builder.title': 'Constructor d’estratègies',
  'welcome.builder.tagline': 'Defineix les teves regles abans que obri el mercat',
  'welcome.builder.summary':
    'Tria plantilles matemàtiques provades o seleccions d’IA. Tria les teves accions, defineix indicadors i configura pressupostos per fases.',
  'welcome.builder.point1': 'Plantilles provades amb lògica clara: mitjanes mòbils, baixades a la zona de descompte i remuntades.',
  'welcome.builder.point2': 'Tu controles les llistes d’accions, l’import de cada fase i els límits de pressupost amb valors provats.',
  'welcome.builder.point3': 'Regles en llenguatge planer: sense caixes negres ni suposicions amagades.',
  'welcome.builder.cta': 'Prova de construir una estratègia com a convidat ›',

  // Mock interactive builder widget
  'welcome.builder.mock.templateLabel': 'Tria plantilla',
  'welcome.builder.mock.t1Name': 'Líders amb descompte',
  'welcome.builder.mock.t1Rule': 'SMA 50 > 150 > 200 · zona de descompte 5%–15%',
  'welcome.builder.mock.t1Budget': '100 € / fase · màx. 3 fases',
  'welcome.builder.mock.t2Name': 'Empreses en remuntada',
  'welcome.builder.mock.t2Rule': '≥ 30% sota el màxim anual · +15% sobre SMA 50 + volum',
  'welcome.builder.mock.t2Budget': '150 € / fase · màx. 2 fases',
  'welcome.builder.mock.t3Name': 'Mercats baixistes',
  'welcome.builder.mock.t3Rule': 'Caiguda de l’S&P 500 ≥ 20% · rebot del +5% des del mínim',
  'welcome.builder.mock.t3Budget': '200 € / fase · màx. 5 fases',
  'welcome.builder.mock.ruleLabel': 'Regles del senyal',
  'welcome.builder.mock.budgetLabel': 'Import per fase',
  'welcome.builder.mock.stocksLabel': 'Accions a vigilar',
  'welcome.builder.mock.fundLabel': 'El fons que vigila',
  'welcome.builder.mock.bearFund': 'SPY · Fons S&P 500 ✓',

  // Pillar 2: Simulation
  'welcome.sim.tag': '02 // SIMULA',
  'welcome.sim.badge': 'SIMULACIÓ',
  'welcome.sim.title': 'Simulació en temps real',
  'welcome.sim.tagline': 'Posa-la a prova amb preus reals i zero risc financer',
  'welcome.sim.summary':
    'Simula operacions amb preus reals des d’avui mateix. Posa a prova l’execució i compara rendiments amb el fons de referència (S&P 500) i el pitjor any històric.',
  'welcome.sim.point1': 'Preus reals de tancament actualitzats diàriament després del tancament del mercat dels EUA.',
  'welcome.sim.point2': 'Registre automàtic d’operacions simulades segons les regles exactes de la teva estratègia.',
  'welcome.sim.point3': 'Mètriques de rendiment honestes al costat del fons de referència i el pitjor any històric.',
  'welcome.sim.cta': 'Prova el mode simulació ›',

  // Mock interactive simulation widget
  'welcome.sim.mock.mode': 'SIMULACIÓ',
  'welcome.sim.mock.returnLabel': 'Rendiment de l’estratègia',
  'welcome.sim.mock.indexLabel': 'Fons S&P 500',
  'welcome.sim.mock.worstLabel': 'El pitjor any',
  'welcome.sim.mock.tabPerf': 'Comparació de rendiment',
  'welcome.sim.mock.tabTrades': 'Registre d’operacions simulades',
  'welcome.sim.mock.log1': '1 fase comprada · AAPL a 178,40 $',
  'welcome.sim.mock.log2': '1 fase comprada · MSFT a 412,10 $',
  'welcome.sim.mock.note': 'El seguiment de referència utilitza preus de tancament diaris.',

  // Pillar 3: Trading tracker
  'welcome.tracker.tag': '03 // SEGUIMENT',
  'welcome.tracker.badge': 'SEGUIMENT MANUAL',
  'welcome.tracker.title': 'Seguiment d’operacions',
  'welcome.tracker.tagline': 'Opera al teu broker. 100% privat i no custodi',
  'welcome.tracker.summary':
    'Rep avisos diaris quan es compleixin les teves regles. Confirma, posposa o omet fases en segons. No et demanem mai contrasenyes ni claus de broker.',
  'welcome.tracker.point1': 'Zero sincronització amb el broker: sense contrasenyes, OAuth, claus d’API ni accés als teus diners.',
  'welcome.tracker.point2': 'Avisos diaris de senyals que indiquen quina regla s’ha complert i quina fase representa segons la teva configuració.',
  'welcome.tracker.point3': 'Registra les operacions amb els preus reals d’execució del broker o omet senyals amb un motiu ràpid.',
  'welcome.tracker.cta': 'Explora el seguiment manual ›',

  // Mock interactive tracker widget
  'welcome.tracker.mock.mode': 'MANUAL',
  'welcome.tracker.mock.signalWaiting': 'Senyal esperant',
  'welcome.tracker.mock.signalStock': 'Zscaler (ZS)',
  'welcome.tracker.mock.signalDetail': 'Baixada del 8,2% sota el màxim anual · Per sobre de SMA 200 · Fase 1 de 3',
  'welcome.tracker.mock.prompt': 'Has actuat sobre el senyal?',
  'welcome.tracker.mock.btnDidIt': 'Ho he fet',
  'welcome.tracker.mock.btnSkipped': 'L’he omès',
  'welcome.tracker.mock.btnSnooze': 'Recorda-m’ho demà',
  'welcome.tracker.mock.statusDidIt': '✓ Operació registrada: fase de 100 € anotada. Efectiu i rendiment actualitzats.',
  'welcome.tracker.mock.statusSkipped': 'Fase omesa: motiu registrat. Les regles continuen vigilant.',
  'welcome.tracker.mock.statusSnooze': 'Posposat: recordatori programat per a la propera sessió.',
  'welcome.tracker.mock.privacyCallout': 'STL mai no toca el teu broker. Mantens el control total de les teves operacions.',

  // Toggle & expansion controls
  'welcome.expand': 'Mostra detalls ▾',
  'welcome.collapse': 'Amaga detalls ▴',

  // Trust & Architecture section
  'welcome.trust.eyebrow': 'PER QUÈ STRATEGY TRADING LAB',
  'welcome.trust.title': 'Dissenyat per a la transparència i el control',
  'welcome.trust.card1Title': 'Zero accés al broker',
  'welcome.trust.card1Text':
    'Mai no et demanem credencials de broker ni permisos d’operativa. Els teus diners continuen exclusivament a les teves mans.',
  'welcome.trust.card2Title': 'Honestedat sense concessions',
  'welcome.trust.card2Text':
    'Cada estratègia es mostra al costat d’un fons de referència (S&P 500) i del seu pitjor any històric. Sense falses promeses.',
  'welcome.trust.card3Title': 'Regles sistemàtiques disciplinades',
  'welcome.trust.card3Text':
    'Regles matemàtiques definides prèviament que eliminen les compres impulsives i les vendes de pànic en moments de volatilitat.',

  // Bottom CTA
  'welcome.bottom.title': 'A punt per provar la teva primera estratègia?',
  'welcome.bottom.lead':
    'Explora totes les plantilles, personalitza la configuració i executa simulacions sense cap compromís. Sense targeta de crèdit.',
  'welcome.bottom.tryGuest': 'PROVA COM A CONVIDAT ›',
  'welcome.bottom.register': 'Crea un compte',
}
