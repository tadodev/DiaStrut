using DiaStrut.Core.Model;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiaStrut.Core.Geometry;

public class StrutAndTiesSimpleComponent
{

    public static SlabGridResult GenerateSlabGrid(
        Brep slabFaceBrep,
        IEnumerable<Point3d> controlPoints,
        UnitSystem unit = UnitSystem.Metric,
        double? targetSpacing = null,
        bool addDiagonals = false,
        double tol = 1e-4)
    {
        if (slabFaceBrep == null)
            throw new ArgumentNullException(nameof(slabFaceBrep));
        if (controlPoints == null)
            throw new ArgumentNullException(nameof(controlPoints));

        var controlPointsList = controlPoints.ToList();

        if (slabFaceBrep.Faces.Count != 1)
            throw new InvalidOperationException("Slab brep must have exactly 1 face.");

        var face = slabFaceBrep.Faces[0];
        if (!face.IsPlanar(tol))
        {
            if (face.TryGetPlane(out Plane _, tol * 10))
                throw new InvalidOperationException("Slab face is not planar within tolerance.");
            else
                throw new InvalidOperationException("Slab face is not planar.");
        }

        var surface = face.UnderlyingSurface();
        if (surface == null)
            throw new InvalidOperationException("Could not get surface from face.");

        Brep brep = face.DuplicateFace(false);
        double step = targetSpacing ?? (unit == UnitSystem.Metric ? 1000.0 : 48.0);
        if (step <= 0)
            throw new ArgumentException("Invalid spacing.");

        Interval uDom = surface.Domain(0);
        Interval vDom = surface.Domain(1);

        // ═══ STEP 1: Process control points and get their UV coordinates ═══
        var validControlPoints = new List<(Point3d worldPt, Point3d surfacePt, double u, double v)>();

        foreach (var ctrlPt in controlPointsList)
        {
            if (face.ClosestPoint(ctrlPt, out double u, out double v))
            {
                Point3d surfacePt = surface.PointAt(u, v);

                // Project control point to surface plane if needed
                if (face.TryGetPlane(out Plane slabPlane, tol))
                {
                    Point3d projectedPt = slabPlane.ClosestPoint(ctrlPt);
                    Point3d finalPt = projectedPt.DistanceTo(ctrlPt) < step * 0.2 ? projectedPt : surfacePt;

                    if (IsInsideOrOnBoundary(brep, surfacePt, tol * 5))
                    {
                        validControlPoints.Add((ctrlPt, finalPt, u, v));
                    }
                }
                else if (IsInsideOrOnBoundary(brep, surfacePt, tol))
                {
                    validControlPoints.Add((ctrlPt, surfacePt, u, v));
                }
            }
        }

        // ═══ STEP 2: Create adaptive grid stations including control points ═══
        int nu = Math.Max(1, (int)Math.Round(uDom.Length / step));
        int nv = Math.Max(1, (int)Math.Round(vDom.Length / step));
        nu = Math.Min(nu, 200);
        nv = Math.Min(nv, 200);

        double du = uDom.Length / nu;
        double dv = vDom.Length / nv;

        // Start with regular grid stations
        var uStationsSet = new SortedSet<double>();
        var vStationsSet = new SortedSet<double>();

        // Add regular stations
        for (int i = 0; i <= nu; i++)
            uStationsSet.Add(uDom.T0 + i * du);
        for (int j = 0; j <= nv; j++)
            vStationsSet.Add(vDom.T0 + j * dv);

        // Add control point stations
        double snapTolerance = Math.Min(du, dv) * 0.4; // 40% of grid spacing

        foreach (var (worldPt, surfacePt, u, v) in validControlPoints)
        {
            // Check if we should snap to existing station or add new one
            var closestU = uStationsSet.OrderBy(us => Math.Abs(us - u)).First();
            var closestV = vStationsSet.OrderBy(vs => Math.Abs(vs - v)).First();

            if (Math.Abs(closestU - u) > snapTolerance)
                uStationsSet.Add(u);
            if (Math.Abs(closestV - v) > snapTolerance)
                vStationsSet.Add(v);
        }

        var uStations = uStationsSet.ToList();
        var vStations = vStationsSet.ToList();

        // ═══ STEP 3: Generate grid lines with control point integration ═══
        var orthoLines = new List<Line>();

        // U-direction lines
        foreach (double u in uStations)
        {
            var line = new Line(surface.PointAt(u, vDom.T0), surface.PointAt(u, vDom.T1));
            orthoLines.AddRange(ClipLineWithBrep(line, brep, tol));
        }

        // V-direction lines
        foreach (double v in vStations)
        {
            var line = new Line(surface.PointAt(uDom.T0, v), surface.PointAt(uDom.T1, v));
            orthoLines.AddRange(ClipLineWithBrep(line, brep, tol));
        }

        // ═══ STEP 4: Add control point connections ═══
        var controlPointConnections = new List<Line>();
        var controlPointNodes = new List<Point3d>();

        foreach (var (worldPt, surfacePt, u, v) in validControlPoints)
        {
            controlPointNodes.Add(surfacePt);

            // Find nearby grid intersection points
            double connectionRadius = step * 0.8;
            var nearbyGridPoints = new List<Point3d>();

            foreach (var uStn in uStations)
            {
                foreach (var vStn in vStations)
                {
                    Point3d gridPt = surface.PointAt(uStn, vStn);
                    if (IsInsideOrOnBoundary(brep, gridPt, tol))
                    {
                        double distance = surfacePt.DistanceTo(gridPt);
                        if (distance <= connectionRadius && distance > tol * 10)
                        {
                            nearbyGridPoints.Add(gridPt);
                        }
                    }
                }
            }

            // Connect to closest grid points (max 4 connections)
            var sortedGridPoints = nearbyGridPoints
                .OrderBy(pt => surfacePt.DistanceTo(pt))
                .Take(4)
                .ToList();

            foreach (var gridPt in sortedGridPoints)
            {
                var connectionLine = new Line(surfacePt, gridPt);

                // Verify connection doesn't cross holes
                var clippedConnections = ClipLineWithBrep(connectionLine, brep, tol);
                foreach (var clippedLine in clippedConnections)
                {
                    // Only add if connection is mostly intact (not interrupted by holes)
                    if (clippedLine.Length > connectionLine.Length * 0.8)
                    {
                        controlPointConnections.Add(clippedLine);
                    }
                }
            }
        }

        // Add connections to orthogonal lines
        orthoLines.AddRange(controlPointConnections);

        // ═══ STEP 5: Generate diagonal lines (avoid holes and control point conflicts) ═══
        var diagonalLines = new List<Line>();
        if (addDiagonals)
        {
            for (int i = 0; i < uStations.Count - 1; i++)
            {
                for (int j = 0; j < vStations.Count - 1; j++)
                {
                    double u0 = uStations[i];
                    double u1 = uStations[i + 1];
                    double v0 = vStations[j];
                    double v1 = vStations[j + 1];

                    // Check if cell center is inside (avoid diagonals across holes)
                    Point3d cellCenter = surface.PointAt((u0 + u1) / 2, (v0 + v1) / 2);
                    if (IsInsideOrOnBoundary(brep, cellCenter, tol))
                    {
                        var diag1 = new Line(surface.PointAt(u0, v0), surface.PointAt(u1, v1));
                        var diag2 = new Line(surface.PointAt(u1, v0), surface.PointAt(u0, v1));

                        diagonalLines.AddRange(ClipLineWithBrep(diag1, brep, tol));
                        diagonalLines.AddRange(ClipLineWithBrep(diag2, brep, tol));
                    }
                }
            }
        }

        // ═══ STEP 6: Generate mesh with control points integrated ═══
        var mesh = new Mesh();
        var nodeIndex = new Dictionary<(int, int), int>();

        // Add control points first
        var controlPointIndices = new Dictionary<Point3d, int>();
        foreach (var ctrlPt in controlPointNodes)
        {
            int index = mesh.Vertices.Add(ctrlPt);
            controlPointIndices[ctrlPt] = index;
        }

        // Add grid vertices (avoiding duplicates with control points)
        for (int i = 0; i < uStations.Count; i++)
        {
            for (int j = 0; j < vStations.Count; j++)
            {
                var pt = surface.PointAt(uStations[i], vStations[j]);
                if (IsInsideOrOnBoundary(brep, pt, tol))
                {
                    // Check if too close to any control point
                    bool tooCloseToControl = controlPointNodes.Any(cp => cp.DistanceTo(pt) < tol * 20);

                    if (!tooCloseToControl)
                    {
                        nodeIndex[(i, j)] = mesh.Vertices.Add(pt);
                    }
                }
            }
        }

        // Create mesh faces (only for cells that are fully inside)
        for (int i = 0; i < uStations.Count - 1; i++)
        {
            for (int j = 0; j < vStations.Count - 1; j++)
            {
                var keys = new[] { (i, j), (i + 1, j), (i + 1, j + 1), (i, j + 1) };

                if (keys.All(k => nodeIndex.ContainsKey(k)))
                {
                    // Additional check: ensure cell center is inside (avoid faces across holes)
                    double uCenter = (uStations[i] + uStations[i + 1]) / 2;
                    double vCenter = (vStations[j] + vStations[j + 1]) / 2;
                    Point3d cellCenter = surface.PointAt(uCenter, vCenter);

                    if (IsInsideOrOnBoundary(brep, cellCenter, tol))
                    {
                        mesh.Faces.AddFace(
                            nodeIndex[keys[0]],
                            nodeIndex[keys[1]],
                            nodeIndex[keys[2]],
                            nodeIndex[keys[3]]
                        );
                    }
                }
            }
        }

        mesh.Normals.ComputeNormals();
        mesh.Compact();

        return new SlabGridResult
        {
            FloorMesh = mesh,
            OrthoLines = orthoLines,
            DiagonalLines = diagonalLines,
        };
    }

    private static IEnumerable<Line> ClipLineWithBrep(Line line, Brep brep, double tol)
    {
        if (line.Length < tol) return Enumerable.Empty<Line>();

        int sampleCount = Math.Max(20, (int)(line.Length / (tol * 50)));
        var segments = new List<Line>();
        var current = new List<Point3d>();

        for (int i = 0; i <= sampleCount; i++)
        {
            var pt = line.PointAt((double)i / sampleCount);
            if (IsInsideOrOnBoundary(brep, pt, tol))
                current.Add(pt);
            else
            {
                if (current.Count >= 2)
                    segments.Add(new Line(current.First(), current.Last()));
                current.Clear();
            }
        }
        if (current.Count >= 2)
            segments.Add(new Line(current.First(), current.Last()));

        return segments;
    }

    private static bool IsInsideOrOnBoundary(Brep brep, Point3d pt, double tol)
    {
        try
        {
            if (brep.IsPointInside(pt, tol, true)) return true;

            bool foundClosest = brep.ClosestPoint(pt, out Point3d cp, out _, out _, out _, tol * 5, out _);
            return foundClosest && pt.DistanceTo(cp) <= tol * 2;
        }
        catch
        {
            return false;
        }
    }
}





