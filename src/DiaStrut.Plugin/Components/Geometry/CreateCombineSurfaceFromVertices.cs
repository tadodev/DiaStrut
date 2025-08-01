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
    public class CreateCombineSurfaceFromVertices : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CreateCombineSurfaceFromVertices class.
        /// </summary>
        public CreateCombineSurfaceFromVertices()
          : base("CreateCombineSurfaceFromVertices", "CFV",
              "Create a combined surface from a tree of surface vertex points (4 per surface)",
              "DiaStrut", "Geometry")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddPointParameter("Vertices", "V",
                "Tree of surface corner points (4 points per branch)", GH_ParamAccess.tree);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddSurfaceParameter("Surface", "S",
                "Combined surface from vertices", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Structure<GH_Point> ghTree;
            if (!DA.GetDataTree(0, out ghTree)) return;

            // Convert GH_Structure<GH_Point> to DataTree<Point3d>
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
                var surface = GeometryComponent.CreateCombineSurfaceFromVertices(tree);
                DA.SetData(0, surface);
            }
            catch (Exception ex)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("86F550A8-8B5A-403A-9600-7ACF5091532A"); }
        }
    }
}