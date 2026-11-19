#requires -Version 7.0
param(
    [string]$ProjectRoot = (Split-Path -Parent $PSScriptRoot),
    [string]$OutputParent = (Join-Path $HOME 'Downloads'),
    [string]$IsccPath
)

$ErrorActionPreference = 'Stop'

function Step([string]$Message) {
    Write-Host "`n==> $Message" -ForegroundColor Cyan
}

function Find-InnoCompiler {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    foreach ($version in @(6, 7)) {
        foreach ($parent in @(${env:ProgramFiles(x86)}, $env:ProgramFiles, (Join-Path $env:LOCALAPPDATA 'Programs'))) {
            if ([string]::IsNullOrWhiteSpace($parent)) { continue }
            $candidate = Join-Path $parent "Inno Setup $version/ISCC.exe"
            if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
        }
    }
    return $null
}

function Copy-RuntimeNotices([string]$Id, [string]$Version, [string[]]$Roots, [string]$Destination) {
    # The Windows Desktop 10.0.12 runtime pack ships LICENSE without a separate
    # THIRD-PARTY-NOTICES file. NETCore ships both. Preserve every supplied notice.
    $requireNotices = switch ($Id) {
        'Microsoft.NETCore.App.Runtime.win-x64' { $true }
        'Microsoft.WindowsDesktop.App.Runtime.win-x64' { $false }
        default { throw "Runtime pack não previsto no empacotamento: $Id" }
    }
    $packageRoot = $null
    foreach ($root in $Roots) {
        $candidate = Join-Path $root ($Id.ToLowerInvariant() + '/' + $Version)
        if (Test-Path -LiteralPath $candidate -PathType Container) {
            $packageRoot = $candidate
            break
        }
    }
    if (-not $packageRoot) { throw "Runtime pack não localizado: $Id $Version." }
    $files = @(Get-ChildItem -LiteralPath $packageRoot -File | Where-Object {
        $_.Name -match '^(license|third[-_ ]?party[-_ ]?notices)(\..*)?$'
    })
    if (-not @($files | Where-Object { $_.Name -match '^license(\.|$)' }).Count) {
        throw "Licença ausente no runtime pack $Id $Version. Pasta: $packageRoot"
    }
    if ($requireNotices -and -not @($files | Where-Object { $_.Name -match '^third' }).Count) {
        throw "Avisos de terceiros ausentes no runtime pack $Id $Version. Pasta: $packageRoot"
    }
    if (@($files | Where-Object { $_.Length -eq 0 }).Count) {
        throw "Arquivo de licença ou avisos vazio no runtime pack $Id $Version."
    }
    $folder = Join-Path $Destination "$Id/$Version"
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    foreach ($file in $files) {
        Copy-Item -LiteralPath $file.FullName -Destination $folder -Force
        "`r`n$Id $Version - $($file.Name)`r`n`r`n" + [IO.File]::ReadAllText($file.FullName)
    }
}

if (-not $IsWindows) { throw 'Execute o empacotamento no Windows.' }
$ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot)
$project = Join-Path $ProjectRoot 'SwitchSaveAssistant.App/SwitchSaveAssistant.App.csproj'
$installer = Join-Path $PSScriptRoot 'Installer.iss'
foreach ($relative in @('SwitchSaveAssistant.slnx', 'LICENSE', 'THIRD-PARTY-NOTICES.md', 'SwitchSaveAssistant.App/Assets/App.ico')) {
    if (-not (Test-Path -LiteralPath (Join-Path $ProjectRoot $relative) -PathType Leaf)) {
        throw "Arquivo necessário ausente: $relative"
    }
}
if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) { throw 'Installer.iss ausente.' }
$projectXml = [xml][IO.File]::ReadAllText($project)
$version = [string]$projectXml.Project.PropertyGroup.Version
if ($version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$') {
    throw "Versão inválida para a distribuição: $version"
}
$binaryVersion = [string]$projectXml.Project.PropertyGroup.FileVersion
if ($binaryVersion -notmatch '^([0-9]+\.){3}[0-9]+$' -or @($binaryVersion.Split('.') | Where-Object { [long]$_ -gt 65535 }).Count) {
    throw "FileVersion inválida para o instalador: $binaryVersion"
}

