param(
    [string]$RepoRoot = ".",
    [switch]$CleanGeneratedSnapshots
)

$ErrorActionPreference = 'Stop'

function Get-Sha256Hex {
    param([string]$Text)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($Text)
        $hash = $sha.ComputeHash($bytes)
        return ([System.BitConverter]::ToString($hash) -replace '-', '').ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-SanitizedPathSegment {
    param([string]$Value)
    $invalid = [System.IO.Path]::GetInvalidFileNameChars()
    $builder = New-Object System.Text.StringBuilder
    foreach ($ch in $Value.ToCharArray()) {
        if ($invalid -contains $ch) { [void]$builder.Append('_') }
        else { [void]$builder.Append($ch) }
    }
    return $builder.ToString().Replace(' ', '_')
}

function ConvertTo-OrderedObject {
    param($InputObject)
    if ($null -eq $InputObject) { return $null }
    if ($InputObject -is [System.Collections.IDictionary]) {
        $ordered = [ordered]@{}
        foreach ($key in ($InputObject.Keys | Sort-Object)) {
            $ordered[$key] = ConvertTo-OrderedObject $InputObject[$key]
        }
        return $ordered
    }
    if ($InputObject -is [System.Collections.IEnumerable] -and -not ($InputObject -is [string])) {
        $list = New-Object System.Collections.ArrayList
        foreach ($item in $InputObject) { [void]$list.Add((ConvertTo-OrderedObject $item)) }
        return ,$list.ToArray()
    }
    return $InputObject
}

function Get-CanonicalJson {
    param($Object)
    $normalized = ConvertTo-OrderedObject $Object
    return ($normalized | ConvertTo-Json -Depth 100 -Compress)
}

function Get-ChangedFieldsSummary {
    param($Previous, $Current)
    if ($null -eq $Previous) { return 'Initial snapshot imported from git history.' }
    $prevProps = @{}
    foreach ($p in $Previous.PSObject.Properties) { $prevProps[$p.Name] = $p.Value }
    $currProps = @{}
    foreach ($p in $Current.PSObject.Properties) { $currProps[$p.Name] = $p.Value }
    $names = ($prevProps.Keys + $currProps.Keys | Sort-Object -Unique)
    $changed = @()
    foreach ($name in $names) {
        $prevJson = if ($prevProps.ContainsKey($name)) { Get-CanonicalJson $prevProps[$name] } else { '__MISSING__' }
        $currJson = if ($currProps.ContainsKey($name)) { Get-CanonicalJson $currProps[$name] } else { '__MISSING__' }
        if ($prevJson -ne $currJson) { $changed += $name }
    }
    if ($changed.Count -eq 0) { return 'No content change relative to previous snapshot.' }
    return ('Changed fields: ' + ($changed -join ', '))
}

function Get-CommitRecords {
    param([string]$PathSpec)
    $lines = git log --follow --reverse --format='%H|%cI|%s' -- $PathSpec
    $records = @()
    foreach ($line in $lines) {
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        $parts = $line.Split('|', 3)
        $records += [pscustomobject]@{
            Commit = $parts[0]
            CommitDate = [DateTimeOffset]::Parse($parts[1])
            Subject = $parts[2]
        }
    }
    return $records
}

function Resolve-PromptFunctionName {
    param([string]$StableId)
    switch ($StableId.ToLowerInvariant()) {
        'gatekeeper' { return 'Gatekeeper' }
        'extract_content_vision' { return 'ExtractedContent' }
        'auto_tagging' { return 'EmbeddingTagging' }
        'question_generation' { return 'QuestionGeneration' }
        'question_generation_repair' { return 'QuestionGenerationRepair' }
        'question_review' { return 'QuestionReview' }
        'code_mentor' { return 'CodeMentor' }
        default { return 'Unknown' }
    }
}

function Try-GetGitFileJson {
    param([string]$Commit, [string]$Path)
    $content = git show "$Commit`:$Path" 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace(($content -join "`n"))) { return $null }
    return ($content -join "`n") | ConvertFrom-Json
}

