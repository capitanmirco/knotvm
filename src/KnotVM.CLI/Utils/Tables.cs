using Spectre.Console;

namespace KnotVM.CLI.Utils;

public static class Tables
{
    public static Table CreateSpectreTable(string[] headerStringValues)
    {
        var table = new Table().Border(TableBorder.Rounded);
        foreach (var header in headerStringValues)
            table.AddColumn(new TableColumn($"[bold]{header}[/]").Centered());
        return table;
    }

    public static Table AddHeaderColumn(this Table table, string headerString)
    {
        table.AddColumn(new TableColumn($"[bold]{headerString}[/]").Centered());
        return table;
    }

    public static Table AddContentRow(this Table table, string[] rowStringValues, Func<string, string>? formatter)
    {
        var formatted = rowStringValues.Select(v => formatter?.Invoke(v) ?? v).Take(table.Columns.Count).ToArray();
        table.AddRow(formatted);
        return table;
    }
}