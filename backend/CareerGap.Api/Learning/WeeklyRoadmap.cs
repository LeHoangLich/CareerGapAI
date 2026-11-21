using System.Security.Claims;
using CareerGap.Api.Matching;

namespace CareerGap.Api.Learning;

public sealed record RoadmapFocus(string RequirementKey, string Requirement, string Importance, string Goal, string Practice, string Deliverable);
public sealed record RoadmapWeek(int Week, int Hours, IReadOnlyList<RoadmapFocus> Focus);
public sealed record WeeklyPlan(Guid PairId, int Weeks, int HoursPerWeek, string Method, IReadOnlyList<RoadmapWeek> Schedule, string? CareerContext = null);

public static class WeeklyRoadmap
{
    public static WeeklyPlan Build(Guid id, IReadOnlyList<ScoredCriterion> criteria, int weeks, int hours, string? careerContext = null)
    {
        if (weeks is < 2 or > 12 || hours is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(weeks));
        var gaps = criteria.Where(c => c.Status is "partial" or "no_evidence")
            .OrderBy(c => c.Importance == "required" ? 0 : 1).ThenBy(c => c.Status == "no_evidence" ? 0 : 1)
            .ThenBy(c => c.RequirementKey, StringComparer.Ordinal).ToArray();
        if (gaps.Length == 0) return new(id, weeks, hours, "weekly-plan-v1", [], careerContext);
        var schedule = new List<RoadmapWeek>();
        var occurrences = new Dictionary<string,int>();
        for (var week = 0; week < weeks; week++)
        {
            var selected = gaps.Length >= weeks ? gaps.Where((_, index) => index % weeks == week).ToArray() : [gaps[week % gaps.Length]];
            var focus = new List<RoadmapFocus>();
            foreach (var gap in selected)
            {
                var round = occurrences.GetValueOrDefault(gap.RequirementKey); occurrences[gap.RequirementKey] = round + 1;
                var (goal, practice, deliverable) = (careerContext, round) switch
                {
                    ("career_start", 0) => (
                        "Nắm vững khái niệm cốt lõi và thuật ngữ nền tảng cho người mới bắt đầu.",
                        "Làm bài tập mẫu cơ bản theo hướng dẫn từng bước và ghi chú lại các lỗi thường gặp.",
                        "Một bài thực hành hoàn chỉnh ở mức cơ bản kèm ghi chú kiến thức nền."),
                    ("career_start", _) => (
                        "Củng cố kỹ năng qua tình huống thực tế ở mức độ đầu vào.",
                        "Tự xây dựng lại một tính năng nhỏ không nhìn hướng dẫn và kiểm tra kết quả.",
                        "Sản phẩm demo nhỏ kèm danh sách kiểm tra tự đánh giá."),
                    ("career_switch", 0) => (
                        "Ánh xạ kinh nghiệm nền tảng cũ sang yêu cầu mới của vị trí chuyển ngành.",
                        "Chọn một bài toán tương đồng với lĩnh vực cũ nhưng triển khai bằng công nghệ/quy trình mới.",
                        "Bản đối chiếu kỹ năng cũ–mới và một bài tập cầu nối có kết quả chạy được."),
                    ("career_switch", _) => (
                        "Lấp khoảng trống thực chiến để chứng minh khả năng chuyển đổi vai trò.",
                        "Mở rộng bài tập cầu nối thành mini-project hoàn chỉnh và xử lý các ngoại lệ.",
                        "Mini-project kèm mô tả cách chuyển hóa kinh nghiệm cũ sang bối cảnh mới."),
                    ("promotion", 0) => (
                        "Đào sâu tư duy hệ thống, tối ưu hiệu năng và tiêu chuẩn chất lượng ở mức nâng cao.",
                        "Phân tích trade-off thiết kế, xử lý tình huống tải cao hoặc viết tài liệu kỹ thuật cho yêu cầu này.",
                        "Bản thiết kế/giải pháp nâng cao kèm phân tích ưu nhược điểm."),
                    ("promotion", _) => (
                        "Chứng minh năng lực dẫn dắt, review và giải quyết sự cố phức tạp.",
                        "Tái cấu trúc hoặc tối ưu giải pháp trước, viết test bao phủ các trường hợp biên.",
                        "Báo cáo cải tiến có số liệu đo lường hoặc dẫn chứng trước/sau."),
                    (_, 0) => (
                        "Rà yêu cầu, xác định phần đã biết và học kiến thức nền còn thiếu.",
                        "Chọn một bài tập nhỏ liên quan trực tiếp đến yêu cầu; ghi lại cách làm, lỗi gặp và cách sửa.",
                        "Một bài tập hoàn chỉnh kèm ghi chú và bằng chứng kết quả."),
                    _ => (
                        "Củng cố phần còn yếu qua một tình huống ứng dụng có thể kiểm chứng.",
                        "Mở rộng bài tập trước, thử ít nhất một trường hợp lỗi và giải thích lựa chọn của bạn.",
                        "Một phiên bản bài tập cải tiến kèm kiểm tra kết quả và phần tự đánh giá.")
                };
                focus.Add(new(gap.RequirementKey, gap.Requirement, gap.Importance, goal, practice, deliverable));
            }
            schedule.Add(new(week+1, hours, focus));
        }
        return new(id, weeks, hours, "weekly-plan-v1", schedule, careerContext);
    }

    public static void MapWeeklyRoadmap(this WebApplication app)
    {
        app.MapGet("/api/learning/roadmap/{id:guid}", async (Guid id, int? weeks, int? hoursPerWeek, ClaimsPrincipal user, PairMatchingService pairs, CancellationToken ct) =>
        {
            var length = weeks ?? 4; var hours = hoursPerWeek ?? 5;
            if (length is < 2 or > 12 || hours is < 1 or > 20) return Results.Problem(statusCode:400, title:"invalid_roadmap_options");
            var uid = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var pair = await pairs.Read(uid, id, ct);
            if (pair is not { Context: "self", Status: "succeeded", Criteria: not null }) return Results.NotFound();
            var plan = Build(id, pair.Criteria, length, hours, pair.CareerContext);
            // Reuse current source access checks after constructing the plan as well.
            if (await pairs.Read(uid, id, ct) is null) return Results.NotFound();
            return Results.Ok(plan);
        }).RequireAuthorization(p => p.RequireRole("Candidate"));
    }
}
