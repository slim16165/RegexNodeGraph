using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegexNodeGraph.Graph.GraphCore;
using RegexNodeGraph.Model;

namespace RegexNodeGraph.Graph.Processing;

public class CypherQueryGenerator
{
    private readonly TransformationGraph _graph;
    private readonly StringBuilder _sb = new StringBuilder();

    public CypherQueryGenerator(TransformationGraph graph)
    {
        _graph = graph;
    }

    public string GenerateCypherQueries()
    {
        _sb.Clear();

        // Aggiungi una query per svuotare il database
        _sb.AppendLine("// Svuota il database");
        _sb.AppendLine("MATCH (n)");
        _sb.AppendLine("DETACH DELETE n;\n");

        // Generazione dei vincoli di unicità
        GenerateConstraints();

        // Raccolta dei dati dei nodi
        var detailedNodes = new List<string>();
        var aggregatedNodes = new List<string>();

        foreach (var node in _graph.Nodes)
        {
            if (node is AggregatedTransactionsNode aggNode)
            {
                // Preparazione dei dati per AggregatedNode
                int descriptionsCount = aggNode.Descriptions.Count;
                aggregatedNodes.Add($"{{id: {aggNode.Id}, descriptions_count: {descriptionsCount}}}");
            }
            else if (node is DetailedTransactionNode detailNode)
            {
                // Escaping delle virgolette nella descrizione
                var escapedDescription = detailNode.Description.CurrentDescription.Replace("\"", "\\\"");
                detailedNodes.Add($"{{id: {detailNode.Id}, description: \"{escapedDescription}\"}}");
            }
        }

        // Generazione batch di nodi DetailedNode
        if (detailedNodes.Any())
        {
            _sb.AppendLine("// Creazione batch di DetailedNode");
            _sb.AppendLine("UNWIND [");
            _sb.AppendLine(string.Join(",\n", detailedNodes.Select(dn => $"  {dn}")));
            _sb.AppendLine("] AS nodeData");
            _sb.AppendLine("CREATE (n:DetailedNode {id: nodeData.id, description: nodeData.description});\n");
        }

        // Generazione batch di nodi AggregatedNode
        if (aggregatedNodes.Any())
        {
            _sb.AppendLine("// Creazione batch di AggregatedNode");
            _sb.AppendLine("UNWIND [");
            _sb.AppendLine(string.Join(",\n", aggregatedNodes.Select(an => $"  {an}")));
            _sb.AppendLine("] AS nodeData");
            _sb.AppendLine("CREATE (n:AggregatedNode {id: nodeData.id, descriptions_count: nodeData.descriptions_count});\n");
        }

        // Raccolta dei dati delle relazioni
        var belongsToRelations = new List<string>();
        var aggregatedTransformsRelations = new List<string>();
        var detailTransformsRelations = new List<string>();

        foreach (var edge in _graph.Edges)
        {
            if (edge is MembershipEdge memberEdge)
            {
                belongsToRelations.Add($"{{fromId: {memberEdge.SourceNode.Id}, toId: {memberEdge.TargetNode.Id}}}");
            }
            else if (edge is TransformationEdge { Rule: IRegexRuleMetadata meta1 } transEdge)
            {
                string regexFrom = GetRegexFrom(edge);
                var escapedRegex = EscapeForCypher(regexFrom);

                aggregatedTransformsRelations.Add($"{{fromId: {transEdge.SourceNode.Id}, toId: {transEdge.TargetNode.Id}, regex: '{escapedRegex}', count: {transEdge.TransformationCount}}}");
            }
            else if (edge is DetailTransformationEdge { Rule: IRegexRuleMetadata meta2 } detailEdge)
            {
                var escapedRegex = EscapeForCypher(meta2.From);
                detailTransformsRelations.Add($"{{fromId: {detailEdge.SourceNode.Id}, toId: {detailEdge.TargetNode.Id}, regex: '{escapedRegex}'}}");
            }

        }

        belongsToRelations = belongsToRelations.Distinct().ToList();

        // Generazione batch di relazioni BELONGS_TO
        if (belongsToRelations.Any())
        {
            _sb.AppendLine("// Creazione batch di relazioni BELONGS_TO");
            _sb.AppendLine("UNWIND [");
            _sb.AppendLine(string.Join(",\n", belongsToRelations.Select(br => $"  {br}")));
            _sb.AppendLine("] AS relData");
            _sb.AppendLine("MATCH (from:DetailedNode {id: relData.fromId})");
            _sb.AppendLine("MATCH (to:AggregatedNode {id: relData.toId})");
            _sb.AppendLine("CREATE (from)-[:BELONGS_TO]->(to);\n");
        }

        // Generazione batch di relazioni AGGREGATED_TRANSFORMS
        if (aggregatedTransformsRelations.Any())
        {
            _sb.AppendLine("// Creazione batch di relazioni AGGREGATED_TRANSFORMS");
            _sb.AppendLine("UNWIND [");
            _sb.AppendLine(string.Join(",\n", aggregatedTransformsRelations.Select(ar => $"  {ar}")));
            _sb.AppendLine("] AS relData");
            _sb.AppendLine("MATCH (from:AggregatedNode {id: relData.fromId})");
            _sb.AppendLine("MATCH (to:AggregatedNode {id: relData.toId})");
            _sb.AppendLine("CREATE (from)-[:AGGREGATED_TRANSFORMS {regex: relData.regex, count: relData.count}]->(to);\n");
        }

        // Generazione batch di relazioni DETAIL_TRANSFORMS
        if (detailTransformsRelations.Any())
        {
            _sb.AppendLine("// Creazione batch di relazioni DETAIL_TRANSFORMS");
            _sb.AppendLine("UNWIND [");
            _sb.AppendLine(string.Join(",\n", detailTransformsRelations.Select(dr => $"  {dr}")));
            _sb.AppendLine("] AS relData");
            _sb.AppendLine("MATCH (from:DetailedNode {id: relData.fromId})");
            _sb.AppendLine("MATCH (to:DetailedNode {id: relData.toId})");
            _sb.AppendLine("CREATE (from)-[:DETAIL_TRANSFORMS {regex: relData.regex}]->(to);\n");
        }

        // Aggiungi il calcolo del nodo root
        _sb.AppendLine("// Calcolo del nodo root");
        _sb.AppendLine("MATCH (n:AggregatedNode)");
        _sb.AppendLine("WHERE NOT (n)<-[:AGGREGATED_TRANSFORMS]-()");
        _sb.AppendLine("SET n.is_root = true;\n");

        // Dopo aver generato i nodi e le relazioni, aggiungi il calcolo degli step trasformativi
        _sb.AppendLine("// Calcolo degli step trasformativi dal nodo root");
        _sb.AppendLine("MATCH path = (root:AggregatedNode {is_root: true})-[:AGGREGATED_TRANSFORMS*]->(n:AggregatedNode)");
        _sb.AppendLine("WITH n, length(path) AS steps");
        _sb.AppendLine("SET n.steps_from_root = steps;\n");


        return _sb.ToString();
    }

    public static string EscapeForCypher(string s) => s.Replace("'", "\\'");

    private void GenerateConstraints()
    {
        _sb.AppendLine("// Vincoli di unicità");
        _sb.AppendLine("CREATE CONSTRAINT IF NOT EXISTS FOR (n:DetailedNode) REQUIRE n.id IS UNIQUE;");
        _sb.AppendLine("CREATE CONSTRAINT IF NOT EXISTS FOR (n:AggregatedNode) REQUIRE n.id IS UNIQUE;\n");
    }

    // Metodo helper per ottenere l'etichetta del nodo
    private static string GetLabel(GraphNode node)
    {
        return node switch
        {
            AggregatedTransactionsNode _ => "AggregatedNode",
            DetailedTransactionNode _ => "DetailedNode",
            _ => "Node"
        };
    }

    private static string GetRegexFrom(GraphEdge edge)
    {
        return edge.Rule is IRegexRuleMetadata meta ? meta.From : "";
    }

}