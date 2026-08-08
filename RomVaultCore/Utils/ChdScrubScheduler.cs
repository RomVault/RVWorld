using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RomVaultCore.Utils;

public sealed class ChdScrubPlanItem
{
    public string Path { get; set; } = "";
    public int RiskScore { get; set; }
    public string Reason { get; set; } = "";
}

public static class ChdScrubScheduler
{
    public static int Plan(string root, int maxAgeDays, int limit, out List<ChdScrubPlanItem> plan, out string report)
    {
        plan = new List<ChdScrubPlanItem>();
        if (!Directory.Exists(root))
        {
            report = "CHD scrub root was not found.";
            return 2;
        }
        if (maxAgeDays < 1) maxAgeDays = 1;
        if (limit < 1) limit = int.MaxValue;
        try
        {
            long now = DateTime.UtcNow.Ticks;
            foreach (string file in Directory.GetFiles(root, "*.chd", SearchOption.AllDirectories))
            {
                ChdHealthRecord record = ChdHealthStore.FindRecord(file);
                int risk;
                string reason;
                if (record == null)
                {
                    risk = 10000;
                    reason = "never externally verified";
                }
                else if (!record.ParityPassed)
                {
                    risk = 20000 + Math.Min(5000, record.ConsecutiveFailures * 250);
                    reason = "previous verification failed";
                }
                else if (!ChdHealthStore.HasCurrentExternalParity(file, maxAgeDays))
                {
                    long ageTicks = Math.Max(0, now - record.CheckedUtcTicks);
                    risk = 5000 + (int)Math.Min(4000, TimeSpan.FromTicks(ageTicks).TotalDays);
                    reason = "parity expired or container identity changed";
                }
                else
                {
                    continue;
                }
                plan.Add(new ChdScrubPlanItem { Path = file, RiskScore = risk, Reason = reason });
            }
            plan = plan.OrderByDescending(item => item.RiskScore)
                .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToList();
            report = BuildReport(root, maxAgeDays, plan);
            return 0;
        }
        catch (Exception ex)
        {
            report = "Could not build CHD scrub schedule: " + ex.Message;
            return 5;
        }
    }

    public static int RunDue(string root, ChdScrubMode mode, int maxAgeDays, int limit, out string report)
    {
        int planCode = Plan(root, maxAgeDays, limit, out List<ChdScrubPlanItem> plan, out string planReport);
        if (planCode != 0)
        {
            report = planReport;
            return planCode;
        }
        StringBuilder output = new StringBuilder(planReport);
        int failed = 0;
        for (int i = 0; i < plan.Count; i++)
        {
            int code = ChdScrubber.Scrub(plan[i].Path, mode, out string itemReport);
            output.AppendLine();
            output.AppendLine(itemReport);
            if (code != 0) failed++;
        }
        output.AppendLine();
        output.AppendLine("scheduledPassed=" + (plan.Count - failed));
        output.AppendLine("scheduledFailed=" + failed);
        report = output.ToString().TrimEnd();
        return failed == 0 ? 0 : 5;
    }

    private static string BuildReport(string root, int maxAgeDays, List<ChdScrubPlanItem> plan)
    {
        StringBuilder output = new StringBuilder();
        output.AppendLine("RomVault CHD scrub schedule");
        output.AppendLine("root=" + ChdDiagnosticFormatter.RedactPath(root));
        output.AppendLine("parityMaxAgeDays=" + maxAgeDays);
        output.AppendLine("due=" + plan.Count);
        for (int i = 0; i < plan.Count; i++)
            output.AppendLine(ChdDiagnosticFormatter.RedactPath(plan[i].Path) + " risk=" + plan[i].RiskScore + " reason=" + plan[i].Reason);
        return output.ToString().TrimEnd();
    }
}
