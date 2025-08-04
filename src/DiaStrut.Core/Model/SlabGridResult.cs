using Rhino.Geometry;
using System.Collections.Generic;

namespace DiaStrut.Core.Model;

public sealed class SlabGridResult
{
    public Mesh FloorMesh { get; init; }
    public List<Line> OrthoLines { get; init; }
    public List<Line> DiagonalLines { get; init; } = new();
}

