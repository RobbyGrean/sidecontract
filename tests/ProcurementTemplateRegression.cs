using System;
using System.Collections.Generic;
using System.IO;
using Sidecontract;

internal static class ProcurementTemplateRegression
{
    private static int Main(string[] args)
    {
        try
        {
            var count = 0;
            foreach (var source in Directory.GetFiles(args[0], "*.docx", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(source).StartsWith("~$")) continue;
                var values = new Dictionary<string, string>();
                foreach (var tag in SidecontractDocxRenderer.FindBusinessTags(source))
                    values[tag] = tag.Contains("หน้า") ? "4" : "ข้อมูลทดสอบ";
                var relative = source.Substring(args[0].TrimEnd(Path.DirectorySeparatorChar).Length + 1);
                var output = SidecontractDocxRenderer.Render(source, Path.Combine(args[1], relative), values);
                if (SidecontractDocxRenderer.FindBusinessTags(output).Count != 0) throw new Exception("Unfilled tags in " + relative);
                count++;
            }
            if (count != 46) throw new Exception("Unexpected Procurement template count: " + count);
            Console.WriteLine("PASS generated all 46 Procurement documents without remaining template tags.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
