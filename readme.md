# RegexNodeGraph  

![License](https://img.shields.io/badge/license-MIT-blue.svg)  
![NuGet](https://img.shields.io/nuget/v/RegexNodeGraph.svg)  
![Build Status](https://img.shields.io/github/actions/workflow/status/YourUsername/RegexNodeGraph/build.yml)  

**Versione:** 1.1.0 (2025-04-26)  

---

## 🚀 Introduzione

**RegexNodeGraph** è una libreria avanzata per la gestione di regole di trasformazione testuale basate su espressioni regolari. A partire dalla versione **1.1.0**, la logica di applicazione delle regex è stata **separata** dalla struttura a grafo, migliorando modularità e manutenibilità:

- **`RegexDescription`** rimane un **DTO** (pattern, replacement, metadata).
- **`ITransformationRule`** definisce il contratto generico per ogni regola.
- **`RegexTransformationRule`** incapsula l’esecuzione (Apply/Simulate) e il tracciamento thread-safe.
- **`TransformationGraph`** (ex `RegexTransformationGraph`) gestisce la topologia del grafo senza dipendere dall’implementazione delle regole.

Questa architettura permette in futuro di integrare **regole non-regex** (AI, NLP, normalizzazioni) semplicemente offrendo nuove implementazioni di `ITransformationRule`.

---

## 🎯 Obiettivi e Visione

RegexNodeGraph è pensata per chi deve:

- Applicare regole di trasformazione a testi di vario tipo (transazioni bancarie, log, descrizioni libere).
- Monitorare con precisione ogni passaggio di trasformazione.
- Analizzare conflitti e sovrapposizioni tramite una **struttura a grafo**.
- Esportare il grafo in database come **Neo4j** per query avanzate.

---

## 📦 Struttura del Progetto

```
RegexNodeGraph/
├── src/
│   ├── Connection/
│   │   └── Neo4jConnection.cs
│   ├── Graph/
│   │   ├── AggregatedTransactionsNode.cs
│   │   ├── DetailedTransactionNode.cs
│   │   ├── MembershipEdge.cs
│   │   ├── TransformationEdge.cs
│   │   └── TransformationGraph.cs       // ex RegexTransformationGraph
│   ├── RegexRules/
│   │   ├── RegexDescription.cs          // DTO
│   │   ├── RegexTransformationRule.cs   // logica Apply/Simulate
│   │   └── RegexRuleBuilder.cs
│   ├── Runtime/
│   │   ├── RegexDebugData.cs
│   │   └── RegexHelper.cs
│   └── Application/
│       └── GenericCategorization.cs     // pipeline e orchestrazione
├── tests/
│   ├── RegexRulesTests.cs
│   └── TransformationGraphTests.cs
├── README.md
├── LICENSE
└── RegexNodeGraph.sln
```

---

## 🚀 Quickstart

### 1. Definizione delle Regole (DTO)

```csharp
using RegexNodeGraph.RegexRules;

// Crei descrizioni "pure" di regole
var descriptions = new List<RegexDescription> {
    new RegexDescription(@"(pizza)", "panino", ConfigOptions.NonUscireInCasoDiMatch) { Description = "Sostituisce pizza" },
    new RegexDescription(@"(\d{4}-\d{2}-\d{2})", "DATA", ConfigOptions.EsciInCasoDiMatch)
};
```

### 2. Creazione delle Regole Eseguibili

```csharp
using RegexNodeGraph.RegexRules;
using RegexNodeGraph.Graph.GraphCore;

// Factory o builder per trasformare DTO → motore
var rules: List<ITransformationRule> = descriptions
    .Select(desc => new RegexTransformationRule(
        pattern: desc.From,
        replacement: desc.To,
        name: desc.Description,
        categories: desc.Categories,
        opts: desc.ConfigOptions
    ))
    .Cast<ITransformationRule>()
    .ToList();
```

### 3. Costruzione del Grafo

```csharp
using RegexNodeGraph.Graph.Processing;
using RegexNodeGraph.Model;

var texts = new List<string> { "Ho ordinato una pizza il 2025-04-26" };
var descriptions = texts.Select(t => new Description(t)).ToList();

// Applichi regole e costruisci il grafo
var graph = new TransformationGraph();
graph.BuildGraph(descriptions, descriptionsOfType<RegexDescription>());
```

### 4. Analisi e Debug

```csharp
// Ottieni report debug
string report = GenericCategorization.GenerateDebugReportForTransaction(descriptions[0], graph);
Console.WriteLine(report);
```

---

## 🛠 Integrazione con Neo4j (Opzionale)

```csharp
using RegexNodeGraph.Graph.Processing;
using RegexNodeGraph.Connection;

// Costruisci query Cypher dal grafo
var cypher = new CypherQueryGenerator(graph).GenerateCypherQueries();

// Esegui su Neo4j
using var conn = new Neo4jConnection("bolt://localhost:7687", "user", "pwd");
await new GraphBuilder(false)
    .ExecuteQueriesAsync(conn.Driver);
```

---

## 🔍 Analisi dei Risultati

- **Testi Finali**: Mostra come ogni stringa è stata modificata (es. "Ho ordinato una panino il DATA").
- **Grafo delle Trasformazioni**: Permette di visualizzare, tramite nodi e archi, come le stringhe si sono evolute regola per regola.

## 🛠️ Funzionalità Principali

- **Motore di Trasformazione a Grafo**: Ogni applicazione di regola è tracciata nel grafo.
- **Categorizzazione via Regex**: Definisci e applica regole di categorizzazione con facilità.
- **Integrazione con Neo4j**: Esporta il grafo per analisi avanzate.
- **Debug e Analisi Avanzata**: Identifica conflitti e sovrapposizioni di regex, monitora l’uso delle regole.

## 📈 Strumenti e Dipendenze

- **EPPlus**: Lettura/scrittura Excel.
- **ReactiveUI**: Binding reattivo, aggiornamenti automatici.
- **OxyPlot**: Motore di plotting.
- **MathNet.Numerics**: Calcoli statistici, spline, ecc.
- **Neo4j.Driver (opzionale)**: Integrazione con database Neo4j.
- **Telerik UI (opzionale)**: Componenti UI aggiuntivi.

---

## 📚 Changelog

### [0.7.0] - 2025-04-26
- **Architettura**: separata la logica di regex (RegexTransformationRule) dal modello a grafo (TransformationGraph).
- **DTO vs Logic**: RegexDescription ora funge solo da DTO per pattern e metadata.
- **Interfaccia `ITransformationRule`**: unifica Apply, Simulate, Apply(string).
- **Refactoring**: renaming di RegexTransformationGraph → TransformationGraph.
- **Namespace**: spostate classi in `RegexRules`, `Graph`, `Graph.Processing` per chiarezza.
- **Compatibilità**: mantienuti riferimenti a RegexDescription in edge per preservare From/To.

### [0.6.0] - 2025-01-26
- Prima release: motore di trasformazione a grafo basato su regex, con tracciamento dettagliato.

---

## 🤝 Contribuisci

RegexNodeGraph è un progetto open-source: invia PR, suggerisci miglioramenti o nuovi tipi di regole!  

## 📄 Licenza

Rilasciato sotto **MIT License**.  
Buon coding con **RegexNodeGraph**!  



> **"La potenza di una pipeline di testo si misura dalla chiarezza di ogni suo passaggio"**  
> – Anonimo

Buon divertimento con **RegexNodeGraph**!

---

## 📫 Contatti

**Autore:** [Gianluigi Salvi](https://github.com/slim16165)  
**Email:** [gianluigi.salvi@gmail.com](mailto:gianluigi.salvi@gmail.com)  
**Ultimo Aggiornamento:** 26 Gennaio 2025
