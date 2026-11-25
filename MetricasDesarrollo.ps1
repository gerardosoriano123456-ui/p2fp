$ErrorActionPreference = 'Stop'

function Get-CodeLineCount([string] $Path) {
    $count = 0
    $insideBlockComment = $false

    foreach ($line in Get-Content -LiteralPath $Path) {
        $text = $line
        if ($insideBlockComment) {
            if ($text -match '\*/') {
                $text = $text.Substring($Matches[0].Length + $text.IndexOf('*/'))
                $insideBlockComment = $false
            }
            else {
                continue
            }
        }

        while ($text -match '/\*') {
            $start = $text.IndexOf('/*')
            $end = $text.IndexOf('*/', $start + 2)
            if ($end -ge 0) {
                $text = $text.Remove($start, $end + 2 - $start)
            }
            else {
                $text = $text.Substring(0, $start)
                $insideBlockComment = $true
                break
            }
        }

        $text = $text -replace '//.*$', ''
        if (-not [string]::IsNullOrWhiteSpace($text)) {
            $count++
        }
    }

    return $count
}

$adoFiles = @(
    Get-ChildItem (Join-Path $PSScriptRoot 'src\TechStore.Datos.Ado') -Filter '*RepositorioAdo.cs' -File
)
$efFiles = @(
    Get-ChildItem (Join-Path $PSScriptRoot 'src\TechStore.Datos.Ef') -Filter '*RepositorioEf.cs' -File
)

$adoLines = ($adoFiles | ForEach-Object { Get-CodeLineCount $_.FullName } | Measure-Object -Sum).Sum
$efLines = ($efFiles | ForEach-Object { Get-CodeLineCount $_.FullName } | Measure-Object -Sum).Sum

$rows = @(
    [pscustomobject]@{
        Metrica = 'Lineas de codigo de los repositorios (sin comentarios ni lineas vacias)'
        ADO_NET = $adoLines
        EF_Core = $efLines
        Notas = "ADO.NET: $($adoFiles.Count) archivos; EF Core: $($efFiles.Count) archivos"
    }
    [pscustomobject]@{
        Metrica = 'Tiempo de desarrollo (horas)'
        ADO_NET = 'No desglosado'
        EF_Core = 'No desglosado'
        Notas = '68 horas totales declaradas por el estudiante; tercer día de trabajo aún en curso'
    }
    [pscustomobject]@{
        Metrica = 'Errores encontrados al compilar o ejecutar'
        ADO_NET = 'Sin errores en build Release final'
        EF_Core = 'Sin errores en build Release final'
        Notas = '1) Dos intentos iniciales del benchmark expiraron por timeout SQL; se optimizó la consulta y se configuró timeout de 180 s. 2) La primera ronda tuvo latencias atípicas y se repitió. Suite final completa; build: 0 errores, 0 advertencias.'
    }
    [pscustomobject]@{
        Metrica = 'Cambio de esquema Marca: archivos y lineas modificadas'
        ADO_NET = 'Cambio reflejado en consultas y esquema SQL'
        EF_Core = 'Cambio reflejado en modelo y migración'
        Notas = 'Marca aparece en 10 archivos C# y 2 scripts SQL. No es posible contar líneas del diff sin una copia previa o historial Git.'
    }
)

$outputPath = Join-Path $PSScriptRoot 'MetricasDesarrollo.csv'
$rows | Export-Csv -LiteralPath $outputPath -NoTypeInformation -Encoding UTF8
$rows | Format-Table -AutoSize
Write-Output "Metricas guardadas en: $outputPath"