Step 'Conferindo Inno Setup'
if (-not $IsccPath) { $IsccPath = Find-InnoCompiler }
if (-not $IsccPath) {
    if (-not (Get-Command winget.exe -ErrorAction SilentlyContinue)) {
        throw 'Instale Inno Setup pelo site https://jrsoftware.org/isinfo.php e execute novamente.'
    }
    & winget.exe install --id JRSoftware.InnoSetup --exact --source winget --accept-package-agreements --accept-source-agreements
    if ($LASTEXITCODE -ne 0) { throw 'A instalação do Inno Setup falhou.' }
    $IsccPath = Find-InnoCompiler
}
if (-not $IsccPath -or -not (Test-Path -LiteralPath $IsccPath -PathType Leaf)) {
    throw 'ISCC.exe não encontrado. Informe -IsccPath com o caminho do compilador.'
}

$releaseRoot = Join-Path $OutputParent ('SwitchSaveAssistant-' + $version + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 6))
$payload = Join-Path $releaseRoot 'portable/SwitchSaveAssistant'
New-Item -ItemType Directory -Path $payload -Force | Out-Null
Push-Location -LiteralPath $ProjectRoot
try {
    Step 'Fechando a cópia de desenvolvimento'
    $developmentExe = Join-Path $ProjectRoot 'SwitchSaveAssistant.App/bin/Release/net10.0-windows/SwitchSaveAssistant.exe'
    $running = @(Get-Process -Name SwitchSaveAssistant -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $developmentExe })
    foreach ($process in $running) {
        $null = $process.CloseMainWindow()
        if (-not $process.WaitForExit(15000)) { throw 'Conclua as operações abertas e feche o aplicativo.' }
    }

    Step 'Build Release e testes'
    & dotnet build ./SwitchSaveAssistant.slnx -c Release --nologo -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Build Release falhou.' }
    & dotnet test ./SwitchSaveAssistant.Tests/SwitchSaveAssistant.Tests.csproj -c Release --no-build --no-restore --nologo
    if ($LASTEXITCODE -ne 0) { throw 'A suíte automatizada falhou.' }

    Step 'Publicando Windows x64 autocontido'
    & dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:DebugType=None -p:DebugSymbols=false --nologo -warnaserror -o $payload
    if ($LASTEXITCODE -ne 0) { throw 'Publicação falhou.' }
    $exe = Join-Path $payload 'SwitchSaveAssistant.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'Executável ausente na publicação.' }
    $productVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($exe).ProductVersion
    if (($productVersion -split '\+')[0] -ne $version) { throw "Versão do executável inesperada: $productVersion" }
    foreach ($name in @('coreclr.dll', 'hostpolicy.dll', 'PresentationFramework.dll', 'MediaDevices.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $payload $name) -PathType Leaf)) { throw "Publicação incompleta: $name" }
    }
    $runtimeConfig = Get-Content -LiteralPath (Join-Path $payload 'SwitchSaveAssistant.runtimeconfig.json') -Raw | ConvertFrom-Json
    $frameworks = @($runtimeConfig.runtimeOptions.includedFrameworks)
    foreach ($name in @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App')) {
        if (-not @($frameworks | Where-Object { $_.name -eq $name }).Count) { throw "Runtime autocontido ausente: $name" }
    }
    foreach ($name in @('conectar-switch.html', 'connect-switch.en-US.html', 'conectar-switch.es-ES.html')) {
        if (-not (Test-Path -LiteralPath (Join-Path $payload "Help/$name") -PathType Leaf)) { throw "Ajuda ausente: $name" }
    }

    Step 'Incluindo licenças e avisos dos runtimes publicados'
    Copy-Item -LiteralPath (Join-Path $ProjectRoot 'LICENSE') -Destination $payload
    Copy-Item -LiteralPath (Join-Path $ProjectRoot 'THIRD-PARTY-NOTICES.md') -Destination $payload
    $licenses = [Collections.Generic.List[string]]::new()
    $licenses.Add([IO.File]::ReadAllText((Join-Path $ProjectRoot 'LICENSE')))
    $licenses.Add([IO.File]::ReadAllText((Join-Path $ProjectRoot 'THIRD-PARTY-NOTICES.md')))
    $assets = Get-Content -LiteralPath (Join-Path $ProjectRoot 'SwitchSaveAssistant.App/obj/project.assets.json') -Raw | ConvertFrom-Json
    $roots = @($assets.packageFolders.PSObject.Properties.Name)
    $dotnetDir = Split-Path -Parent (Get-Command dotnet -ErrorAction Stop).Source
    $roots += Join-Path $dotnetDir 'packs'
    foreach ($framework in $frameworks) {
        $id = $framework.name + '.Runtime.win-x64'
        foreach ($text in @(Copy-RuntimeNotices $id $framework.version $roots (Join-Path $payload 'Licenses'))) {
            $licenses.Add($text)
        }
    }
    [IO.File]::WriteAllText((Join-Path $payload 'LICENSES.txt'), ($licenses -join "`r`n`r`n"), [Text.UTF8Encoding]::new($true))

    Step 'Conferindo arquivos da distribuição'
    $published = @(Get-ChildItem -LiteralPath $payload -Recurse -File -Force)
    $private = @($published | Where-Object {
        $_.Name -in @('settings.json', 'profiles.json', 'profiles.dat', '.env') -or
        $_.FullName -match '[\\/](SafetyBackups|SyncData|TestBackup|RestoreStaging|_backups)[\\/]' -or
        $_.Name -match '\.(bak|pdb)($|-)'
    })
    if ($private.Count -gt 0) { throw ('Arquivos locais encontrados na publicação: ' + ($private.Name -join ', ')) }
    $sourceCommit = & git rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível identificar o commit de origem.' }
    $sourceStatus = @(& git status --porcelain)
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível conferir o estado Git.' }
    $info = [ordered]@{
        version = $version
        productVersion = $productVersion
        runtimeIdentifier = 'win-x64'
        selfContained = $true
        sourceCommit = ($sourceCommit -join '').Trim()
        sourceHasChanges = $sourceStatus.Count -gt 0
        frameworks = $frameworks
        binariesSigned = $false
    }
    $info | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $payload 'release-info.json') -Encoding utf8
    $readme = @"
