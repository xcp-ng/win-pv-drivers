$ErrorActionPreference = "Stop"
$base = Get-CimInstance -Namespace "root\WMI" -ClassName CitrixXenStoreBase
$result = Invoke-CimMethod -InputObject $base -MethodName AddSession -Arguments @{Id=$base.InstanceName}
$session = Get-CimInstance -Namespace "root\WMI" -ClassName CitrixXenStoreSession -Filter "SessionId = $($result.SessionId)"
try {
    Invoke-CimMethod -InputObject $session -MethodName SetValue -Arguments @{Pathname="vm-data/foo";value="123"}
} finally {
    Invoke-CimMethod -InputObject $session -MethodName EndSession
}