using System;
using System.IO;
using Grasshopper.Kernel;
using GH_MCP.Utils;
using Rhino;

namespace GH_MCP
{
    /// <summary>
    /// Grasshopper component that investigates script components via reflection.
    /// Connect any script component to the input to generate a detailed structure report.
    /// </summary>
    public class GH_ScriptInvestigatorComponent : GH_Component
    {
        public GH_ScriptInvestigatorComponent()
            : base("Script Investigator",
                   "ScriptInv",
                   "Investigates a script component's structure via reflection and writes a detailed report",
                   "Params",
                   "Util")
        {
        }

        public override Guid ComponentGuid => new Guid("F8A3C2D1-4E5F-4A8B-9C1D-2E3F4A5B6C7D");

        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Component", "C", "Script component to investigate (connect any script component here)", GH_ParamAccess.item);
        }

        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Report", "R", "Investigation report text", GH_ParamAccess.item);
            pManager.AddTextParameter("Report File", "F", "Path to saved report file", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Success", "S", "True if investigation completed successfully", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            object component = null;

            if (!DA.GetData(0, ref component))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "No component connected to input");
                return;
            }

            if (component == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input component is null");
                return;
            }

            try
            {
                // Get the actual component object
                var ghComponent = component as IGH_DocumentObject;
                if (ghComponent == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input is not a Grasshopper component");
                    DA.SetData(2, false);
                    return;
                }

                // Create investigator and run investigation
                var investigator = new ReflectionInvestigator();
                string report = investigator.InvestigateComponent(ghComponent);

                // Generate unique filename with component ID and timestamp
                var componentId = ghComponent.InstanceGuid.ToString();
                var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                var fileName = $"script-investigation-{timestamp}-{componentId.Substring(0, 8)}.txt";
                var reportPath = Path.Combine(GrasshopperMCP.GH_MCPInfo.GhaFolder, fileName);

                // Write report to file
                File.WriteAllText(reportPath, report);

                // Log to Rhino command line
                RhinoApp.WriteLine($"GH_MCP: Investigation complete. Report saved to:");
                RhinoApp.WriteLine($"  {reportPath}");

                // Output results
                DA.SetData(0, report);
                DA.SetData(1, reportPath);
                DA.SetData(2, true);

                // Set component message to show file location
                Message = $"Report saved:\n{fileName}";
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, $"Investigation failed: {ex.Message}");
                DA.SetData(2, false);
            }
        }

        protected override System.Drawing.Bitmap Icon => null;
    }
}
