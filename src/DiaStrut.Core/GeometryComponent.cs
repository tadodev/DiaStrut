using Grasshopper;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace DiaStrut.Core;

public class GeometryComponent
{
    public GeometryComponent()
    {
        // Constructor logic here
    }

    public static Curve CreateSpiral(Plane plane, double r0, double r1, int turns) // CA1822: Marked as static
    {
        var l0 = new Line(plane.Origin + r0 * plane.XAxis, plane.Origin + r1 * plane.XAxis); // IDE0090: Simplified 'new' expression
        var l1 = new Line(plane.Origin - r0 * plane.XAxis, plane.Origin - r1 * plane.XAxis); // IDE0090: Simplified 'new' expression

        l0.ToNurbsCurve().DivideByCount(turns, true, out Point3d[] p0); // IDE0018: Variable declaration inlined
        l1.ToNurbsCurve().DivideByCount(turns, true, out Point3d[] p1); // IDE0018: Variable declaration inlined

        var spiral = new PolyCurve(); // IDE0090: Simplified 'new' expression

        for (int i = 0; i < p0.Length - 1; i++)
        {
            var arc0 = new Arc(p0[i], plane.YAxis, p1[i + 1]); // IDE0090: Simplified 'new' expression
            var arc1 = new Arc(p1[i + 1], -plane.YAxis, p0[i + 1]); // IDE0090: Simplified 'new' expression

            spiral.Append(arc0);
            spiral.Append(arc1);
        }

        return spiral;
    }

    public static (Brep brep, Curve outerBoundary, List<Curve> innerHoles) CreateCombinedTrimmedSurface(DataTree<Point3d> tree)
    {
        var breps = new List<Brep>();

        foreach (var branch in tree.Branches)
        {
            if (branch.Count != 4)
                throw new ArgumentException("Each branch must contain exactly 4 points.");

            var surface = NurbsSurface.CreateFromCorners(branch[0], branch[1], branch[2], branch[3]);
            if (surface == null)
                throw new InvalidOperationException("Failed to create surface from a branch.");

            breps.Add(surface.ToBrep());
        }

        double joinTol = Rhino.RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 1e-6;
        var joined = Brep.JoinBreps(breps, joinTol);
        if (joined == null || joined.Length == 0)
            throw new InvalidOperationException("Failed to join Breps.");

        var merged = joined[0];
        double angleTol = Rhino.RhinoDoc.ActiveDoc?.ModelAngleToleranceRadians ?? 0.01;
        merged.MergeCoplanarFaces(angleTol);

        if (merged.Faces.Count != 1)
            throw new InvalidOperationException("Could not merge all faces into one trimmed face.");

        var face = merged.Faces[0];
        var trimmedFace = face.DuplicateFace(false); // false = keep trimming

        // Extract loops
        Curve outerLoop = null;
        List<Curve> innerLoops = new List<Curve>();

        foreach (var loop in face.Loops)
        {
            var loopCurve = loop.To3dCurve();
            if (loop.LoopType == BrepLoopType.Outer)
                outerLoop = loopCurve;
            else if (loop.LoopType == BrepLoopType.Inner)
                innerLoops.Add(loopCurve);
        }

        return (trimmedFace, outerLoop, innerLoops);
    }

    public static DataTree<Surface> CreateSurfaceFromVertices(DataTree<Point3d> tree)
    {
        var surfaces = new DataTree<Surface>();

        for (int i = 0; i < tree.BranchCount; i++)
        {
            var branch = tree.Branch(i);
            if (branch.Count != 4)
                throw new ArgumentException("Each branch must contain exactly 4 points.");

            var surface = NurbsSurface.CreateFromCorners(branch[0], branch[1], branch[2], branch[3]);
            if (surface == null)
                throw new InvalidOperationException("Failed to create surface from branch.");

            var path = tree.Path(i);
            surfaces.Add(surface, path);
        }

        return surfaces;
    }
}
