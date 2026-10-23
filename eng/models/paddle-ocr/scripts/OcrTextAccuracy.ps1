# Evaluation helper: ordinal Unicode-scalar CER and whitespace-token WER.
# No implicit lowercasing, trimming of character text, or punctuation removal.
function Get-OcrTextAccuracy {
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Reference,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Hypothesis
    )

    function Get-Scalars([string]$Text) {
        for ($index = 0; $index -lt $Text.Length; $index++) {
            if ([char]::IsHighSurrogate($Text[$index]) -and $index + 1 -lt $Text.Length -and [char]::IsLowSurrogate($Text[$index + 1])) {
                $Text.Substring($index, 2)
                $index++
            } else {
                $Text[$index].ToString()
            }
        }
    }

    function Get-TokenDistance([string[]]$ExpectedTokens, [string[]]$ActualTokens) {
        $previous = [int[]](0..$ActualTokens.Count)
        $current = [int[]]::new($ActualTokens.Count + 1)
        for ($row = 1; $row -le $ExpectedTokens.Count; $row++) {
            $current[0] = $row
            for ($column = 1; $column -le $ActualTokens.Count; $column++) {
                $cost = if ([string]::Equals($ExpectedTokens[$row - 1], $ActualTokens[$column - 1], [StringComparison]::Ordinal)) { 0 } else { 1 }
                $current[$column] = [Math]::Min([Math]::Min($previous[$column] + 1, $current[$column - 1] + 1), $previous[$column - 1] + $cost)
            }
            $swap = $previous; $previous = $current; $current = $swap
        }
        $previous[$ActualTokens.Count]
    }

    $referenceScalars = @(Get-Scalars $Reference)
    $hypothesisScalars = @(Get-Scalars $Hypothesis)
    $referenceWords = @([regex]::Split($Reference, '\s+') | Where-Object { $_.Length -gt 0 })
    $hypothesisWords = @([regex]::Split($Hypothesis, '\s+') | Where-Object { $_.Length -gt 0 })
    $characterEdits = Get-TokenDistance $referenceScalars $hypothesisScalars
    $wordEdits = Get-TokenDistance $referenceWords $hypothesisWords
    [pscustomobject]@{
        referenceCharacters = $referenceScalars.Count
        hypothesisCharacters = $hypothesisScalars.Count
        characterEditDistance = $characterEdits
        characterErrorRate = if ($referenceScalars.Count) { $characterEdits / [double]$referenceScalars.Count } elseif ($hypothesisScalars.Count) { 1.0 } else { 0.0 }
        referenceWords = $referenceWords.Count
        hypothesisWords = $hypothesisWords.Count
        wordEditDistance = $wordEdits
        wordErrorRate = if ($referenceWords.Count) { $wordEdits / [double]$referenceWords.Count } elseif ($hypothesisWords.Count) { 1.0 } else { 0.0 }
    }
}
