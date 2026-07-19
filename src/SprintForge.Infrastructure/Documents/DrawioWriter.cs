using System.Text;
using System.Xml;
using SprintForge.Application.Documents;

namespace SprintForge.Infrastructure.Documents;

/// <summary>
///   Generates valid mxGraph XML (Draw.io format) natively — no external tools or cloud calls.
///   Layout uses a simple grid: gateway/clients on the left column, services in the centre,
///   data/messaging on the right. Geometry is computed from component kind and count.
/// </summary>
public sealed class DrawioWriter : IDrawioWriter
{
    private static readonly Dictionary<ComponentKind, string> StyleMap = new()
    {
        [ComponentKind.ApiGateway]     = "rounded=1;whiteSpace=wrap;html=1;fillColor=#dae8fc;strokeColor=#6c8ebf;fontStyle=1;",
        [ComponentKind.Service]        = "rounded=1;whiteSpace=wrap;html=1;fillColor=#d5e8d4;strokeColor=#82b366;",
        [ComponentKind.Database]       = "shape=mxgraph.flowchart.database;whiteSpace=wrap;html=1;fillColor=#1a1a2e;strokeColor=#555;fontColor=#fff;",
        [ComponentKind.Cache]          = "rounded=1;whiteSpace=wrap;html=1;fillColor=#ffe6cc;strokeColor=#d6b656;",
        [ComponentKind.MessageBroker]  = "rounded=1;whiteSpace=wrap;html=1;fillColor=#fff2cc;strokeColor=#d6b656;fontStyle=1;",
        [ComponentKind.ExternalSystem] = "rounded=1;whiteSpace=wrap;html=1;fillColor=#f8cecc;strokeColor=#b85450;dashed=1;",
        [ComponentKind.Client]         = "shape=mxgraph.flowchart.terminator;whiteSpace=wrap;html=1;fillColor=#e1d5e7;strokeColor=#9673a6;"
    };

    private static readonly Dictionary<RelationshipStyle, string> EdgeStyleMap = new()
    {
        [RelationshipStyle.Sync]     = "endArrow=block;endFill=1;",
        [RelationshipStyle.Async]    = "endArrow=open;endFill=0;dashed=1;",
        [RelationshipStyle.Cache]    = "endArrow=open;endFill=0;dotted=1;strokeColor=#d6b656;",
        [RelationshipStyle.Database] = "endArrow=ERmany;endFill=1;strokeColor=#555;"
    };

    public string CreateDiagram(DiagramSpec spec)
    {
        var sb = new StringBuilder();
        var settings = new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true, Encoding = Encoding.UTF8 };

        // Use scoped block so writer.Dispose() (which flushes) runs before sb.ToString()
        using (var writer = XmlWriter.Create(sb, settings))
        {

        writer.WriteStartElement("mxGraphModel");
        writer.WriteAttributeString("dx", "1422");
        writer.WriteAttributeString("dy", "762");
        writer.WriteAttributeString("grid", "1");
        writer.WriteAttributeString("gridSize", "10");
        writer.WriteAttributeString("connect", "1");
        writer.WriteAttributeString("tooltips", "1");
        writer.WriteAttributeString("arrows", "1");
        writer.WriteAttributeString("fold", "1");

        writer.WriteStartElement("root");

        // Required sentinel cells
        WriteCell(writer, "0", null, null, null, null, null, null, null, null);
        WriteCell(writer, "1", "0", null, null, null, null, null, null, null);

        // Position components in columns by kind
        var positions = ComputeLayout(spec.Components);

        foreach (var comp in spec.Components)
        {
            var (x, y) = positions[comp.Id];
            var style = StyleMap.GetValueOrDefault(comp.Kind, StyleMap[ComponentKind.Service]);
            var label = comp.Technology is null ? comp.Label : $"{comp.Label}\n({comp.Technology})";
            WriteCell(writer, comp.Id, "1", label, style, "1", null, x.ToString(), y.ToString(), null);
        }

        // Edges
        int edgeId = 1000;
        foreach (var rel in spec.Relationships)
        {
            var style = EdgeStyleMap.GetValueOrDefault(rel.Style, EdgeStyleMap[RelationshipStyle.Sync]);
            WriteEdge(writer, (edgeId++).ToString(), "1", rel.Label ?? "", style, rel.SourceId, rel.TargetId);
        }

        writer.WriteEndElement(); // root
        writer.WriteEndElement(); // mxGraphModel

        } // writer.Dispose() flushes to sb here