function Ensure-Snapshot {
    param(
        [string]$HistoryRoot,
        [string]$ArtifactType,
        [string]$StableId,
        [string]$Version,
        [string]$FunctionName,
        [string]$SubjectCode,
        [string]$QuestionType,
        [string]$Language,
        [string]$Description,
        [bool]$IsActive,
        [string]$Source,
        [string]$SourceCommit,
        [string]$CapturedFrom,
        [datetimeoffset]$CapturedAt,
        [string]$ChangeReason,
        [string]$ChangeSummary,
        $Content
    )

    $stableFolder = Join-Path $HistoryRoot (Join-Path $ArtifactType (Get-SanitizedPathSegment $StableId))
    New-Item -ItemType Directory -Force -Path $stableFolder | Out-Null

    $contentJson = Get-CanonicalJson $Content
    $contentHash = Get-Sha256Hex $contentJson
    $versionSanitized = Get-SanitizedPathSegment $Version
    $stamp = $CapturedAt.UtcDateTime.ToString('yyyyMMddHHmmssfff')
    $snapshotId = "$StableId`:$Version`:$stamp`:$($contentHash.Substring(0,12))"
    $fileName = "$stamp-$versionSanitized-$($contentHash.Substring(0,12)).json"
    $targetPath = Join-Path $stableFolder $fileName

    if (Test-Path $targetPath) {
        return [pscustomobject]@{ SnapshotPath = $targetPath; ContentHash = $contentHash; Created = $false }
    }

    $payload = [ordered]@{
        snapshot_id = $snapshotId
        artifact_type = $ArtifactType
        stable_id = $StableId
        version = $Version
        function_name = $FunctionName
        subject_code = $SubjectCode
        question_type = $QuestionType
        language = $Language
        description = $Description
        is_active = $IsActive
        source = $Source
        source_commit = $SourceCommit
        captured_from = $CapturedFrom
        captured_at = $CapturedAt.UtcDateTime.ToString('o')
        content_hash = $contentHash
        change_reason = $ChangeReason
        change_summary = $ChangeSummary
        content = (ConvertTo-OrderedObject $Content)
    }

    $payload | ConvertTo-Json -Depth 100 | Set-Content -Path $targetPath -Encoding UTF8
    return [pscustomobject]@{ SnapshotPath = $targetPath; ContentHash = $contentHash; Created = $true }
}

$repo = (Resolve-Path $RepoRoot).Path
Set-Location $repo
$appDataRoot = Join-Path $repo 'src-dotnet/Ape.AiModule.Api/App_Data'
$historyRoot = Join-Path $appDataRoot 'ai-config-history'
$reportRoot = Join-Path $repo 'Docs/Prototype_Document/Guides/Templates/AI_Report'
New-Item -ItemType Directory -Force -Path $historyRoot, $reportRoot | Out-Null

if ($CleanGeneratedSnapshots) {
    foreach ($folder in 'policies','rubrics','groundtruth') {
        $path = Join-Path $historyRoot $folder
        if (Test-Path $path) { Remove-Item -Recurse -Force $path }
    }
}

$changeRows = New-Object System.Collections.Generic.List[object]
$lastByArtifact = @{}

function Register-ChangeRow {
    param(
        [string]$ArtifactType,
        [string]$StableId,
        [string]$Version,
        [string]$FunctionName,
        [string]$SubjectCode,
        [string]$QuestionType,
        [string]$Language,
        [string]$Commit,
        [datetimeoffset]$CommitDate,
        [string]$CommitSubject,
        [string]$ChangeSummary,
        [string]$SnapshotPath,
        [string]$ContentHash
    )
    $changeRows.Add([pscustomobject]@{
        artifact_type = $ArtifactType
        stable_id = $StableId
        version = $Version
        function_name = $FunctionName
        subject_code = $SubjectCode
        question_type = $QuestionType
        language = $Language
        source_commit = $Commit
        commit_date_utc = $CommitDate.UtcDateTime.ToString('o')
        change_reason = $CommitSubject
        change_summary = $ChangeSummary
        content_hash = $ContentHash
        snapshot_path = $SnapshotPath.Replace($repo + [System.IO.Path]::DirectorySeparatorChar, '')
    }) | Out-Null
}

