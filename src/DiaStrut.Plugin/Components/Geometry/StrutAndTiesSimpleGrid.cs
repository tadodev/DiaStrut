using DiaStrut.Core.Geometry;
using DiaStrut.Core.Model;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace DiaStrut.Plugin.Components.Geometry
{
    public class StrutAndTiesSimpleGrid : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the StrutAndTiesSimpleGrid class.
        /// </summary>
        public StrutAndTiesSimpleGrid()
          : base("Strut And Ties Simple Grid", "SlabSimpleGrid",
              "Create slab mesh and tie lines from a surface(rectangle) + control points",
              "DiaStrut", "Geometry")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {

            pManager.AddBrepParameter("Slab Surface brep", "S",
                "Planar slab surface (trimmed boundary)", GH_ParamAccess.item);

            pManager.AddPointParameter("Control Points", "P",
                "Column / wall points on the slab", GH_ParamAccess.list);
            pManager.AddTextParameter("Unit", "U",
                "Unit system: \"Metric\" or \"US\"", GH_ParamAccess.item, "Metric");

            pManager.AddNumberParameter("Mesh Size", "m",
                "Grid spacing (leave empty to use defaults: 1000 mm or 48 in)",
                GH_ParamAccess.item, double.NaN);

            pManager.AddBooleanParameter("Add Diagonals", "D",
                "Generate diagonal ties in addition to ortho lines", GH_ParamAccess.item, true);

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {

            pManager.AddMeshParameter("Mesh", "M", "Generated slab mesh", GH_ParamAccess.item);
            pManager.AddLineParameter("Ortho", "O", "Orthogonal tie lines", GH_ParamAccess.list);
            pManager.AddLineParameter("Diag", "D", "Diagonal tie lines (optional)", GH_ParamAccess.list);

        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // ─── 1. Read & validate inputs ──────────────────────────────────────────────
            Brep slab = null;
            if (!DA.GetData(0, ref slab) || slab == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "1 Slab surface is required.");
                return;
            }

            var ctrlPts = new List<Point3d>();
            if (!DA.GetDataList(1, ctrlPts) || ctrlPts.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "At least 1 control point is required.");
                return;
            }

            string unitStr = "Metric";
            DA.GetData(2, ref unitStr);
            UnitSystem unit = unitStr.Trim().ToLower().StartsWith("u") ? UnitSystem.Imperial : UnitSystem.Metric;

            double meshSize = double.NaN;
            DA.GetData(3, ref meshSize);
            double? meshSizeNullable = double.IsNaN(meshSize) ? null : (double?)meshSize;

            bool addDiag = true;
            DA.GetData(4, ref addDiag);

            // ─── 2. Call core generator ─────────────────────────────────────────────────
            SlabGridResult result;
            try
            {
                result = StrutAndTiesSimpleComponent.GenerateSlabSimpleGrid(
                             slab, ctrlPts, unit, meshSizeNullable, addDiag);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                return;
            }

            // ─── 3. Push outputs ────────────────────────────────────────────────────────
            DA.SetData(0, result.FloorMesh);
            DA.SetDataList(1, result.OrthoLines);
            DA.SetDataList(2, result.DiagonalLines);
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
            get { return new Guid("84A37DAC-F3CB-439C-83BD-2F81FBCA62D8"); }
        }
    }
}