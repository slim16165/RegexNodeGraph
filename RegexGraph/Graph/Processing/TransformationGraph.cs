using System;
using System.Collections.Generic;
using System.Linq;
using RegexNodeGraph.Graph.GraphCore;
using RegexNodeGraph.Model;
using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Graph.Processing;

public class TransformationGraph
{
    private readonly object _lock = new object();
    public List<GraphNode> Nodes { get; private set; } = new List<GraphNode>();
    public List<GraphEdge> Edges { get; private set; } = new List<GraphEdge>();

    public void BuildGraph(List<Description> descriptions, List<RegexTransformationRule> regexes)
    {
        ClearGraph();

        var detailNodes = CreateDetailedTransactionNodes(descriptions);
        var rootNode = CreateAggregatedTransactionsNode(descriptions, detailNodes);
        ProcessNode(rootNode, regexes);
    }

    private void ClearGraph()
    {
        lock (_lock)
        {
            Nodes.Clear();
            Edges.Clear();
        }
    }

    private List<DetailedTransactionNode> CreateDetailedTransactionNodes(List<Description> descriptions)
    {
        return descriptions.Select(desc => CreateDetailedTransactionNode(desc)).ToList();
    }

    private DetailedTransactionNode CreateDetailedTransactionNode(Description description)
    {
        var node = new DetailedTransactionNode
        {
            Id = GenerateUniqueId(),
            Description = description
        };

        lock (_lock)
        {
            Nodes.Add(node);
        }

        return node;
    }

    private AggregatedTransactionsNode CreateAggregatedTransactionsNode(List<Description> descriptions, List<DetailedTransactionNode> detailNodes)
    {
        AggregatedTransactionsNode node;
        lock (_lock)
        {
            node = new AggregatedTransactionsNode
            {
                Id = GenerateUniqueId(),
                Descriptions = descriptions
            };
            Nodes.Add(node);
        }

        // Separare la creazione degli archi dalla creazione del nodo
        // PARTE DETTAGLIO
        // Creazione degli archi di appartenenza tra nodi di dettaglio e il nodo aggregato
        // Per ciascuna transazione bancaria, creiamo un nodo di dettaglio (bank_transaction)
        AddMembershipEdges(detailNodes, node);

        return node;
    }

    private void AddMembershipEdges(List<DetailedTransactionNode> detailNodes, AggregatedTransactionsNode aggregatedNode)
    {
        foreach (var detailNode in detailNodes)
        {
            AddMembershipEdge(detailNode, aggregatedNode);
        }
    }

    public void AddMembershipEdge(DetailedTransactionNode detailNode, AggregatedTransactionsNode aggregatedNode)
    {
        var edge = new MembershipEdge
        {
            SourceNode = detailNode,
            TargetNode = aggregatedNode
        };
        AddEdge(edge);
    }

    public void AddAggregatedTransformationEdge(AggregatedTransactionsNode source, AggregatedTransactionsNode target, RegexTransformationRule rule, int count, List<RegexDebugData> debugData)
    {
        var edge = new TransformationEdge
        {
            SourceNode = source,
            TargetNode = target,
            RegexRule = rule,
            TransformationCount = count,
            DebugData = debugData
        };
        AddEdge(edge);
    }

    private void AddDetailTransformationEdge(DetailedTransactionNode sourceNode, DetailedTransactionNode targetNode, RegexTransformationRule rule, RegexDebugData debugData)
    {
        var edge = new DetailTransformationEdge
        {
            SourceNode = sourceNode,
            TargetNode = targetNode,
            RegexRule = rule,
            DebugData = debugData
        };
        AddEdge(edge);
    }

    private int _currentId = 0;
    private int GenerateUniqueId()
    {
        lock (_lock)
        {
            return _currentId++;
        }
    }

    // Implementazione del metodo AddEdge centralizzato
    private void AddEdge(GraphEdge edge)
    {
        lock (_lock)
        {
            // Evita di creare un self-loop
            if (edge.SourceNode.Id == edge.TargetNode.Id)
            {
                string regexFrom = edge is TransformationEdge te ? te.RegexRule.From : edge is DetailTransformationEdge de ? de.RegexRule.From : "";
                Console.WriteLine($"Self-loop detected and avoided for node {edge.SourceNode.Id} with regex '{regexFrom}'");
                return;
            }

            Edges.Add(edge);

            // Aggiungi l'arco all'elenco degli archi in uscita del nodo, se applicabile
            if (edge is TransformationEdge transformationEdge && transformationEdge.SourceNode is AggregatedTransactionsNode aggSource)
            {
                aggSource.OutgoingEdges.Add(transformationEdge);
            }
        }
    }

