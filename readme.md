# RegexNodeGraph

![License](https://img.shields.io/badge/license-MIT-blue.svg)
![NuGet](https://img.shields.io/nuget/v/RegexNodeGraph.svg)
![Build Status](https://img.shields.io/github/actions/workflow/status/YourUsername/RegexNodeGraph/build.yml)

## 🚀 Introduzione

**RegexNodeGraph** è una libreria avanzata per la gestione di regole di trasformazione testuale basate su espressioni regolari (regex). Oltre a fornire un motore di matching e sostituzione, traccia l’evoluzione dei testi trasformati attraverso una struttura a grafo, consentendo analisi e debug approfonditi anche su grandi volumi di dati.

## 🎯 Obiettivi e Visione

RegexNodeGraph è stata progettata per chiunque debba:

- Applicare regole di trasformazione a testi di varia natura (es. descrizioni di transazioni bancarie, log, stringhe libere).
- Monitorare con precisione come ciascuna regola interviene su ogni stringa.
- Analizzare le trasformazioni in profondità, identificando conflitti o sovrapposizioni di regex.

### **Perché una Struttura a Grafo?**

Utilizzare un grafo per tracciare le trasformazioni offre numerosi vantaggi:

- **Tracciabilità Completa**: Ogni passaggio ha un nodo dedicato e un arco che indica la regola applicata.
- **Analisi di Conflitti e Sovrapposizioni**: Il grafo evidenzia chiaramente le interazioni tra regole.
- **Storico dei Cambiamenti**: Permette di risalire alle versioni precedenti del testo e alle regole che le hanno generate.
- **Integrazione con Database Grafici**: Esporta facilmente la struttura a grafo in database come Neo4j per query avanzate.

## ⚙️ Come Funziona

### 1. Definizione delle Regole di Trasformazione

Utilizza `RegexRuleBuilder` per definire regole di trasformazione in modo fluido:

```csharp
var builder = new RegexRuleBuilder()
    .Add(@"(pizza)", "panino", "Sostituzione di 'pizza' con 'panino'")
    .Categorize("Cibo")
    .Add(@"(\d{4}-\d{2}-\d{2})", "DATA", "Mascheramento delle date")
    // Aggiungi altre regole...
    .Build();
```

### 2. Esecuzione delle Trasformazioni

Applica le regole definite a un insieme di stringhe, generando sia le stringhe trasformate sia un log dettagliato delle sostituzioni:

```csharp
var categorization = new GenericCategorization(builder);
var listaDiStringhe = new List<string> {
    "Ho ordinato una pizza il 2024-11-03",
    "Nessuna data qui" 
};

var risultato = categorization.CategorizeDescriptions(listaDiStringhe);

// Stringhe trasformate
var testiFinali = risultato.Transformate; 
// Grafo delle trasformazioni
var grafoTrasformazioni = risultato.Graph;
```

### 3. Struttura a Grafo e Debug

Il grafo delle trasformazioni (`RegexTransformationGraph`) contiene:

- **Nodi**: Rappresentano gli stati intermedi delle stringhe.
- **Archi**: Indicano quale regola ha generato ogni trasformazione.
- **Metadati**: Informazioni come conteggi, tempi di esecuzione e conflitti.

### 4. Integrazione con Neo4j (Opzionale)

Esporta il grafo delle trasformazioni in Neo4j per analisi avanzate:

```csharp
var graphBuilder = new GraphBuilder(grafoTrasformazioni);
var cypherGenerator = new CypherQueryGenerator(graphBuilder);
var batchQueries = cypherGenerator.GenerateBatchQueries();
// Esegui batchQueries su un database Neo4j
```

## 🛠 Struttura del Progetto

```
RegexNodeGraph/
├── src/
│   ├── Connection/
│   │   └── Neo4jConnection.cs        // Connessione e operazioni con Neo4j 
│   ├── Graph/
│   │   ├── AggregatedTransactionsNode.cs
│   │   ├── DetailedTransactionNode.cs
│   │   ├── TransformationEdge.cs
│   │   ├── MembershipEdge.cs
│   │   ├── GraphBuilder.cs           // Costruzione del grafo delle trasformazioni 
│   │   └── RegexTransformationGraph.cs
│   ├── RegexDescription.cs            // Definizione delle singole regole
│   ├── RegexDebugData.cs              // Dati di debug sulle esecuzioni delle regole
│   ├── RegexHelper.cs                 // Metodi di utilità per la sintassi delle regex
│   ├── RegexRuleBuilder.cs            // Costruzione fluida di regole
│   └── GenericCategorization.cs       // Processo di applicazione delle regole e costruzione del grafo
├── tests/
│   ├── RegexTesterTests.cs            // Test per il core delle regex
│   ├── TransformationGraphTests.cs    // Test per la struttura del grafo
│   └── ...
├── README.md                           // Documentazione
├── RegexNodeGraph.sln                      // Soluzione Visual Studio
├── LICENSE                             // Licenza MIT
└── RegexNodeGraph.nuspec                   // Configurazione per il pacchetto NuGet
```

## 📚 Esempio di Utilizzo

```csharp
// Definizione delle regole di trasformazione
var builder = new RegexRuleBuilder()
    .Add(@"(pizza)", "panino", "Sostituzione di 'pizza' con 'panino'")
    .Categorize("Cibo")
    .Add(@"(\d{4}-\d{2}-\d{2})", "DATA", "Mascheramento delle date")
    .Build();

// Creazione della pipeline e applicazione delle regole
var categorization = new GenericCategorization(builder);
var listaDiStringhe = new List<string> {
    "Ho ordinato una pizza il 2024-11-03",
    "Nessuna data qui" 
};

var risultato = categorization.CategorizeDescriptions(listaDiStringhe);

// Accesso ai risultati
var testiFinali = risultato.Transformate; 
var grafoTrasformazioni = risultato.Graph;

// Stampa dei risultati
foreach(var testo in testiFinali)
{
    Console.WriteLine(testo);
}

// Utilizzo del grafo per analisi avanzate
var graphBuilder = new GraphBuilder(grafoTrasformazioni);
var cypherGenerator = new CypherQueryGenerator(graphBuilder);
var batchQueries = cypherGenerator.GenerateBatchQueries();
// Esegui batchQueries su Neo4j
```

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

## 🤝 Contribuisci

RegexNodeGraph è un progetto open-source e le tue idee sono fondamentali per la sua crescita! Ecco come puoi contribuire:

1. **Forka il repository**
2. **Crea un branch** per la tua modifica
3. **Implementa la tua modifica**
4. **Invia una Pull Request**

Tutti i suggerimenti per migliorare RegexNodeGraph, aggiungere nuove regole o ottimizzare la pipeline di trasformazione sono benvenuti!

## 📄 Licenza

RegexNodeGraph è rilasciato sotto licenza [MIT](LICENSE). Puoi utilizzarlo liberamente sia in contesti personali che commerciali, nel rispetto della licenza.

---

> **"La potenza di una pipeline di testo si misura dalla chiarezza di ogni suo passaggio"**  
> – Anonimo

Buon divertimento con **RegexNodeGraph**!

---

## 📫 Contatti

**Autore:** [Gianluigi Salvi](https://github.com/slim16165)  
**Email:** [gianluigi.salvi@gmail.com](mailto:gianluigi.salvi@gmail.com)  
**Ultimo Aggiornamento:** 26 Gennaio 2025
