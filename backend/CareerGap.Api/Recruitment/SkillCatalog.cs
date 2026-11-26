using System.Globalization;
using System.Text;
namespace CareerGap.Api.Recruitment;
public sealed record CatalogSkill(string Id,string Name,string Category,string[] Aliases);
public static class SkillCatalog
{
    public const string Version="careergap-skills-v1";
    public static readonly CatalogSkill[] Items = [
        new("react","React","CNTT",["React.js","ReactJS"]),new("typescript","TypeScript","CNTT",["TS"]),new("javascript","JavaScript","CNTT",["JS"]),
        new("java","Java","CNTT",[]),new("csharp","C#","CNTT",["CSharp"]),new("cpp","C++","CNTT",["CPP"]),new("dotnet",".NET","CNTT",["dotnet"]),new("aspnet","ASP.NET Core","CNTT",["ASP.NET"]),
        new("python","Python","CNTT",[]),new("go","Go","CNTT",["Golang"]),new("php","PHP","CNTT",[]),new("ruby","Ruby","CNTT",[]),new("kotlin","Kotlin","CNTT",[]),new("swift","Swift","CNTT",[]),
        new("html","HTML","CNTT",["HTML5"]),new("css","CSS","CNTT",["CSS3"]),new("vue","Vue.js","CNTT",["Vue","VueJS"]),new("angular","Angular","CNTT",[]),new("node","Node.js","CNTT",["NodeJS"]),
        new("sql","SQL","CNTT",[]),new("postgresql","PostgreSQL","CNTT",["Postgres"]),new("mysql","MySQL","CNTT",[]),new("sqlserver","SQL Server","CNTT",["MSSQL"]),new("mongodb","MongoDB","CNTT",[]),
        new("docker","Docker","CNTT",[]),new("kubernetes","Kubernetes","CNTT",["K8s"]),new("aws","AWS","CNTT",["Amazon Web Services"]),new("azure","Azure","CNTT",["Microsoft Azure"]),
        new("git","Git","CNTT",[]),new("cicd","CI/CD","CNTT",["CI CD"]),new("linux","Linux","CNTT",[]),new("testing","Kiểm thử phần mềm","CNTT",["Software testing"]),new("rest","REST API","CNTT",["RESTful"]),
        new("excel","Microsoft Excel","Văn phòng & Dữ liệu",["Excel"]),new("word","Microsoft Word","Văn phòng & Dữ liệu",["Word"]),new("powerpoint","Microsoft PowerPoint","Văn phòng & Dữ liệu",["PowerPoint"]),
        new("powerbi","Power BI","Văn phòng & Dữ liệu",[]),new("tableau","Tableau","Văn phòng & Dữ liệu",[]),new("data-analysis","Phân tích dữ liệu","Văn phòng & Dữ liệu",["Data analysis"]),
        new("statistics","Thống kê","Văn phòng & Dữ liệu",["Statistics"]),new("accounting","Kế toán","Tài chính & Kinh doanh",["Accounting"]),new("finance","Phân tích tài chính","Tài chính & Kinh doanh",["Financial analysis"]),
        new("sales","Bán hàng","Tài chính & Kinh doanh",["Sales"]),new("customer-service","Chăm sóc khách hàng","Tài chính & Kinh doanh",["Customer service"]),new("negotiation","Đàm phán","Tài chính & Kinh doanh",["Negotiation"]),
        new("marketing","Digital Marketing","Marketing & Thiết kế",["Tiếp thị số"]),new("seo","SEO","Marketing & Thiết kế",[]),new("content","Content Marketing","Marketing & Thiết kế",["Tiếp thị nội dung"]),
        new("figma","Figma","Marketing & Thiết kế",[]),new("photoshop","Adobe Photoshop","Marketing & Thiết kế",["Photoshop"]),new("ux","Thiết kế UX","Marketing & Thiết kế",["UX design"]),
        new("communication","Giao tiếp","Kỹ năng chung",["Communication"]),new("teamwork","Làm việc nhóm","Kỹ năng chung",["Teamwork"]),new("problem-solving","Giải quyết vấn đề","Kỹ năng chung",["Problem solving"]),
        new("project-management","Quản lý dự án","Kỹ năng chung",["Project management"]),new("time-management","Quản lý thời gian","Kỹ năng chung",["Time management"]),new("english","Tiếng Anh","Kỹ năng chung",["English"])
    ];
    public static string Fold(string value)=>string.Concat(value.Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)).Replace('đ','d').Replace('Đ','D').Trim().ToLowerInvariant();
    public static IEnumerable<CatalogSkill> Search(string? q,string? category)=>Items.Where(s=>(string.IsNullOrEmpty(category)||s.Category==category)&&(string.IsNullOrWhiteSpace(q)||new[]{s.Name,s.Id}.Concat(s.Aliases).Any(v=>Fold(v).Contains(Fold(q))))).OrderBy(s=>s.Name,StringComparer.OrdinalIgnoreCase);
    public static void MapSkillCatalog(this WebApplication app)=>app.MapGet("/api/skills",(string? q,string? category)=>{
        if(q?.Length>100||category is not null&&!Items.Any(s=>s.Category==category))return Results.Problem(statusCode:400,title:"invalid_skill_query");
        return Results.Ok(new {version=Version,categories=Items.Select(s=>s.Category).Distinct().ToArray(),items=Search(q,category).ToArray()});
    });
}
