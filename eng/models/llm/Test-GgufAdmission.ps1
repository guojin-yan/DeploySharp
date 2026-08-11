[CmdletBinding()]
param(
    [string]$WarehouseRoot = 'E:\DeploySharp-Models',
    [string]$ModelPath = $env:DEPLOYSHARP_LLAMA_MODEL,
    [string]$ManifestPath = $env:DEPLOYSHARP_LLAMA_ADMISSION_MANIFEST,
    [switch]$RequireAdmitted
)

$ErrorActionPreference = 'Stop'

function Get-ObjectValue([object]$Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Get-ExtensionValue([object]$Artifact, [string]$Name) {
    return Get-ObjectValue (Get-ObjectValue $Artifact 'extensions') $Name
}

function Test-Meaningful([object]$Value) {
    if ($null -eq $Value) { return $false }
    $text = [string]$Value
    if ([string]::IsNullOrWhiteSpace($text)) { return $false }

    return @('unknown', 'unverified', 'blocked', 'missing', 'none', 'model-specific', 'caller-owned-unverified') -notcontains $text.Trim().ToLowerInvariant()
}

function Resolve-ContainedFile([string]$Root, [string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [IO.Path]::IsPathRooted($RelativePath)) { return $null }
    try {
        $rootFullPath = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        $fileFullPath = [IO.Path]::GetFullPath((Join-Path $Root $RelativePath))
        if (-not $fileFullPath.StartsWith($rootFullPath, [StringComparison]::OrdinalIgnoreCase)) { return $null }
        return $fileFullPath
    }
    catch {
        return $null
    }
}

function Test-GgufMagic([string]$Path) {
    try {
        $stream = [IO.File]::OpenRead($Path)
        try {
            $bytes = [byte[]]::new(4)
            if ($stream.Read($bytes, 0, $bytes.Length) -ne $bytes.Length) { return $false }
            return [Text.Encoding]::ASCII.GetString($bytes) -ceq 'GGUF'
        }
        finally {
            $stream.Dispose()
        }
    }
    catch {
        return $false
    }
}

function Write-AdmissionResult([string]$Status, [string]$Reason, [string[]]$Missing, [string]$Model, [int]$Candidates) {
    $missingValue = if ($Missing.Count -eq 0) { 'none' } else { $Missing -join ',' }
    $modelValue = if ([string]::IsNullOrWhiteSpace($Model)) { 'none' } else { $Model }
    Write-Output "DEPLOYSHARP_LLAMA_ADMISSION_$($Status.ToUpperInvariant()) reason=$Reason candidates=$Candidates model=$modelValue missing=$missingValue"
    if ($RequireAdmitted -and $Status -ne 'admitted') {
        throw "The exact GGUF admission is not complete: $Reason."
    }
}

$warehouseCandidates = @()
if (Test-Path -LiteralPath $WarehouseRoot -PathType Container) {
    $warehouseCandidates = @(Get-ChildItem -LiteralPath $WarehouseRoot -Recurse -File -Filter '*.gguf' | ForEach-Object { $_.FullName } | Sort-Object)
}

$candidateCount = $warehouseCandidates.Count
$selectedModel = $null
if (-not [string]::IsNullOrWhiteSpace($ModelPath)) {
    if (-not (Test-Path -LiteralPath $ModelPath -PathType Leaf)) {
        Write-AdmissionResult 'blocked' 'configured-model-file-missing' @('exact-gguf') $ModelPath $candidateCount
        return
    }

    $selectedModel = (Resolve-Path -LiteralPath $ModelPath).Path
    if (-not $selectedModel.EndsWith('.gguf', [StringComparison]::OrdinalIgnoreCase)) {
        Write-AdmissionResult 'blocked' 'configured-model-is-not-gguf' @('exact-gguf') $selectedModel $candidateCount
        return
    }
}
elseif ($candidateCount -eq 0) {
    Write-AdmissionResult 'blocked' 'missing-exact-gguf' @('exact-gguf', 'source', 'license', 'tokenizer-chat-template', 'generation-context', 'sha256', 'runtime-evidence') $null $candidateCount
    return
}
elseif ($candidateCount -gt 1) {
    Write-AdmissionResult 'blocked' 'exact-selection-required' @('DEPLOYSHARP_LLAMA_MODEL') $null $candidateCount
    return
}
else {
    $selectedModel = $warehouseCandidates[0]
}

if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    Write-AdmissionResult 'blocked' 'missing-admission-manifest' @('DEPLOYSHARP_LLAMA_ADMISSION_MANIFEST') $selectedModel $candidateCount
    return
}