        return sb.ToString();
    }

    private static Dictionary<string, (int x, int y)> ComputeLayout(IReadOnlyList<DiagramComponent> components)
    {
        const int colWidth = 200;
        const int rowHeight = 100;
        const int startX = 80;
        const int startY = 80;

        var columns = new Dictionary<ComponentKind, List<string>>
        {
            [ComponentKind.Client]         = [],
            [ComponentKind.ApiGateway]     = [],
            [ComponentKind.Service]        = [],
            [ComponentKind.Database]       = [],
            [ComponentKind.Cache]          = [],
            [ComponentKind.MessageBroker]  = [],
            [ComponentKind.ExternalSystem] = []
        };

        // Column order: Client(0), Gateway(1), Service(2), Messaging(3), Data(4), External(5)
        var colIndex = new Dictionary<ComponentKind, int>
        {
            [ComponentKind.Client]         = 0,
            [ComponentKind.ApiGateway]     = 1,
            [ComponentKind.Service]        = 2,
            [ComponentKind.MessageBroker]  = 3,
            [ComponentKind.Database]       = 4,
            [ComponentKind.Cache]          = 4,
            [ComponentKind.ExternalSystem] = 5
        };

        foreach (var comp in components)
            columns[comp.Kind].Add(comp.Id);

        var result = new Dictionary<string, (int, int)>();
        var rowCounters = new Dictionary<int, int>();

        foreach (var comp in components)
        {
            var col = colIndex[comp.Kind];
            rowCounters.TryGetValue(col, out var row);
            result[comp.Id] = (startX + col * colWidth, startY + row * rowHeight);
            rowCounters[col] = row + 1;
        }

        return result;
    }

    private static void WriteCell(XmlWriter w, string id, string? parent, string? value,
        string? style, string? vertex, string? edge, string? x, string? y, string? relative)
    {
        w.WriteStartElement("mxCell");
        w.WriteAttributeString("id", id);
        if (parent is not null) w.WriteAttributeString("parent", parent);
        if (value is not null) w.WriteAttributeString("value", value);
        if (style is not null) w.WriteAttributeString("style", style);
        if (vertex is not null) w.WriteAttributeString("vertex", vertex);

        if (x is not null || y is not null)
        {
            w.WriteStartElement("mxGeometry");
            if (x is not null) w.WriteAttributeString("x", x);
            if (y is not null) w.WriteAttributeString("y", y);
            w.WriteAttributeString("width", "160");
            w.WriteAttributeString("height", "60");
            w.WriteAttributeString("as", "geometry");
            w.WriteEndElement();
        }

        w.WriteEndElement();
    }

    private static void WriteEdge(XmlWriter w, string id, string parent, string value, string style, string source, string target)
    {
        w.WriteStartElement("mxCell");
        w.WriteAttributeString("id", id);
        w.WriteAttributeString("parent", parent);
        w.WriteAttributeString("value", value);
        w.WriteAttributeString("style", style);
        w.WriteAttributeString("edge", "1");
        w.WriteAttributeString("source", source);
        w.WriteAttributeString("target", target);
        w.WriteStartElement("mxGeometry");
        w.WriteAttributeString("relative", "1");
        w.WriteAttributeString("as", "geometry");
        w.WriteEndElement();
        w.WriteEndElement();
    }
}