# prompts
$promptPath = 'src-dotnet/Ape.AiModule.Api/App_Data/ai-prompts.json'
foreach ($record in Get-CommitRecords $promptPath) {
    $items = Try-GetGitFileJson -Commit $record.Commit -Path $promptPath
    if ($null -eq $items) { continue }
    foreach ($item in $items) {
        $stableId = [string]$item.Key
        $artifactKey = "prompts::$stableId"
        $previous = if ($lastByArtifact.ContainsKey($artifactKey)) { $lastByArtifact[$artifactKey] } else { $null }
        $changeSummary = Get-ChangedFieldsSummary -Previous $previous -Current $item
        $functionName = Resolve-PromptFunctionName $stableId
        $snapshot = Ensure-Snapshot -HistoryRoot $historyRoot -ArtifactType 'prompts' -StableId $stableId -Version ([string]$item.Version) -FunctionName $functionName -SubjectCode $null -QuestionType $null -Language 'en' -Description ([string]$item.Description) -IsActive ([bool]$item.IsActive) -Source 'git_backfill' -SourceCommit $record.Commit -CapturedFrom $promptPath -CapturedAt $record.CommitDate -ChangeReason $record.Subject -ChangeSummary $changeSummary -Content ([ordered]@{
            key = [string]$item.Key
            version = [string]$item.Version
            description = [string]$item.Description
            system_prompt = [string]$item.SystemPrompt
            user_prompt = [string]$item.UserPrompt
            is_active = [bool]$item.IsActive
            updated_at = [string]$item.UpdatedAt
        })
        Register-ChangeRow -ArtifactType 'prompts' -StableId $stableId -Version ([string]$item.Version) -FunctionName $functionName -SubjectCode '' -QuestionType '' -Language 'en' -Commit $record.Commit -CommitDate $record.CommitDate -CommitSubject $record.Subject -ChangeSummary $changeSummary -SnapshotPath $snapshot.SnapshotPath -ContentHash $snapshot.ContentHash
        $lastByArtifact[$artifactKey] = $item
    }
}

# single-file json artifacts
$singleFileSpecs = @(
    @{ ArtifactType = 'policies'; Paths = @('src-dotnet/Ape.AiModule.Api/App_Data/gatekeeper-policy.json','src-dotnet/Ape.AiModule.Api/App_Data/extracted-content-policy.json','src-dotnet/Ape.AiModule.Api/App_Data/embedding-tagging-policy.json','src-dotnet/Ape.AiModule.Api/App_Data/code-mentor-policy.json') },
    @{ ArtifactType = 'rubrics'; Paths = @(Get-ChildItem (Join-Path $appDataRoot 'ai-rubrics') -Filter '*.json' | Where-Object { $_.Name -ne 'README.json' } | ForEach-Object { 'src-dotnet/Ape.AiModule.Api/App_Data/ai-rubrics/' + $_.Name }) },
    @{ ArtifactType = 'groundtruth'; Paths = @(Get-ChildItem (Join-Path $appDataRoot 'ai-groundtruth') -Filter '*.json' | Where-Object { $_.Name -ne 'README.json' } | ForEach-Object { 'src-dotnet/Ape.AiModule.Api/App_Data/ai-groundtruth/' + $_.Name }) }
)

foreach ($spec in $singleFileSpecs) {
    foreach ($path in $spec.Paths) {
        foreach ($record in Get-CommitRecords $path) {
            $item = Try-GetGitFileJson -Commit $record.Commit -Path $path
            if ($null -eq $item) { continue }
            $stableId = switch ($spec.ArtifactType) {
                'policies' { if ($item.policy_id) { [string]$item.policy_id } else { [System.IO.Path]::GetFileNameWithoutExtension($path) } }
                'rubrics' { if ($item.rubric_id) { [string]$item.rubric_id } else { [System.IO.Path]::GetFileNameWithoutExtension($path) } }
                'groundtruth' { if ($item.dataset_id) { [string]$item.dataset_id } else { [System.IO.Path]::GetFileNameWithoutExtension($path) } }
            }
            $version = if ($item.version) { [string]$item.version } else { 'v1' }
            $artifactKey = "$($spec.ArtifactType)::$stableId"
            $previous = if ($lastByArtifact.ContainsKey($artifactKey)) { $lastByArtifact[$artifactKey] } else { $null }
            $changeSummary = Get-ChangedFieldsSummary -Previous $previous -Current $item
            $functionName = if ($item.function_name) { [string]$item.function_name } elseif ($item.functionName) { [string]$item.functionName } else { '' }
            $subjectCode = if ($item.subject_code) { [string]$item.subject_code } else { '' }
            $questionType = if ($item.question_type) { [string]$item.question_type } else { '' }
            $language = if ($item.language) { [string]$item.language } else { '' }
            $description = if ($item.description) { [string]$item.description } elseif ($item.title) { [string]$item.title } else { $stableId }
            $isActive = if ($null -ne $item.is_active) { [bool]$item.is_active } elseif ($null -ne $item.isActive) { [bool]$item.isActive } else { $true }
            $snapshot = Ensure-Snapshot -HistoryRoot $historyRoot -ArtifactType $spec.ArtifactType -StableId $stableId -Version $version -FunctionName $functionName -SubjectCode $subjectCode -QuestionType $questionType -Language $language -Description $description -IsActive $isActive -Source 'git_backfill' -SourceCommit $record.Commit -CapturedFrom $path -CapturedAt $record.CommitDate -ChangeReason $record.Subject -ChangeSummary $changeSummary -Content $item
            Register-ChangeRow -ArtifactType $spec.ArtifactType -StableId $stableId -Version $version -FunctionName $functionName -SubjectCode $subjectCode -QuestionType $questionType -Language $language -Commit $record.Commit -CommitDate $record.CommitDate -CommitSubject $record.Subject -ChangeSummary $changeSummary -SnapshotPath $snapshot.SnapshotPath -ContentHash $snapshot.ContentHash
            $lastByArtifact[$artifactKey] = $item
        }
    }
}