Switch Save Assistant $version

Para usar a edição portable, extraia todo o ZIP e abra SwitchSaveAssistant.exe.
As configurações ficam em %LOCALAPPDATA%\SwitchSaveAssistant.
Selecione a pasta central de saves e a raiz do Eden nas configurações.
O módulo Switch usa a conexão DBI/MTP.

Confira origem, destino e prévia antes de sincronizar ou restaurar saves.
Versão de pré-lançamento para avaliação. Executável sem assinatura digital.

Licenças e avisos disponíveis em LICENSE, THIRD-PARTY-NOTICES.md e Licenses.
Projeto e instruções em https://github.com/ascc-br/switch-save-assistant
"@
    $readme | Set-Content -LiteralPath (Join-Path $payload 'LEIA-ME.txt') -Encoding utf8BOM
    $inventory = foreach ($file in @(Get-ChildItem -LiteralPath $payload -Recurse -File -Force)) {
        [pscustomobject]@{
            File = [IO.Path]::GetRelativePath($payload, $file.FullName)
            Bytes = $file.Length
            SHA256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    }
    $inventory | Sort-Object File | Export-Csv -LiteralPath (Join-Path $releaseRoot 'files.csv') -Encoding utf8BOM

    Step 'Criando ZIP portable'
    $zip = Join-Path $releaseRoot "SwitchSaveAssistant-Portable-$version-win-x64.zip"
    Compress-Archive -LiteralPath $payload -DestinationPath $zip -CompressionLevel Optimal

    Step 'Compilando instalador'
    & $IsccPath "/DAppVersion=$version" "/DAppBinaryVersion=$binaryVersion" "/DPayloadDir=$payload" "/DProjectDir=$ProjectRoot" "/DOutputDir=$releaseRoot" $installer
    if ($LASTEXITCODE -ne 0) { throw 'Compilação do instalador falhou. O ZIP portable permanece na pasta de saída.' }
    $setup = Join-Path $releaseRoot "SwitchSaveAssistant-Setup-$version-win-x64.exe"
    if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) { throw 'Instalador não encontrado após a compilação.' }
    $hashes = foreach ($path in @($zip, $setup)) {
        (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($path)
    }
    $hashes | Set-Content -LiteralPath (Join-Path $releaseRoot 'SHA256SUMS.txt') -Encoding ascii
    Write-Host "`nPacotes de pré-lançamento gerados em: $releaseRoot" -ForegroundColor Green
    Write-Host 'Confira a abertura do portable e a instalação, atualização e desinstalação pelo Setup.'
    Write-Host 'Os executáveis ainda estão sem assinatura digital.'
    Invoke-Item -LiteralPath $releaseRoot
}
finally { Pop-Location }
