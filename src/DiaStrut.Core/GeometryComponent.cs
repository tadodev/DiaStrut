using Rhino.Geometry;

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

}
