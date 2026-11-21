namespace CareerGap.Api.Recruitment;
public static class JobTags
{
    public static bool TryNormalize(string[]? input,out string[]? tags)
    {
        tags=null;if(input is null)return true;
        if(input.Length>10)return false;
        var names=new List<string>();
        foreach(var raw in input){
            if(string.IsNullOrWhiteSpace(raw)||raw.Length>100)return false;
            var skill=SkillCatalog.Items.SingleOrDefault(x=>x.Name.Equals(raw.Trim(),StringComparison.OrdinalIgnoreCase));
            if(skill is null)return false;
            if(!names.Contains(skill.Name,StringComparer.Ordinal))names.Add(skill.Name);
        }
        tags=names.OrderBy(x=>x,StringComparer.Ordinal).ToArray();return true;
    }
}
