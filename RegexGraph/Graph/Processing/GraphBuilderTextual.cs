// =============================================================
// GraphBuilder.Textual.cs
// -------------------------------------------------------------
// A minimal, dependency‑free utility that emits a *pure text*
// representation (Cypher ready) of a populated `TransformationGraph`.
// It can be used in console apps or unit tests when you do *not*
// want to touch Neo4j at all.
// =============================================================

// =============================================================
// GraphBuilder.Textual.cs  –-  IMPLEMENTA IGraphBuilder
// =============================================================

using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegexNodeGraph.Graph.GraphCore;
using RegexNodeGraph.Model;

namespace RegexNodeGraph.Graph.Processing;

/// <summary>
/// Builder che NON parla con Neo4j: genera un unico script Cypher
/// completamente in memoria. Implementa <see cref="IGraphBuilder"/>
/// così può essere sostituito dove prima usavi un builder “reale”.
/// </summary>
public sealed class GraphBuilderTextual : IGraphBuilder
{
    /* ---------------------------------------------------------
     *   storage interno
     * ------------------------------------------------------- */
    private readonly HashSet<DetailedTransactionNode> _detailed = new();
    private readonly HashSet<AggregatedTransactionsNode> _agg = new();
    private readonly HashSet<MembershipEdge> _mEdges = new();
    private readonly List<DetailTransformationEdge> _dEdges = new();
    private readonly List<TransformationEdge> _aEdges = new();

    private readonly StringBuilder _sb = new();

    /* ---------------------------------------------------------
     *   IGraphBuilder - nodi
     * ------------------------------------------------------- */
    public void AddNode(AggregatedTransactionsNode node) => _agg.Add(node);
    public void AddNode(DetailedTransactionNode node) => _detailed.Add(node);

    /* ---------------------------------------------------------
     *   IGraphBuilder - archi
     * ------------------------------------------------------- */
    public void AddMembershipEdge(DetailedTransactionNode src,
                                  AggregatedTransactionsNode dst)
        => _mEdges.Add(new MembershipEdge { SourceNode = src, TargetNode = dst });

    public void AddDetailEdge(DetailedTransactionNode src,
        DetailedTransactionNode dst,
        TransformationRuleBase rule)
        => _dEdges.Add(new DetailTransformationEdge
        {
            SourceNode = src,
            TargetNode = dst,
            Rule = rule
        });

    public void AddAggregatedEdge(AggregatedTransactionsNode src,
                                  AggregatedTransactionsNode dst,
                                  TransformationRuleBase rule,
                                  int transformationCount,
                                  IReadOnlyList<TransformationDebugData> debug)
        => _aEdges.Add(new TransformationEdge
        {
            SourceNode = src,
            TargetNode = dst,
            Rule = rule,
            TransformationCount = transformationCount,
            DebugData = debug.ToList()
        });

    /* ---------------------------------------------------------
     *   IGraphBuilder - output / lifecycle
     * ------------------------------------------------------- */
    public void Reset()
    {
        _sb.Clear();
        _detailed.Clear();
        _agg.Clear();
        _mEdges.Clear();
        _dEdges.Clear();
        _aEdges.Clear();
    }

    public string Build() => Generate();          // alias semantico
    public string Generate()
    {
        _sb.Clear();
        WritePreamble();
        WriteNodes();
        WriteMembershipEdges();
        WriteDetailEdges();
        WriteAggregatedEdges();
        WritePostProcessing();
        return _sb.ToString();
    }

    /* ---------------------------------------------------------
     *   rendering helpers
     * ------------------------------------------------------- */

    private void WritePreamble()
    {
        _sb.AppendLine("// wipe DB");
        _sb.AppendLine("MATCH (n) DETACH DELETE n;\n");
        _sb.AppendLine("CREATE CONSTRAINT IF NOT EXISTS FOR (n:DetailedNode)   REQUIRE n.id IS UNIQUE;");
        _sb.AppendLine("CREATE CONSTRAINT IF NOT EXISTS FOR (n:AggregatedNode) REQUIRE n.id IS UNIQUE;\n");
    }

    private void WriteNodes()
    {
        if (_detailed.Any())
        {
            var d = _detailed.Select(n => $"{{id:{n.Id},descr:'{Esc(n.Description.CurrentDescription)}'}}");
            _sb.AppendLine("UNWIND [" + string.Join(",", d) + "] AS r");
            _sb.AppendLine("CREATE (:DetailedNode {id:r.id, description:r.descr});\n");
        }

        if (_agg.Any())
        {
            var a = _agg.Select(n => $"{{id:{n.Id},cnt:{n.Cardinality}}}");
            _sb.AppendLine("UNWIND [" + string.Join(",", a) + "] AS r");
            _sb.AppendLine("CREATE (:AggregatedNode {id:r.id, descriptions_count:r.cnt});\n");
        }
    }

    private void WriteMembershipEdges()
    {
        if (!_mEdges.Any()) return;
        var rows = _mEdges.Select(e => $"{{f:{e.SourceNode.Id},t:{e.TargetNode.Id}}}");
        _sb.AppendLine("UNWIND [" + string.Join(",", rows) + "] AS r");
        _sb.AppendLine("MATCH (f:DetailedNode  {id:r.f})");
        _sb.AppendLine("MATCH (t:AggregatedNode{id:r.t})");
        _sb.AppendLine("CREATE (f)-[:BELONGS_TO]->(t);\n");
    }

    private void WriteDetailEdges()
    {
        if (!_dEdges.Any()) return;
        var rows = _dEdges.Select(e =>
        {
            var rx = (e.Rule as IRegexRuleMetadata)?.From ?? "";
            return $"{{f:{e.SourceNode.Id},t:{e.TargetNode.Id},rx:'{Esc(rx)}'}}";
        });
        _sb.AppendLine("UNWIND [" + string.Join(",", rows) + "] AS r");
        _sb.AppendLine("MATCH (f:DetailedNode {id:r.f})");
        _sb.AppendLine("MATCH (t:DetailedNode {id:r.t})");
        _sb.AppendLine("CREATE (f)-[:DETAIL_TRANSFORMS {regex:r.rx}]->(t);\n");
    }

    private void WriteAggregatedEdges()
    {
        if (!_aEdges.Any()) return;
        var rows = _aEdges.Select(e =>
        {
            var rx = (e.Rule as IRegexRuleMetadata)?.From ?? "";
            return $"{{f:{e.SourceNode.Id},t:{e.TargetNode.Id},rx:'{Esc(rx)}',c:{e.TransformationCount}}}";
        });
        _sb.AppendLine("UNWIND [" + string.Join(",", rows) + "] AS r");
        _sb.AppendLine("MATCH (f:AggregatedNode {id:r.f})");
        _sb.AppendLine("MATCH (t:AggregatedNode {id:r.t})");
        _sb.AppendLine("CREATE (f)-[:AGGREGATED_TRANSFORMS {regex:r.rx, count:r.c}]->(t);\n");
    }

    private void WritePostProcessing()
    {
        _sb.AppendLine("MATCH (n:AggregatedNode) WHERE NOT (n)<-[:AGGREGATED_TRANSFORMS]-() SET n.is_root=true;\n");
        _sb.AppendLine("MATCH p=(r:AggregatedNode{is_root:true})-[:AGGREGATED_TRANSFORMS*]->(n)");
        _sb.AppendLine("WITH n,length(p) AS hops SET n.steps_from_root=hops;\n");
    }

    private static string Esc(string s) => s.Replace("\\", "\\\\").Replace("'", "\\'");
}