    private void ProcessNode(AggregatedTransactionsNode node, List<RegexTransformationRule> rules)
    {
        foreach (var rule in rules)
        {
            // Filtriamo le descrizioni che devono essere saltate
            var skippedDescriptions = node.Descriptions
                .Where(description => description.IsCategorized && rule.EsciInCasoDiMatch)
                .ToList();

            // Applichiamo la regola alle descrizioni restanti
            var processedItems = node.Descriptions
                .Where(description => !(description.IsCategorized && rule.EsciInCasoDiMatch))
                .Select(description =>
                {
                    var (modifiedDescr, result) = ApplyRule(rule, description);
                    return new { OriginalDescription = description, ModifiedDescription = modifiedDescr, DebugInfo = result };
                })
                .ToList();

            // Separiamo le descrizioni trasformate da quelle non trasformate
            var transformedItems = processedItems
                .Where(item => item.ModifiedDescription.HasChangedNow)
                .ToList();

            var unchangedItems = processedItems
                .Where(item => !item.ModifiedDescription.HasChangedNow)
                .Select(item => item.OriginalDescription)
                .Concat(skippedDescriptions)
                .ToList();

            // Se non ci sono trasformazioni, passiamo alla prossima regola
            if (!transformedItems.Any())
                continue;

            // Creazione dei nodi di dettaglio trasformati
            var transformedDetailNodes = transformedItems
                .Select(item => CreateDetailedTransactionNode(item.ModifiedDescription))
                .ToList();

            // Creazione del nuovo nodo aggregato per le descrizioni trasformate
            var transformedDescriptions = transformedItems.Select(item => item.ModifiedDescription).ToList();
            var targetNode = CreateAggregatedTransactionsNode(transformedDescriptions, transformedDetailNodes);

            // Aggiunta dell'edge di trasformazione aggregata
            var debugData = transformedItems.Select(item => item.DebugInfo).ToList();
            AddAggregatedTransformationEdge(node, targetNode, rule, transformedDescriptions.Count, debugData);

            // Gestione delle descrizioni non trasformate
            // Le transazioni non trasformate rimangono nel nodo aggregato originale
            HandleUnchangedDescriptions(unchangedItems, node);

            // Gestione delle trasformazioni a livello di dettaglio
            var detailTransformations = transformedItems
                .Select(item => (item.OriginalDescription, item.ModifiedDescription, item.DebugInfo))
                .ToList();

            HandleDetailTransformations(detailTransformations, rule);

            // Assegnazione delle transazioni trasformate al nuovo nodo aggregato
            AssignTransformedNodesToAggregated(transformedDetailNodes, targetNode);

            // Ricorsione sulle transazioni trasformate
            ProcessNode(targetNode, rules.Skip(rules.IndexOf(rule) + 1).ToList());
        }
    }


    private void HandleUnchangedDescriptions(List<Description> unchangedDescriptions, AggregatedTransactionsNode originalNode)
    {
        foreach (var unchangedDesc in unchangedDescriptions)
        {
            DetailedTransactionNode detailNode;
            lock (_lock)
            {
                detailNode = Nodes.OfType<DetailedTransactionNode>().FirstOrDefault(n => n.Description == unchangedDesc);
            }
            if (detailNode != null)
            {
                AddMembershipEdge(detailNode, originalNode); // Appartenenza a nodo aggregato originale
            }
        }
    }

    private void HandleDetailTransformations(List<(Description sourceDesc, Description targetDesc, RegexDebugData debugInfo)> detailTransformations, RegexTransformationRule rule)
    {
        foreach (var (sourceDesc, targetDesc, debugInfo) in detailTransformations)
        {
            DetailedTransactionNode sourceNode;
            DetailedTransactionNode targetNodeDetail;
            lock (_lock)
            {
                sourceNode = Nodes.OfType<DetailedTransactionNode>().First(n => n.Description == sourceDesc);
                targetNodeDetail = Nodes.OfType<DetailedTransactionNode>().FirstOrDefault(n => n.Description == targetDesc);
                if (targetNodeDetail == null)
                {
                    targetNodeDetail = CreateDetailedTransactionNode(targetDesc);
                }
            }

            AddDetailTransformationEdge(sourceNode, targetNodeDetail, rule, debugInfo);
        }
    }

    private void AssignTransformedNodesToAggregated(List<DetailedTransactionNode> transformedNodes, AggregatedTransactionsNode aggregatedNode)
    {
        foreach (var transformedNode in transformedNodes)
        {
            AddMembershipEdge(transformedNode, aggregatedNode);
        }
    }

