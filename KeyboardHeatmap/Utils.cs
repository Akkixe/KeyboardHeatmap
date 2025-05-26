namespace KeyboardHeatmap;

public class Utils
{
    public static string GetActionName(uint actionId)
    {
        return Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>().GetRow(actionId).Name.ExtractText();
    }
}