if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    Write-AdmissionResult 'blocked' 'admission-manifest-file-missing' @('ModelPack-manifest') $selectedModel $candidateCount
    return
}

try {
    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
}
catch {
    Write-AdmissionResult 'blocked' 'admission-manifest-invalid-json' @('valid-ModelPack-manifest') $selectedModel $candidateCount
    return
}

$missingMetadata = [System.Collections.Generic.List[string]]::new()
if ((Get-ObjectValue $manifest 'schemaVersion') -ne '2.0') { $missingMetadata.Add('schemaVersion-2.0') }

$source = Get-ObjectValue $manifest 'source'
$sourceUrl = [string](Get-ObjectValue $source 'sourceUrl')
$sourceUri = $null
if (-not [Uri]::TryCreate($sourceUrl, [UriKind]::Absolute, [ref]$sourceUri) -or $sourceUri.Scheme -ne 'https' -or $sourceUrl -match 'SciSharp/LLamaSharp') {
    $missingMetadata.Add('exact-upstream-source')
}
foreach ($field in @('projectUrl', 'revision', 'licenseExpression')) {
    if (-not (Test-Meaningful (Get-ObjectValue $source $field))) { $missingMetadata.Add("source-$field") }
}
$sourceRevision = [string](Get-ObjectValue $source 'revision')
$projectUrl = [string](Get-ObjectValue $source 'projectUrl')
if ((Test-Meaningful $sourceRevision) -and ($sourceUrl.IndexOf($sourceRevision, [StringComparison]::Ordinal) -lt 0 -or $projectUrl.IndexOf($sourceRevision, [StringComparison]::Ordinal) -lt 0)) {
    $missingMetadata.Add('immutable-source-revision-binding')
}

$ggufArtifacts = @((Get-ObjectValue $manifest 'artifacts') | Where-Object { (Get-ObjectValue $_ 'format') -eq 'gguf' })
if ($ggufArtifacts.Count -ne 1) {
    $missingMetadata.Add('single-gguf-artifact')
    Write-AdmissionResult 'blocked' 'admission-metadata-incomplete' $missingMetadata.ToArray() $selectedModel $candidateCount
    return
}

$artifact = $ggufArtifacts[0]
if (@(Get-ObjectValue $artifact 'compatibleBackends') -notcontains 'llamasharp') { $missingMetadata.Add('llamasharp-backend') }
if (-not (Test-Meaningful (Get-ObjectValue $artifact 'quantization'))) { $missingMetadata.Add('quantization') }
if ((Get-ObjectValue $artifact 'locationKind') -ne 'file') { $missingMetadata.Add('gguf-location-kind-file') }
if (-not (Test-GgufMagic $selectedModel)) { $missingMetadata.Add('gguf-magic') }

$actualLength = (Get-Item -LiteralPath $selectedModel).Length
$actualHash = (Get-FileHash -LiteralPath $selectedModel -Algorithm SHA256).Hash.ToLowerInvariant()
$modelDirectory = [IO.Path]::GetDirectoryName($selectedModel)
$entrypointPath = Resolve-ContainedFile $modelDirectory ([string](Get-ObjectValue $artifact 'entrypoint'))
if ([string]::IsNullOrWhiteSpace($entrypointPath) -or -not [string]::Equals($entrypointPath, $selectedModel, [StringComparison]::OrdinalIgnoreCase)) {
    $missingMetadata.Add('exact-gguf-entrypoint')
}

