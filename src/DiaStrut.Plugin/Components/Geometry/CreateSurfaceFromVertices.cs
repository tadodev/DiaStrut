using DiaStrut.Core;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace DiaStrut.Plugin.Components.Geometry
{
    public class CreateSurfaceFromVertices : GH_Component
    {
        public CreateSurfaceFromVertices()
          : base("CreateSurfaceFromVertices", "SFV",
              "Create surfaces from a tree of surface vertex points (4 per surface)",
              "DiaStrut", "Geometry")
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Vertices", "V",
                "Tree of surface corner points (4 points per branch)", GH_ParamAccess.tree);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddSurfaceParameter("Surfaces", "S",
                "Surfaces from vertices", GH_ParamAccess.tree);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<GH_Point> ghTree;
            if (!DA.GetDataTree(0, out ghTree)) return;

            var tree = new DataTree<Point3d>();
            foreach (GH_Path path in ghTree.Paths)
            {
                var pts = ghTree.get_Branch(path);
                var branch = new List<Point3d>();
                foreach (var pt in pts)
                {
                    if (pt is GH_Point ghPt)
                        branch.Add(ghPt.Value);
                }
                tree.AddRange(branch, path);
            }

            try
            {
                var surfaces = GeometryComponent.CreateSurfaceFromVertices(tree);
                DA.SetDataTree(0, surfaces);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        protected override System.Drawing.Bitmap Icon => null;

        public override Guid ComponentGuid => new Guid("3AF9C4D7-69B4-424A-B3A7-CD49738AD3F4");
    }
}
