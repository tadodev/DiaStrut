using DiaStrut.Core.Model;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiaStrut.Core.Geometry;

public class StrutAndTiesSimpleComponent
{
    /// <summary>
    /// Generates a planar slab grid (mesh + tie lines) in the slab’s local plane.
    /// </summary>
    public static SlabGridResult GenerateSlabSimpleGrid(
        Brep slabFaceBrep,
        IEnumerable<Point3d> controlPoints,
        UnitSystem unitSystem = UnitSystem.Metric,
        double? meshSizeOverride = null,
        bool addDiagonals = false,
        double tol = 1e-3)
    {
        // ─────────────────────────────── 0. Argument checks ───────────────────────────────
        if (slabFaceBrep is null) throw new ArgumentNullException(nameof(slabFaceBrep));
        if (controlPoints is null) throw new ArgumentNullException(nameof(controlPoints));

        if (slabFaceBrep.Faces.Count != 1)
            throw new ArgumentException("Brep must contain exactly one face.", nameof(slabFaceBrep));

        var face = slabFaceBrep.Faces[0];

        if (!face.IsPlanar(tol))
            throw new InvalidOperationException("Only planar slabs are supported.");

        // ─────────────────────────────── 1. Get slab plane & bounds ───────────────────────
        if (!face.TryGetPlane(out Plane plane, tol))
            throw new InvalidOperationException("Could not extract a plane from the face.");

        // (Optional) include slab extents so the grid always spans the full face
        var xSet = new SortedSet<double>();
        var ySet = new SortedSet<double>();

        foreach (var corner in slabFaceBrep.GetBoundingBox(true).GetCorners())           // NEW
        {
            plane.ClosestParameter(corner, out double u, out double v);
            xSet.Add(u); ySet.Add(v);
        }

        // ─────────────────────────────── 2. Collect / project control points ──────────────
        var pts = controlPoints.ToList();
        if (pts.Count < 3)
            throw new ArgumentException("At least three control points are required.", nameof(controlPoints));

        foreach (var p in pts)
        {
            plane.ClosestParameter(p, out double u, out double v);
            if (!xSet.Any(x => Math.Abs(x - u) < tol)) xSet.Add(u);
            if (!ySet.Any(y => Math.Abs(y - v) < tol)) ySet.Add(v);
        }

        // ─────────────────────────────── 3. Determine spacing ────────────────────────────
        double defaultSpacing = unitSystem == UnitSystem.Metric ? 1000.0 /* mm */
                                                                : 4.0 * 12.0 /* in */;
        double spacing = meshSizeOverride ?? defaultSpacing;

        var xGrid = BuildGridStations(xSet.ToList(), spacing, tol);
        var yGrid = BuildGridStations(ySet.ToList(), spacing, tol);

        // ─────────────────────────────── 4. Build mesh vertices ───────────────────────────
        var mesh = new Mesh();
        var index = new Dictionary<(int i, int j), int>();

        for (int i = 0; i < xGrid.Count; i++)
            for (int j = 0; j < yGrid.Count; j++)
            {
                Point3d pt3d = plane.PointAt(xGrid[i], yGrid[j]);
                index[(i, j)] = mesh.Vertices.Add(pt3d);
            }

        // ─────────────────────────────── 5. Build mesh faces ──────────────────────────────
        for (int i = 0; i < xGrid.Count - 1; i++)
            for (int j = 0; j < yGrid.Count - 1; j++)
            {
                mesh.Faces.AddFace(
                    index[(i, j)],
                    index[(i + 1, j)],
                    index[(i + 1, j + 1)],
                    index[(i, j + 1)]);
            }

        mesh.Normals.ComputeNormals();
        mesh.Compact();

        // ─────────────────────────────── 6. Strut & tie lines ─────────────────────────────
        var ortho = new List<Line>();
        var diag = new List<Line>();

        // verticals
        foreach (double u in xGrid)
            ortho.Add(new Line(plane.PointAt(u, yGrid.First()),
                               plane.PointAt(u, yGrid.Last())));

        // horizontals
        foreach (double v in yGrid)
            ortho.Add(new Line(plane.PointAt(xGrid.First(), v),
                               plane.PointAt(xGrid.Last(), v)));

        // diagonals
        if (addDiagonals)
        {
            for (int i = 0; i < xGrid.Count - 1; i++)
                for (int j = 0; j < yGrid.Count - 1; j++)
                {
                    diag.Add(new Line(plane.PointAt(xGrid[i], yGrid[j]),
                                      plane.PointAt(xGrid[i + 1], yGrid[j + 1])));

                    diag.Add(new Line(plane.PointAt(xGrid[i + 1], yGrid[j]),
                                      plane.PointAt(xGrid[i], yGrid[j + 1])));
                }
        }

        return new SlabGridResult
        {
            FloorMesh = mesh,
            OrthoLines = ortho,
            DiagonalLines = diag
        };
    }

    /// <summary>Interpolates intermediate stations between pivot values.</summary>
    private static List<double> BuildGridStations(IList<double> pivots, double spacing, double tol)
    {
        var stations = new SortedSet<double>(pivots);

        for (int i = 0; i < pivots.Count - 1; i++)
        {
            double start = pivots[i];
            double end = pivots[i + 1];

            double current = start + spacing;
            while (current < end - tol)
            {
                stations.Add(current);
                current += spacing;
            }
        }
        return stations.ToList();
    }
}