    private (Description description, RegexDebugData result) ApplyRule(RegexTransformationRule rule, Description description)
    {
        // Applichiamo la sostituzione regex, che aggiorna la descrizione se c'è un match
        (var regexDebugData, description) = rule.ApplyReplacement(description);

        //Se non è cambiata, valuta se devi testare un retry dall'originale
        if (!description.HasChangedNow)
        {
            if (description.WasEverModified && rule.ShouldRetryFromOrig)
            {
                return HandleRetryFromOriginal(rule, description);
            }
        }

        // Altrimenti, ritorniamo la descrizione corrente (modificata o meno)
        return (description, regexDebugData);
    }

    private (Description description, RegexDebugData result) HandleRetryFromOriginal(RegexTransformationRule rule, Description description)
    {
        var simulatedResult = rule.SimulateApplication(description.OriginalDescription);

        if (simulatedResult.IsMatch)
        {
            var breakingRules = FindInterferingRules(description.OriginalDescription, rule).ToList();
            if (breakingRules.Any())
            {
                string log = $"La regola '{breakingRules[0].From}' (applicata prima) sta interferendo con la regola corrente '{rule.From}'. L'input originale era '{description.OriginalDescription}'.";
                // Log o gestione dell'interferenza
                // Utilizza un logger appropriato invece di Console.WriteLine
                Console.WriteLine(log);
            }
        }

        // Aggiorniamo la descrizione solo se c'è stato un match simulato
        if (simulatedResult.IsMatch)
        {
            description.UpdateDescription(simulatedResult.Output);
        }

        return (description, simulatedResult);
    }

    public IEnumerable<RegexTransformationRule> FindInterferingRules(string input, RegexTransformationRule rule2)
    {
        var rules = Edges.OfType<TransformationEdge>().Select(e => e.RegexRule).Distinct().ToList();
        foreach (RegexTransformationRule rule1 in rules)
        {
            if (rule1 == rule2)
                break;

            var result1 = rule1.ApplyReplacement(input);

            if (!result1.IsMatch || result1.Output == input)
            {
                continue;
            }
            else
            {
                var result2 = rule2.ApplyReplacement(result1.Output);
                if (!result2.IsMatch)
                    yield return rule1;
            }
        }
    }

    /// <summary>
    /// Il metodo cerca di trovare la descrizione finale di una transazione partendo da una descrizione originale e attraversando il grafo di trasformazioni regex. Si ferma quando non ci sono più trasformazioni disponibili o quando un arco specifica l'opzione di interruzione (EsciInCasoDiMatch). Se non viene trovata alcuna corrispondenza iniziale, viene restituito l'input originale.
    /// </summary>
    /// <summary>
    /// Trova la descrizione finale di una transazione attraversando il grafo di trasformazioni regex.
    /// Si ferma quando non ci sono più trasformazioni disponibili o quando un arco specifica l'opzione di interruzione (EsciInCasoDiMatch).
    /// Se non viene trovata alcuna corrispondenza iniziale, ritorna l'input originale.
    /// </summary>
    /// <param name="originalDescription">La descrizione originale della transazione.</param>
    /// <param name="graph">Il grafo di trasformazioni regex.</param>
    /// <returns>La descrizione finale dopo l'applicazione delle trasformazioni.</returns>
    public static string UpdateOriginalItemsWithFinalCategory(string originalDescription, TransformationGraph graph)
    {
        //Recupera il nodo di dettaglio che corrisponde alla descrizione fornita.
        var detailedNode = graph.Nodes
            .OfType<DetailedTransactionNode>()
            .FirstOrDefault(n => n.Description.OriginalDescription == originalDescription);
        
        if (detailedNode == null)
        {
            Console.WriteLine("Nodo dettagliato non trovato. Ritorno input originale.");
            return originalDescription;
        }

        var finalNode = TraverseTransformations(detailedNode, graph);
        Console.WriteLine($"Descrizione finale trovata: \"{finalNode.Description.CurrentDescription}\"");

        return finalNode.Description.CurrentDescription;
    }

    /// <summary>
    /// Trova la descrizione finale di una transazione attraversando il grafo di trasformazioni regex.
    /// Si ferma quando non ci sono più trasformazioni disponibili o quando un arco specifica l'opzione di interruzione (EsciInCasoDiMatch).
    /// Se non viene trovata alcuna corrispondenza iniziale, ritorna l'input originale.
    /// </summary>
    /// <param name="originalInput">La descrizione originale della transazione.</param>
    /// <param name="graph">Il grafo di trasformazioni regex.</param>
    /// <returns>La descrizione finale dopo l'applicazione delle trasformazioni.</returns>
    private static string FindFinalCategory(string originalInput, TransformationGraph graph)
    {
        Console.WriteLine($"Inizio categorizzazione per: \"{originalInput}\"");

        var detailedNode = GetDetailedNodeByDescription(originalInput, graph);
        if (detailedNode == null)
        {
            Console.WriteLine("Nodo dettagliato non trovato. Ritorno input originale.");
            return originalInput;
        }

        var finalNode = TraverseTransformations(detailedNode, graph);
        Console.WriteLine($"Descrizione finale trovata: \"{finalNode.Description.CurrentDescription}\"");

        return finalNode.Description.CurrentDescription;
    }

