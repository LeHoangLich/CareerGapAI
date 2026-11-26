namespace CareerGap.Api.Recruitment;
public sealed record CatalogLocation(string Id,string Name,string[] Aliases);
public static class LocationCatalog
{
    // Source: Decision 19/2025/QD-TTg table, as published by baochinhphu.vn on 2025-07-04.
    public const string Source="https://baochinhphu.vn/bang-danh-muc-va-ma-so-cua-34-tinh-thanh-moi-3321-don-vi-hanh-chinh-cap-xa-moi-102250704153652947.htm";
    private const string Rows="01:Hà Nội|04:Cao Bằng|08:Tuyên Quang|11:Điện Biên|12:Lai Châu|14:Sơn La|15:Lào Cai|19:Thái Nguyên|20:Lạng Sơn|22:Quảng Ninh|24:Bắc Ninh|25:Phú Thọ|31:Hải Phòng|33:Hưng Yên|37:Ninh Bình|38:Thanh Hóa|40:Nghệ An|42:Hà Tĩnh|44:Quảng Trị|46:Huế|48:Đà Nẵng|51:Quảng Ngãi|52:Gia Lai|56:Khánh Hòa|66:Đắk Lắk|68:Lâm Đồng|75:Đồng Nai|79:Hồ Chí Minh|80:Tây Ninh|82:Đồng Tháp|86:Vĩnh Long|91:An Giang|92:Cần Thơ|96:Cà Mau";
    public static readonly CatalogLocation[] Items=Rows.Split('|').Select(row=>{
        var parts=row.Split(':');var name=parts[1];var city=new[]{"01","31","46","48","79","92"}.Contains(parts[0]);
        var aliases=new List<string>{SkillCatalog.Fold(name),(city?"Thành phố ":"Tỉnh ")+name};
        if(city){aliases.Add("TP. "+name);aliases.Add("TP "+name);}
        if(parts[0]=="01")aliases.Add("Hanoi");
        if(parts[0]=="79")aliases.AddRange(["TP.HCM","TP HCM","TPHCM","HCM","HCMC","Ho Chi Minh City","Sài Gòn","Saigon"]);
        return new CatalogLocation("vn-"+parts[0],name,aliases.Distinct().ToArray());
    }).ToArray();
    public static CatalogLocation? Find(string value)=>Items.SingleOrDefault(l=>new[]{l.Name}.Concat(l.Aliases).Any(a=>SkillCatalog.Fold(a)==SkillCatalog.Fold(value)));
    public static string? Normalize(string? value)=>string.IsNullOrWhiteSpace(value)?null:Find(value.Trim())?.Name??value.Trim();
    public static string[] SearchNames(string value)=>Find(value) is {} location ? new[]{location.Name}.Concat(location.Aliases).Distinct().ToArray():[value.Trim()];
    public static void MapLocationCatalog(this WebApplication app)=>app.MapGet("/api/locations",(string? q)=>{
        if(q?.Length>100)return Results.Problem(statusCode:400,title:"invalid_location_query");
        var items=Items.Where(l=>string.IsNullOrWhiteSpace(q)||new[]{l.Name}.Concat(l.Aliases).Any(a=>SkillCatalog.Fold(a).Contains(SkillCatalog.Fold(q))));
        return Results.Ok(new {version="vn-locations-2025-v1",source=Source,items=items.ToArray()});
    });
}
