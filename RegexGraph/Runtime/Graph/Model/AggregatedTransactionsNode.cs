﻿using System.Collections.Generic;

namespace RegexNodeGraph.Runtime.Graph.Model;

public class AggregatedTransactionsNode : GraphNode
{
    public List<Description> Descriptions { get; set; } = new List<Description>();
    public int Cardinality => Descriptions.Count;
    public List<TransformationEdge> OutgoingEdges { get; set; } = new List<TransformationEdge>();
    public List<string> Categories { get; set; }
}