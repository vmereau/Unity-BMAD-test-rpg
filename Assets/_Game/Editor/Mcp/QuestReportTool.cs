using System;
using Game.Editor.QuestExplorer;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace Game.Editor.Mcp
{
    /// <summary>
    /// MCP custom tool exposing <see cref="QuestReport"/> to Claude. Only compiles when the Unity MCP
    /// package is installed (asmdef version define <c>GAME_UNITY_MCP</c>).
    /// </summary>
    [McpForUnityTool("quest_report",
        Description = "Quest Explorer data as Markdown: quests, their parts and facts, who sets/reads each fact, " +
                      "involved NPC memories, gated choices and validator issues (V1-V17). " + QuestReport.USAGE)]
    public static class QuestReportTool
    {
        public class Parameters
        {
            [ToolParameter("'list', 'audit', a quest (questId / asset name / title), a fact asset name or a memory asset name. Default: list.",
                Required = false, DefaultValue = "list")]
            public string target { get; set; }
        }

        public static object HandleCommand(JObject @params)
        {
            string target = @params?["target"]?.ToString();
            try
            {
                return new SuccessResponse($"Quest report: {(string.IsNullOrEmpty(target) ? "list" : target)}",
                    new { report = QuestReport.Run(target) });
            }
            catch (Exception e)
            {
                return new ErrorResponse($"quest_report failed: {e.Message}");
            }
        }
    }
}