    /// <summary>
    /// Recupera il nodo di dettaglio che corrisponde alla descrizione fornita.
    /// </summary>
    /// <param name="description">La descrizione della transazione da cercare.</param>
    /// <param name="graph">Il grafo di trasformazioni regex.</param>
    /// <returns>Il nodo di dettaglio corrispondente o null se non trovato.</returns>
    private static DetailedTransactionNode GetDetailedNodeByDescription(string description, TransformationGraph graph)
    {
        var node = graph.Nodes
            .OfType<DetailedTransactionNode>()
            .FirstOrDefault(n => n.Description.CurrentDescription.Equals(description, StringComparison.OrdinalIgnoreCase));

        if (node != null)
        {
            Console.WriteLine($"Nodo dettagliato trovato: ID {node.Id}");
        }
        else
        {
            Console.WriteLine("Nodo dettagliato non trovato.");
        }

        return node;
    }

    /// <summary>
    /// Attraversa le trasformazioni partendo da un nodo di dettaglio fino a trovare la descrizione finale.
    /// </summary>
    /// <param name="startNode">Il nodo di dettaglio iniziale.</param>
    /// <param name="graph">Il grafo di trasformazioni regex.</param>
    /// <returns>Il nodo di dettaglio finale dopo le trasformazioni.</returns>
    private static DetailedTransactionNode TraverseTransformations(DetailedTransactionNode startNode, TransformationGraph graph)
    {
        var currentNode = startNode;
        var visitedNodes = new HashSet<int> { currentNode.Id };
        Console.WriteLine($"Avvio traversata delle trasformazioni per nodo ID {currentNode.Id}");

        while (true)
        {
            var transformationEdge = GetFirstDetailTransformationEdge(currentNode, graph);
            if (transformationEdge == null)
            {
                Console.WriteLine("Nessun ulteriore arco di trasformazione trovato. Traversata terminata.");
                break;
            }

            var targetNode = transformationEdge.TargetNode as DetailedTransactionNode;
            if (targetNode == null)
            {
                Console.WriteLine($"L'arco di trasformazione porta a un nodo non di dettaglio (ID {transformationEdge.TargetNode.Id}). Traversata terminata.");
                break;
            }

            if (visitedNodes.Contains(targetNode.Id))
            {
                Console.WriteLine($"Ciclo rilevato al nodo ID {targetNode.Id}. Traversata terminata.");
                break;
            }

            currentNode = targetNode;
            visitedNodes.Add(currentNode.Id);
            Console.WriteLine($"Trasformazione applicata: Nodo ID {currentNode.Id}, Descrizione: \"{currentNode.Description.CurrentDescription}\"");

            if (ShouldExitOnMatch(transformationEdge.RegexRule))
            {
                Console.WriteLine("Opzione EsciInCasoDiMatch rilevata. Traversata terminata.");
                break;
            }
        }

        Console.WriteLine($"Traversata completata. Nodo finale ID {currentNode.Id}, Descrizione: \"{currentNode.Description.CurrentDescription}\"");
        return currentNode;
    }

    /// <summary>
    /// Recupera il primo arco di trasformazione di dettaglio associato al nodo corrente.
    /// </summary>
    /// <param name="sourceNode">Il nodo sorgente.</param>
    /// <param name="graph">Il grafo di trasformazioni regex.</param>
    /// <returns>Il primo arco di trasformazione di dettaglio o null se non trovato.</returns>
    private static DetailTransformationEdge GetFirstDetailTransformationEdge(DetailedTransactionNode sourceNode, TransformationGraph graph)
    {
        var edge = graph.Edges
            .OfType<DetailTransformationEdge>()
            .FirstOrDefault(e => e.SourceNode == sourceNode);

        if (edge != null)
        {
            Console.WriteLine($"Arco di trasformazione trovato: RegEx '{edge.RegexRule.From}', Target Node ID {edge.TargetNode.Id}");
        }
        else
        {
            Console.WriteLine("Nessun arco di trasformazione trovato per il nodo corrente.");
        }

        return edge;
    }

    /// <summary>
    /// Determina se una regola regex specifica di uscire in caso di match.
    /// </summary>
    /// <param name="regexRule">La regola regex da verificare.</param>
    /// <returns>True se la regola specifica di uscire in caso di match, altrimenti False.</returns>
    private static bool ShouldExitOnMatch(RegexTransformationRule regexRule)
    {
        return (regexRule.ConfigOptions & ConfigOptions.EsciInCasoDiMatch) == ConfigOptions.EsciInCasoDiMatch;
    }
}