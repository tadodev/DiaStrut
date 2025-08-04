using DiaStrut.Core.Model;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DiaStrut.Core.Geometry;

public class StrutAndTiesSimpleComponent
{ /// <summary>
  /// Generates a uniform orthogonal (and optional diagonal) grid on a planar, trimmed slab face.
  /// Pass in the Brep returned by CreateCombinedTrimmedSurface().
  /// </summary>
  /// <param name="slabFaceBrep">Single-face Brep with outer + inner loops.</param>
  /// <param name="controlPoints">Column / wall points (currently not used for snapping).</param>
  /// <param name="unit">Unit system (Metric = mm, Imperial = in).</param>
  /// <param name="targetSpacing">Desired spacing (null → 1000 mm / 48 in).</param>
  /// <param name="addDiagonals">Add diagonal ties inside every cell.</param>
  /// <param name="tol">Model tolerance.</param>
    public static SlabGridResult GenerateSlabGrid(
        Brep slabFaceBrep,
        IEnumerable<Point3d> controlPoints,
        UnitSystem unit = UnitSystem.Metric,
        double? targetSpacing = null,
        bool addDiagonals = false,
        double tol = 1e-4)
    {
        // ─── Guard clauses ────────────────────────────────────────────────
        if (slabFaceBrep == null) throw new ArgumentNullException(nameof(slabFaceBrep));
        if (controlPoints == null) throw new ArgumentNullException(nameof(controlPoints));
        if (slabFaceBrep.Faces.Count != 1)
            throw new InvalidOperationException("Input Brep must contain exactly one face.");

        var face = slabFaceBrep.Faces[0];
        if (!face.IsPlanar(tol))
            throw new InvalidOperationException("Slab face must be planar.");

        // Keep trims
        Brep brep = face.DuplicateFace(false);    // includes holes
        Surface surface = face.UnderlyingSurface();

        // ─── Even spacing ────────────────────────────────────────────────
        double defaultStep = unit == UnitSystem.Metric ? 1000.0 : 48.0;
        double step = targetSpacing ?? defaultStep;

        Interval uDom = surface.Domain(0);
        Interval vDom = surface.Domain(1);

        int nu = Math.Max(1, (int)Math.Round(uDom.Length / step));
        int nv = Math.Max(1, (int)Math.Round(vDom.Length / step));

        double du = uDom.Length / nu;
        double dv = vDom.Length / nv;

        var uSta = Enumerable.Range(0, nu + 1).Select(i => uDom.T0 + i * du).ToList();
        var vSta = Enumerable.Range(0, nv + 1).Select(j => vDom.T0 + j * dv).ToList();

        // ─── Ortho lines (clipped) ───────────────────────────────────────
        var ortho = new List<Line>();

        foreach (double u in uSta)
            ortho.AddRange(ClipLineWithBrep(
                new Line(surface.PointAt(u, vDom.T0),
                         surface.PointAt(u, vDom.T1)), brep, tol));

        foreach (double v in vSta)
            ortho.AddRange(ClipLineWithBrep(
                new Line(surface.PointAt(uDom.T0, v),
                         surface.PointAt(uDom.T1, v)), brep, tol));

        // ─── Diagonals (optional) ────────────────────────────────────────
        var diag = new List<Line>();
        if (addDiagonals)
        {
            for (int i = 0; i < nu; i++)
                for (int j = 0; j < nv; j++)
                {
                    double u0 = uDom.T0 + i * du;
                    double u1 = u0 + du;
                    double v0 = vDom.T0 + j * dv;
                    double v1 = v0 + dv;

                    diag.AddRange(ClipLineWithBrep(
                        new Line(surface.PointAt(u0, v0),
                                 surface.PointAt(u1, v1)), brep, tol));

                    diag.AddRange(ClipLineWithBrep(
                        new Line(surface.PointAt(u1, v0),
                                 surface.PointAt(u0, v1)), brep, tol));
                }
        }

        // ─── Mesh (only quads fully inside Brep) ─────────────────────────
        var mesh = new Mesh();
        var nodeIndex = new Dictionary<(int, int), int>();

        for (int i = 0; i <= nu; i++)
            for (int j = 0; j <= nv; j++)
            {
                Point3d p = surface.PointAt(uSta[i], vSta[j]);
                if (IsInsideOrOn(brep, p, tol))
                    nodeIndex[(i, j)] = mesh.Vertices.Add(p);
            }

        for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
            {
                var a = (i, j);
                var b = (i + 1, j);
                var c = (i + 1, j + 1);
                var d = (i, j + 1);

                if (nodeIndex.ContainsKey(a) && nodeIndex.ContainsKey(b) &&
                    nodeIndex.ContainsKey(c) && nodeIndex.ContainsKey(d))
                {
                    mesh.Faces.AddFace(
                        nodeIndex[a], nodeIndex[b], nodeIndex[c], nodeIndex[d]);
                }
            }

        mesh.Normals.ComputeNormals();
        mesh.Compact();

        return new SlabGridResult
        {
            FloorMesh = mesh,
            OrthoLines = ortho,
            DiagonalLines = diag
        };
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helper: clip a line against a trimmed Brep and keep only “inside” bits
    // ─────────────────────────────────────────────────────────────────────
    private static IEnumerable<Line> ClipLineWithBrep(Line line, Brep brep, double tol)
    {
        var lc = new LineCurve(line);

        Curve[] overlap;
        Point3d[] xPts;
        Intersection.CurveBrep(lc, brep, tol, out overlap, out xPts);

        var tVals = xPts
            .Select(pt => { lc.ClosestPoint(pt, out double t); return t; })
            .Concat(new[] { 0.0, 1.0 })
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        var segments = new List<Line>();
        for (int i = 0; i < tVals.Count - 1; i++)
        {
            double t0 = tVals[i], t1 = tVals[i + 1];
            if (t1 - t0 < tol) continue;

            var seg = new Line(lc.PointAt(t0), lc.PointAt(t1));
            if (brep.IsPointInside(seg.PointAt(0.5), tol, false))
                segments.Add(seg);
        }
        return segments;
    }

    private static bool IsInsideOrOn(Brep brep, Point3d pt, double tol)
    {
        // strictly inside?
        if (brep.IsPointInside(pt, tol, true))
            return true;

        // on boundary?  use ClosestPoint
        Point3d cp;
        ComponentIndex ci;
        double s, t;
        Vector3d n;

        bool ok = brep.ClosestPoint(
            pt, out cp, out ci, out s, out t,
            tol,          // maximumDistance to search
            out n);

        // If a closest point was found within 'tol', treat it as “on”
        return ok && pt.DistanceTo(cp) <= tol;
    }

}





