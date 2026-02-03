using Grasshopper;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Rhino;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WiseOwlChat;

namespace ChatGHGPT.GH
{
    public class ChattyMenu : GH_AssemblyPriority
    {
        private static List<ToolStripMenuItem> menuItems;

        public override GH_LoadingInstruction PriorityLoad()
        {
            // Register menu creation when Grasshopper's canvas is created
            Instances.CanvasCreated += RegisterMenuItems;
            return GH_LoadingInstruction.Proceed;
        }

        private void RegisterMenuItems(GH_Canvas canvas)
        {
            // Ensure it runs only once
            Instances.CanvasCreated -= RegisterMenuItems;

            GH_DocumentEditor documentEditor = Instances.DocumentEditor;
            if (documentEditor == null) return;

            CreateMenu(documentEditor, canvas);
        }

        private static void CreateMenu(GH_DocumentEditor editor, GH_Canvas canvas)
        {
            if (editor == null) return;

            GH_MenuStrip ghMenuStrip = null;
            foreach (Control control in editor.Controls)
            {
                if (control is GH_MenuStrip menu)
                {
                    ghMenuStrip = menu;
                    break;
                }
            }

            if (ghMenuStrip == null) return;

            // Create "Chatty" top-level menu
            ToolStripMenuItem chattyMenu = new ToolStripMenuItem("Chatty");

            // Define menu items
            SetMenuItems();

            // Add items to the "Chatty" menu
            chattyMenu.DropDownItems.AddRange(menuItems.ToArray());

            // Add the "Chatty" menu to the main menu strip
            ghMenuStrip.Items.Add(chattyMenu);
        }

        private static void SetMenuItems()
        {
            menuItems = new List<ToolStripMenuItem>();

            ToolStripMenuItem editScriptItem = new ToolStripMenuItem("Edit script with chat");
            editScriptItem.Click += (s, e) => EditScriptWithChat();

            ToolStripMenuItem parseClipboardItem = new ToolStripMenuItem("Parse clipboard to script");
            parseClipboardItem.Click += (s, e) => ParseClipboardToScript();

            ToolStripMenuItem apiSettingsItem = new ToolStripMenuItem("API settings");
            apiSettingsItem.Click += (s, e) => OpenAPISettings();

            // Add to the menu item list
            menuItems.Add(editScriptItem);
            menuItems.Add(parseClipboardItem);
            menuItems.Add(apiSettingsItem);
        }

        // Placeholder methods for future functionality
        private static void EditScriptWithChat()
        {
            var sel = GetSelectedComponent.GetSelectedComponents().FirstOrDefault();
            MessageBox.Show("Edit script with chat clicked!" + sel.GetType(), "Chatty");
            var parser = new ParseToCSharpComponent(sel);

            
        }

        private static void ParseClipboardToScript()
        {
            MessageBox.Show("Parse clipboard to script clicked!", "Chatty");
            WiseOwlChat.App app = new WiseOwlChat.App();
        }

        private static void OpenAPISettings()
        {
            MessageBox.Show("API settings clicked!", "Chatty");
        }
    }

    public class GetSelectedComponent
    {
        public static List<GH_Component> GetSelectedComponents()
        {
            // Get the active Grasshopper document
            var doc = Grasshopper.Instances.ActiveCanvas?.Document;
            if (doc == null)
            {
                RhinoApp.WriteLine("No active Grasshopper document found.");
                return new List<GH_Component>();
            }

            // Get selected objects and filter only GH_Component
            List<GH_Component> selectedComponents = doc.SelectedObjects()
                .OfType<GH_Component>()
                .ToList();

            if (selectedComponents.Count == 0)
            {
                RhinoApp.WriteLine("No components are currently selected.");
            }
            else
            {
                foreach (var comp in selectedComponents)
                {
                    RhinoApp.WriteLine($"Selected Component: {comp.Name}");
                }
            }

            return selectedComponents;
        }
    }
}
