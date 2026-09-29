using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace XenPlus.VifConfigure;

sealed class VifConfigureOptions {
    public bool Enabled { get; set; } = true;
    public bool AllowConfigureNonVifs { get; set; } = true;
    [Range(100, 3_600_000)]
    public int CommandTimeoutMilliseconds { get; set; } = 5000;
    public bool ClearDnsOnEmptyDnsSetting { get; set; } = false;
}

[OptionsValidator]
partial class ValidateVifConfigureOptions : IValidateOptions<VifConfigureOptions> {
}
