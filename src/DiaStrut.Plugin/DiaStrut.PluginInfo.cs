using System;
using System.Drawing;
using Grasshopper;
using Grasshopper.Kernel;

namespace DiaStrut.Plugin
{
  public class DiaStrut_PluginInfo : GH_AssemblyInfo
  {
    public override string Name => "DiaStrut";

    //Return a 24x24 pixel bitmap to represent this GHA library.
    public override Bitmap Icon => null;

    //Return a short string describing the purpose of this GHA library.
    public override string Description => "";

    public override Guid Id => new Guid("67e0c48e-aa62-4f25-bd57-299583125c3b");

    //Return a string identifying you or your company.
    public override string AuthorName => "";

    //Return a string representing your preferred contact details.
    public override string AuthorContact => "";

    //Return a string representing the version.  This returns the same version as the assembly.
    public override string AssemblyVersion => GetType().Assembly.GetName().Version.ToString();
  }
}