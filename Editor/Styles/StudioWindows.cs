using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace CG.CombatFramework.Editor
{
    internal static class StudioWindows
    {
        public const string k_Menu = "Tools/CG Combat Framework/";

        public static ToolbarMenu Menu()
        {
            var tools = new ToolbarMenu { text = "Tools" };
            tools.AddToClassList("cg-toolbar__tools");
            tools.menu.AppendAction("Combat Studio", _ => CombatStudioWindow.Open());
            tools.menu.AppendAction("Unit Creator", _ => UnitCreatorWindow.Open());
            tools.menu.AppendAction("Pipeline Tester", _ => PipelineTesterWindow.Open());
            tools.menu.AppendAction("Database", _ => CombatDatabaseWindow.Open());
            tools.menu.AppendSeparator();
            tools.menu.AppendAction("Validate Definition IDs", _ => DefinitionIdValidator.Validate());
            return tools;
        }
    }
}
