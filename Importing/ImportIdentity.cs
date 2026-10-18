using PhotoImport.Models;
namespace PhotoImport.Importing;
public static class ImportIdentity
{
    public static ImportIdentityRecord Enroll(DeviceInfo device)
    {
        var serial=device.Identifiers.GetValueOrDefault("SerialNumber");
        return new(string.IsNullOrWhiteSpace(serial)?"deviceId":"serial",string.IsNullOrWhiteSpace(serial)?null:serial,
            device.Id,device.Manufacturer,device.Model);
    }
    public static bool Matches(ImportIdentityRecord identity, DeviceInfo device) =>
        (identity.Mode=="serial" ? !string.IsNullOrWhiteSpace(identity.SerialNumber)&&identity.SerialNumber==device.Identifiers.GetValueOrDefault("SerialNumber")
            : identity.Mode=="deviceId"&&identity.DeviceId==device.Id)
        && (identity.Manufacturer is null || identity.Manufacturer==device.Manufacturer)
        && (identity.Model is null || identity.Model==device.Model);
    public static (ImportRegistration Registration,DeviceInfo Device) Select(IReadOnlyList<ImportRegistration> registrations,IReadOnlyList<DeviceInfo> devices,string? alias)
    {
        var choices=registrations.Where(r=>alias is null||r.Alias.Equals(alias,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(alias is not null&&choices.Length!=1)throw new ImportException("Unknown or ambiguous registered alias.");
        var matches=choices.SelectMany(r=>devices.Where(d=>Matches(r.Identity,d)).Select(d=>(r,d))).ToArray();
        if(matches.Length==0)throw new ImportException("Registered phone not found. Unlock it and enable USB File transfer; saved identity must match.",3);
        if(matches.Length!=1)throw new ImportException("Multiple matching phones. Specify a registered --device alias; identity must be unique.");
        return matches[0];
    }
}
