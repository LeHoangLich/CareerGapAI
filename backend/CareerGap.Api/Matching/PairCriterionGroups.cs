using System.Text.RegularExpressions;

namespace CareerGap.Api.Matching;

public sealed record CriterionGroupSummary(
    string Key,
    string Label,
    string Status,
    int TotalCriteria,
    int MetCount,
    int PartialCount,
    int NoEvidenceCount,
    string Summary);

public static partial class PairCriterionGroups
{
    [GeneratedRegex(@"\b(bachelor|master|phd|degree|university|college|diploma|gpa|major|graduate)\b|đại\s+học|cao\s+đẳng|cử\s+nhân|thạc\s+sĩ|tiến\s+sĩ|tốt\s+nghiệp|bằng\s+cấp|chuyên\s+ngành", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EducationPattern();

    [GeneratedRegex(@"\b(portfolio|github|gitlab|behance|dribbble|demo|showcase|deployed|open[- ]?source|side\s+project|case\s+study)\b|dự\s+án\s+(?:cá\s+nhân|thực\s+tế|mẫu)|sản\s+phẩm\s+(?:đã\s+làm|demo)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PortfolioPattern();

    [GeneratedRegex(@"\b(achiev\w*|award\w*|certif\w*|honor\w*|prize|medal|scholarship|kpi|okr|impact)\b|thành\s+tích|giải\s+thưởng|chứng\s+chỉ|học\s+bổng|danh\s+hiệu", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AchievementPattern();

    [GeneratedRegex(@"\b(years?|experience|senior|junior|lead|manage\w*|mentor\w*|worked\s+with|hands[- ]?on)\b|kinh\s+nghiệm|năm\s+làm|từng\s+làm|quản\s+lý|dẫn\s+dắt|triển\s+khai", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExperiencePattern();

    private static string Classify(ScoredCriterion c)
    {
        if (EducationPattern().IsMatch(c.Requirement)) return "Education";
        if (PortfolioPattern().IsMatch(c.Requirement)) return "Portfolio";
        if (AchievementPattern().IsMatch(c.Requirement)) return "Achievements";
        if (ExperiencePattern().IsMatch(c.Requirement)) return "Experience";
        return "SkillMatch";
    }

    public static IReadOnlyList<CriterionGroupSummary> Summarize(IReadOnlyList<ScoredCriterion> criteria)
    {
        var assessable = criteria.Where(c => c.Status != "not_assessed").ToArray();
        var buckets = new (string Key, string Label)[]
        {
            ("SkillMatch", "Kỹ năng chuyên môn"),
            ("Experience", "Kinh nghiệm thực tế"),
            ("Achievements", "Thành tích & Chứng chỉ"),
            ("Portfolio", "Dự án & Portfolio"),
            ("Education", "Học vấn & Bằng cấp")
        };

        var result = new List<CriterionGroupSummary>(6);
        foreach (var (key, label) in buckets)
        {
            var items = assessable.Where(c => Classify(c) == key).ToArray();
            if (items.Length == 0)
            {
                result.Add(new(key, label, "not_requested", 0, 0, 0, 0, "Không có yêu cầu thuộc nhóm này."));
                continue;
            }
            var met = items.Count(c => c.Status == "met");
            var partial = items.Count(c => c.Status == "partial");
            var none = items.Count(c => c.Status == "no_evidence");
            var status = met == items.Length ? "met" : (met > 0 || partial > 0) ? "partial" : "no_evidence";
            result.Add(new(key, label, status, items.Length, met, partial, none,
                $"{met}/{items.Length} đạt, {partial} một phần, {none} chưa có bằng chứng."));
        }

        // AtsKeywords summarizes verified lexical/evidence coverage across assessable criteria without altering rubric scores.
        var totalAssessable = assessable.Length;
        if (totalAssessable == 0)
        {
            result.Add(new("AtsKeywords", "Từ khóa & Bằng chứng ATS", "not_requested", 0, 0, 0, 0, "Không có yêu cầu thuộc nhóm này."));
        }
        else
        {
            var withEvidence = assessable.Count(c => c.Evidence.Count > 0);
            var atsStatus = withEvidence == totalAssessable ? "met" : withEvidence > 0 ? "partial" : "no_evidence";
            result.Add(new("AtsKeywords", "Từ khóa & Bằng chứng ATS", atsStatus, totalAssessable,
                assessable.Count(c => c.Status == "met"),
                assessable.Count(c => c.Status == "partial"),
                assessable.Count(c => c.Status == "no_evidence"),
                $"{withEvidence}/{totalAssessable} yêu cầu có từ khóa/bằng chứng đối chiếu trực tiếp trong CV."));
        }
        return result;
    }
}