$csvPath = Join-Path $reportRoot 'ai_config_version_changelog.csv'
$changeRows | Sort-Object artifact_type, stable_id, commit_date_utc | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8

$mdPath = Join-Path $repo 'Docs/Prototype_Document/Guides/Guide_AI_Config_Version_History.md'
$grouped = $changeRows | Sort-Object artifact_type, stable_id, commit_date_utc | Group-Object artifact_type, stable_id
$md = New-Object System.Collections.Generic.List[string]
$md.Add('# AI Config Version History') | Out-Null
$md.Add('') | Out-Null
$md.Add('Tai lieu nay tong hop lich su version da duoc backfill tu git cho prompt, policy, rubric va ground truth.') | Out-Null
$md.Add('') | Out-Null
$md.Add('## Pham vi va gioi han') | Out-Null
$md.Add('') | Out-Null
$md.Add('- Nguon khoi phuc la git history hien con trong repo.') | Out-Null
$md.Add('- Neu mot artifact truoc day da tung thay doi nhung chua tung duoc commit, thi khong the khoi phuc lai chinh xac.') | Out-Null
$md.Add('- Policy va ground truth hien chi co ban khoi tao trong git, nen lich su cua hai nhom nay con rat ngan.') | Out-Null
$md.Add('- Mot so rubric da thay doi noi dung nhung version trong JSON chua duoc tang; vi vay can doc dong thoi `change_reason` va `change_summary`, khong chi nhin `version`.') | Out-Null
$md.Add('') | Out-Null
$md.Add('## File xuat dung cho report') | Out-Null
$md.Add('') | Out-Null
$md.Add('- CSV: `Docs/Prototype_Document/Guides/Templates/AI_Report/ai_config_version_changelog.csv`') | Out-Null
$md.Add('- Snapshot history: `src-dotnet/Ape.AiModule.Api/App_Data/ai-config-history/`') | Out-Null
$md.Add('') | Out-Null
foreach ($group in $grouped) {
    $artifactType, $stableId = $group.Name -split ', '
    $md.Add("## $artifactType / $stableId") | Out-Null
    $md.Add('') | Out-Null
    $md.Add('| Version | Commit Date (UTC) | Commit | Reason | Summary |') | Out-Null
    $md.Add('|---|---|---|---|---|') | Out-Null
    foreach ($row in $group.Group) {
        $shortCommit = $row.source_commit.Substring(0, 7)
        $reason = ($row.change_reason -replace '\|', '/').Replace("`r", ' ').Replace("`n", ' ')
        $summary = ($row.change_summary -replace '\|', '/').Replace("`r", ' ').Replace("`n", ' ')
        $md.Add("| $($row.version) | $($row.commit_date_utc) | $shortCommit | $reason | $summary |") | Out-Null
    }
    $md.Add('') | Out-Null
}
Set-Content -Path $mdPath -Value $md -Encoding UTF8

Write-Output "Backfill complete. CSV: $csvPath"
Write-Output "Guide: $mdPath"