$artifactFiles = @((Get-ObjectValue $artifact 'files'))
foreach ($artifactFile in $artifactFiles) {
    $relativePath = [string](Get-ObjectValue $artifactFile 'relativePath')
    $fullPath = Resolve-ContainedFile $modelDirectory $relativePath
    $expectedHash = [string](Get-ObjectValue $artifactFile 'sha256')
    $expectedSize = Get-ObjectValue $artifactFile 'size'
    $parsedSize = [int64]0
    if ([string]::IsNullOrWhiteSpace($fullPath) -or $expectedHash -notmatch '^[0-9a-fA-F]{64}$' -or -not [int64]::TryParse([string]$expectedSize, [ref]$parsedSize) -or $parsedSize -lt 0 -or -not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        $missingMetadata.Add("artifact-file:$relativePath")
        continue
    }

    $fileLength = (Get-Item -LiteralPath $fullPath).Length
    $fileHash = if ([string]::Equals($fullPath, $selectedModel, [StringComparison]::OrdinalIgnoreCase)) { $actualHash } else { (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant() }
    if ($fileLength -ne $parsedSize -or $fileHash -ine $expectedHash) { $missingMetadata.Add("artifact-file-size-sha256:$relativePath") }
}

$modelFiles = @((Get-ObjectValue $artifact 'files') | Where-Object { (Get-ObjectValue $_ 'role') -eq 'model' })
$matchingFile = @($modelFiles | Where-Object {
    $recordPath = Resolve-ContainedFile $modelDirectory ([string](Get-ObjectValue $_ 'relativePath'))
    [string]::Equals($recordPath, $selectedModel, [StringComparison]::OrdinalIgnoreCase) -and [int64](Get-ObjectValue $_ 'size') -eq $actualLength -and [string](Get-ObjectValue $_ 'sha256') -ieq $actualHash
})
if ($matchingFile.Count -ne 1) { $missingMetadata.Add('model-file-size-sha256') }

$licenseFile = [string](Get-ObjectValue $source 'licenseFile')
$licenseRecords = @($artifactFiles | Where-Object { (Get-ObjectValue $_ 'role') -eq 'license' -and [string](Get-ObjectValue $_ 'relativePath') -ieq $licenseFile })
if (-not (Test-Meaningful $licenseFile) -or $licenseRecords.Count -ne 1) { $missingMetadata.Add('source-license-file-binding') }

$manifestModelPath = [string](Get-ExtensionValue $artifact 'deploysharp.model-path')
try {
    if ([string]::IsNullOrWhiteSpace($manifestModelPath) -or -not [string]::Equals([IO.Path]::GetFullPath($manifestModelPath), $selectedModel, [StringComparison]::OrdinalIgnoreCase)) {
        $missingMetadata.Add('exact-model-path')
    }
}
catch {
    $missingMetadata.Add('exact-model-path')
}
if ([string](Get-ExtensionValue $artifact 'deploysharp.model-file-size') -ne [string]$actualLength -or [string](Get-ExtensionValue $artifact 'deploysharp.model-sha256') -ine $actualHash) {
    $missingMetadata.Add('model-extension-size-sha256')
}

$contextLength = [string](Get-ExtensionValue $artifact 'deploysharp.context-length')
$parsedContext = 0
if (-not [int]::TryParse($contextLength, [ref]$parsedContext) -or $parsedContext -le 0) { $missingMetadata.Add('context-length') }
foreach ($field in @(
    'deploysharp.bos-eos-pad',
    'deploysharp.tokenizer-identity',
    'deploysharp.chat-template-identity',
    'deploysharp.generation-identity',
    'deploysharp.embedding-capability',
    'deploysharp.license-status',
    'deploysharp.llamasharp-version',
    'deploysharp.native-runtime-package',
    'deploysharp.native-runtime-version')) {
    if (-not (Test-Meaningful (Get-ExtensionValue $artifact $field))) { $missingMetadata.Add($field) }
}

if ($missingMetadata.Count -gt 0) {
    Write-AdmissionResult 'blocked' 'admission-metadata-incomplete' $missingMetadata.ToArray() $selectedModel $candidateCount
    return
}

$runtimeMissing = [System.Collections.Generic.List[string]]::new()
if ([string](Get-ExtensionValue $artifact 'deploysharp.executable') -ne 'true') { $runtimeMissing.Add('deploysharp.executable=true') }
$runtimeEvidencePath = [string](Get-ExtensionValue $artifact 'deploysharp.runtime-evidence-path')
$runtimeEvidenceHash = [string](Get-ExtensionValue $artifact 'deploysharp.runtime-evidence-sha256')
$runtimeOperations = [string](Get-ExtensionValue $artifact 'deploysharp.runtime-evidence-operations')
foreach ($field in @('deploysharp.runtime-evidence-path', 'deploysharp.runtime-evidence-sha256', 'deploysharp.runtime-evidence-operations')) {
    if (-not (Test-Meaningful (Get-ExtensionValue $artifact $field))) { $runtimeMissing.Add($field) }
}

if ($runtimeMissing.Count -gt 0) {
    Write-AdmissionResult 'ready' 'metadata-complete-runtime-evidence-required' $runtimeMissing.ToArray() $selectedModel $candidateCount
    return
}

$runtimeInvalid = [System.Collections.Generic.List[string]]::new()
$runtimeEvidence = $null
if (-not (Test-Path -LiteralPath $runtimeEvidencePath -PathType Leaf)) {
    $runtimeInvalid.Add('runtime-evidence-file')
}
else {
    try {
        $evidenceRoot = [IO.Path]::GetFullPath((Join-Path $modelDirectory 'evidence')).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
        $runtimeEvidenceFullPath = [IO.Path]::GetFullPath($runtimeEvidencePath)
        if (-not $runtimeEvidenceFullPath.StartsWith($evidenceRoot, [StringComparison]::OrdinalIgnoreCase)) { $runtimeInvalid.Add('runtime-evidence-model-directory') }
    }
    catch {
        $runtimeInvalid.Add('runtime-evidence-model-directory')
    }

    if ($runtimeEvidenceHash -notmatch '^[0-9a-fA-F]{64}$' -or (Get-FileHash -LiteralPath $runtimeEvidencePath -Algorithm SHA256).Hash -ine $runtimeEvidenceHash) {
        $runtimeInvalid.Add('runtime-evidence-sha256')
    }

    try {
        $runtimeEvidence = Get-Content -LiteralPath $runtimeEvidencePath -Raw | ConvertFrom-Json
    }
    catch {
        $runtimeInvalid.Add('runtime-evidence-json')
    }
}

$requiredOperations = @('cpu-generate', 'stream', 'cancel', 'repeat', 'contention', 'dispose')
$reportedOperations = @($runtimeOperations.Split(',', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { $_.Trim().ToLowerInvariant() })
foreach ($operation in $requiredOperations) {
    if ($reportedOperations -notcontains $operation) { $runtimeInvalid.Add("runtime-$operation") }
}
if (($reportedOperations -notcontains 'embedding') -and ($reportedOperations -notcontains 'embedding-unsupported')) { $runtimeInvalid.Add('runtime-embedding') }

if ($null -ne $runtimeEvidence) {
    $evidenceModel = Get-ObjectValue $runtimeEvidence 'model'
    $evidenceFacts = Get-ObjectValue $evidenceModel 'facts'
    $managedRuntime = Get-ObjectValue $runtimeEvidence 'managedRuntime'
    $nativeRuntime = Get-ObjectValue $runtimeEvidence 'nativeRuntime'
    $operations = Get-ObjectValue $runtimeEvidence 'operations'
    try {
        if (-not [string]::Equals([IO.Path]::GetFullPath([string](Get-ObjectValue $evidenceModel 'path')), $selectedModel, [StringComparison]::OrdinalIgnoreCase)) { $runtimeInvalid.Add('runtime-evidence-model-path') }
    }
    catch {
        $runtimeInvalid.Add('runtime-evidence-model-path')
    }
    if ([int64](Get-ObjectValue $evidenceModel 'size') -ne $actualLength -or [string](Get-ObjectValue $evidenceModel 'sha256') -ine $actualHash -or [string](Get-ObjectValue $evidenceModel 'magic') -cne 'GGUF') {
        $runtimeInvalid.Add('runtime-evidence-model-identity')
    }
    if ([int](Get-ObjectValue $evidenceFacts 'contextLength') -ne $parsedContext -or [int](Get-ObjectValue $evidenceFacts 'embeddingSize') -lt 0 -or [int](Get-ObjectValue $evidenceFacts 'vocabularySize') -le 0) {
        $runtimeInvalid.Add('runtime-evidence-model-facts')
    }
    if ([string](Get-ObjectValue $managedRuntime 'backend') -ne 'llamasharp' -or [string](Get-ObjectValue $managedRuntime 'version') -ne [string](Get-ExtensionValue $artifact 'deploysharp.llamasharp-version')) {
        $runtimeInvalid.Add('runtime-evidence-managed-runtime')
    }
    if ([string](Get-ObjectValue $nativeRuntime 'package') -ne [string](Get-ExtensionValue $artifact 'deploysharp.native-runtime-package') -or [string](Get-ObjectValue $nativeRuntime 'version') -ne [string](Get-ExtensionValue $artifact 'deploysharp.native-runtime-version') -or [int](Get-ObjectValue $nativeRuntime 'gpuLayerCount') -ne 0 -or -not (Test-Meaningful (Get-ObjectValue $nativeRuntime 'llamaCppRevision'))) {
        $runtimeInvalid.Add('runtime-evidence-native-runtime')
    }

    $evidenceOperations = @((Get-ObjectValue $runtimeEvidence 'runtimeEvidenceOperations') | ForEach-Object { ([string]$_).Trim().ToLowerInvariant() })
    foreach ($operation in $reportedOperations) {
        if ($evidenceOperations -notcontains $operation) { $runtimeInvalid.Add("runtime-evidence-operation:$operation") }
    }

    $generate = Get-ObjectValue $operations 'cpuGenerate'
    $stream = Get-ObjectValue $operations 'stream'
    $cancel = Get-ObjectValue $operations 'cancel'
    $repeat = Get-ObjectValue $operations 'repeat'
    $contention = Get-ObjectValue $operations 'contention'
    $dispose = Get-ObjectValue $operations 'dispose'
    $embedding = Get-ObjectValue $operations 'embedding'
    if (-not (Test-Meaningful (Get-ObjectValue $generate 'Text')) -or [int](Get-ObjectValue $generate 'GeneratedTokens') -le 0 -or [string](Get-ObjectValue $generate 'textSha256') -notmatch '^[0-9a-fA-F]{64}$') { $runtimeInvalid.Add('runtime-evidence-generate') }
    if ([int](Get-ObjectValue $stream 'chunks') -le 1 -or -not (Test-Meaningful (Get-ObjectValue $stream 'terminal'))) { $runtimeInvalid.Add('runtime-evidence-stream') }
    if ([string](Get-ObjectValue $cancel 'terminal') -ne 'Cancelled') { $runtimeInvalid.Add('runtime-evidence-cancel') }
    if ((Get-ObjectValue $repeat 'identical') -ne $true -or [string](Get-ObjectValue $repeat 'textSha256') -ine [string](Get-ObjectValue $generate 'textSha256')) { $runtimeInvalid.Add('runtime-evidence-repeat') }
    if ([string](Get-ObjectValue $contention 'errorCode') -ne 'DS-LLM-4004') { $runtimeInvalid.Add('runtime-evidence-contention') }
    if ((Get-ObjectValue $dispose 'idempotent') -ne $true -or [string](Get-ObjectValue $dispose 'useAfterDispose') -ne 'ObjectDisposedException') { $runtimeInvalid.Add('runtime-evidence-dispose') }
    if ($reportedOperations -contains 'embedding') {
        if ([string](Get-ObjectValue $embedding 'operation') -ne 'embedding' -or [int](Get-ObjectValue $embedding 'dimensions') -le 0 -or (Get-ObjectValue $embedding 'normalized') -ne $true -or [string](Get-ObjectValue $embedding 'sha256') -notmatch '^[0-9a-fA-F]{64}$') { $runtimeInvalid.Add('runtime-evidence-embedding') }
    }
    elseif ([string](Get-ObjectValue $embedding 'operation') -ne 'embedding-unsupported') {
        $runtimeInvalid.Add('runtime-evidence-embedding-unsupported')
    }
}

if ($runtimeInvalid.Count -gt 0) {
    Write-AdmissionResult 'blocked' 'runtime-evidence-invalid' $runtimeInvalid.ToArray() $selectedModel $candidateCount
    return
}

Write-AdmissionResult 'admitted' 'exact-gguf-evidence-complete' @() $selectedModel $candidateCount
