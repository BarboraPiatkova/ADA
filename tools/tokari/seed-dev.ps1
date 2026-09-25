<#
.SYNOPSIS
  Registers AdaPlatform in a local development Tokari: the application, its permissions,
  a Viewer role, and a dev user who has it. Safe to run again; it only adds what's missing.

.DESCRIPTION
  Signs in as Tokari's seeded development admin and uses Tokari's admin API on the external
  port. For a local Tokari only (docker-compose.tokari.yml) — never point it at a shared one.

.EXAMPLE
  ./tools/tokari/seed-dev.ps1
  ./tools/tokari/seed-dev.ps1 -UserPassword 'Something-long-1'
#>
param(
    [string] $TokariUrl = 'http://localhost:8091',
    # Tokari seeds this admin in Development (Tokari.API/Program.cs).
    [string] $AdminUser = 'admin',
    [string] $AdminPassword = 'Admin123!',
    [string] $UserName = 'dispecer',
    [string] $UserEmail = 'dispecer@adaplatform.local',
    [string] $UserPassword = 'Dispecer-dev-1'
)

$ErrorActionPreference = 'Stop'
$App = 'AdaPlatform'                                   # must equal Tokari:Audience
$PermissionNames = @('network:read', 'quality:read')   # src/AdaPlatform.Api/Auth/Permissions.cs

function Invoke-Tokari([string] $Method, [string] $Path, $Body) {
    $params = @{ Method = $Method; Uri = "$TokariUrl$Path"; Headers = $script:Headers; ContentType = 'application/json' }
    # -InputObject, not a pipe: piping an array unrolls it, and Tokari gets no JSON array.
    if ($null -ne $Body) { $params.Body = ConvertTo-Json -InputObject $Body -Depth 5 -Compress }
    $result = Invoke-RestMethod @params
    # Windows PowerShell passes a JSON array on as one object; emit its items instead.
    if ($result -is [array]) { $result | ForEach-Object { $_ } } else { $result }
}

$login = Invoke-RestMethod -Method Post -Uri "$TokariUrl/api/auth/login" -ContentType 'application/json' `
    -Body (@{ email = $AdminUser; password = $AdminPassword } | ConvertTo-Json)
$script:Headers = @{ Authorization = "Bearer $($login.accessToken)" }

$application = Invoke-Tokari Get '/api/applications' | Where-Object name -eq $App
if (-not $application) {
    $application = Invoke-Tokari Post '/api/applications' @{ name = $App }
    Write-Host "Created application $App"
}
$appId = $application.id

# Tokari's applicationId filter isn't applied for every caller; filter here too.
$existing = @(Invoke-Tokari Get "/api/permissions?applicationId=$appId" | Where-Object applicationId -eq $appId)
$permissionIds = foreach ($name in $PermissionNames) {
    $permission = $existing | Where-Object name -eq $name
    if (-not $permission) {
        $permission = Invoke-Tokari Post '/api/permissions' @{ name = $name; applicationId = $appId }
        Write-Host "Created permission $name"
    }
    $permission.id
}

$role = Invoke-Tokari Get "/api/roles?applicationId=$appId" | Where-Object { $_.applicationId -eq $appId -and $_.name -eq 'Viewer' }
if (-not $role) {
    $role = Invoke-Tokari Post '/api/roles' @{ name = 'Viewer'; applicationId = $appId; permissionIds = @($permissionIds) }
    Write-Host 'Created role Viewer'
}
else {
    $role = Invoke-Tokari Post "/api/roles/$($role.id)/permissions" @($permissionIds)
}

$user = Invoke-Tokari Get '/api/users' | Where-Object userName -eq $UserName
if (-not $user) {
    Invoke-Tokari Post '/api/users' @{
        userName = $UserName; email = $UserEmail; password = $UserPassword
        roles = @(@{ applicationId = $appId; roleId = $role.id })
    } | Out-Null
    Write-Host "Created user $UserName"
}
elseif (-not ($user.applications | Where-Object applicationName -eq $App)) {
    Invoke-Tokari Post "/api/users/$($user.id)/roles" @{ applicationId = $appId; roleId = $role.id } | Out-Null
    Write-Host "Gave $UserName the Viewer role"
}

Write-Host "Done. Sign in to AdaPlatform as '$UserName'."
