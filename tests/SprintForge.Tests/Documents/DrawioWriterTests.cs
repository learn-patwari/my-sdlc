using FluentAssertions;
using SprintForge.Application.Documents;
using SprintForge.Infrastructure.Documents;
using System.Xml;
using Xunit;

namespace SprintForge.Tests.Documents;

public sealed class DrawioWriterTests
{
    private readonly DrawioWriter _writer = new();

    private static DiagramSpec SimpleSpec() => new()
    {
        Title = "Test Diagram",
        Components =
        [
            new DiagramComponent { Id = "gw", Label = "API Gateway", Kind = ComponentKind.ApiGateway },
            new DiagramComponent { Id = "svc", Label = "Payment Service", Kind = ComponentKind.Service },
            new DiagramComponent { Id = "db", Label = "PostgreSQL", Kind = ComponentKind.Database }
        ],
        Relationships =
        [
            new DiagramRelationship { SourceId = "gw", TargetId = "svc", Label = "REST", Style = RelationshipStyle.Sync },
            new DiagramRelationship { SourceId = "svc", TargetId = "db", Style = RelationshipStyle.Database }
        ]
    };

    [Fact]
    public void CreateDiagram_ReturnsValidXml()
    {
        var xml = _writer.CreateDiagram(SimpleSpec());

        var doc = new XmlDocument();
        var act = () => doc.LoadXml(xml);
        act.Should().NotThrow("the output must be valid XML");
    }

    [Fact]
    public void CreateDiagram_ContainsMxGraphModelRoot()
    {
        var xml = _writer.CreateDiagram(SimpleSpec());

        xml.Should().Contain("<mxGraphModel");
        xml.Should().Contain("<root>");
    }

    [Fact]
    public void CreateDiagram_ContainsAllComponents()
    {
        var xml = _writer.CreateDiagram(SimpleSpec());

        xml.Should().Contain("API Gateway");
        xml.Should().Contain("Payment Service");
        xml.Should().Contain("PostgreSQL");
    }

    [Fact]
    public void CreateDiagram_ContainsEdgeWithLabel()
    {
        var xml = _writer.CreateDiagram(SimpleSpec());

        xml.Should().Contain("REST");
        xml.Should().Contain("edge=\"1\"");
    }

    [Fact]
    public void CreateDiagram_WithNoComponents_ReturnsValidEmptyDiagram()
    {
        var spec = new DiagramSpec { Title = "Empty" };
        var xml = _writer.CreateDiagram(spec);

        var doc = new XmlDocument();
        var act = () => doc.LoadXml(xml);
        act.Should().NotThrow();
    }

    [Fact]
    public void CreateDiagram_ContainsSentinelCells()
    {
        var xml = _writer.CreateDiagram(SimpleSpec());

        // mxGraph requires cells with id="0" and id="1" as the root sentinel cells
        xml.Should().Contain("id=\"0\"");
        xml.Should().Contain("id=\"1\"");
    }

    [Fact]
    public void CreateDiagram_AsyncRelationship_HasDashedStyle()
    {
        var spec = new DiagramSpec
        {
            Title = "Async Test",
            Components =
            [
                new DiagramComponent { Id = "a", Label = "A", Kind = ComponentKind.Service },
                new DiagramComponent { Id = "b", Label = "Kafka", Kind = ComponentKind.MessageBroker }
            ],
            Relationships =
            [
                new DiagramRelationship { SourceId = "a", TargetId = "b", Style = RelationshipStyle.Async }
            ]
        };

        var xml = _writer.CreateDiagram(spec);

        xml.Should().Contain("dashed=1");
    }

    [Fact]
    public void CreateDiagram_WithTechnologyHint_IncludesItInLabel()
    {
        var spec = new DiagramSpec
        {
            Title = "Tech Test",
            Components =
            [
                new DiagramComponent { Id = "db", Label = "User Store", Kind = ComponentKind.Database, Technology = "PostgreSQL 15" }
            ]
        };

        var xml = _writer.CreateDiagram(spec);

        xml.Should().Contain("PostgreSQL 15");
    }
}
