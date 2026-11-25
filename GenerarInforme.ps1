param(
    [string] $NombreEstudiante = 'Gerardo Soriano Delance',
    [string] $Matricula = '20252481',
    [string] $Asignatura = 'Acceso a Datos en .NET',
    [string] $Docente = 'Eduandy Isabel Cruz'
)

$ErrorActionPreference = 'Stop'
$resultadosPath = Join-Path $PSScriptRoot 'ResultadosRendimiento.csv'
$metricasPath = Join-Path $PSScriptRoot 'MetricasDesarrollo.csv'
if (-not (Test-Path -LiteralPath $resultadosPath)) {
    throw 'Faltan ResultadosRendimiento.csv. Ejecuta primero la opción 4 en Release.'
}
if (-not (Test-Path -LiteralPath $metricasPath)) {
    throw 'Faltan MetricasDesarrollo.csv. Ejecuta primero MetricasDesarrollo.ps1.'
}

$resultados = Import-Csv -LiteralPath $resultadosPath
$metricas = Import-Csv -LiteralPath $metricasPath
$procesador = (Get-CimInstance Win32_Processor | Select-Object -First 1 -ExpandProperty Name)
$memoriaGb = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 1)
$sqlcmd = 'C:\Program Files\Microsoft SQL Server\Client SDK\ODBC\180\Tools\Binn\SQLCMD.EXE'
$versionSql = if (Test-Path -LiteralPath $sqlcmd) {
    $versionOutput = & $sqlcmd -S localhost -E -C -W -h -1 -Q 'SELECT @@VERSION'
    if ($LASTEXITCODE -ne 0 -or -not $versionOutput) {
        throw 'No se pudo consultar la versión de SQL Server en localhost.'
    }
    ($versionOutput | Select-Object -First 1).Trim()
}
else {
    'Completar versión de SQL Server'
}

function Encode-Html {
    param([string] $Value)
    [System.Net.WebUtility]::HtmlEncode($Value)
}

$resultRows = ($resultados | ForEach-Object {
    "<tr><td>$(Encode-Html $_.Escenario)</td><td>$($_.ADO_NET_ms)</td><td>$($_.EF_Variante_1_ms)</td><td>$($_.EF_Variante_2_ms)</td></tr>"
}) -join "`n"
$metricRows = ($metricas | ForEach-Object {
    "<tr><td>$(Encode-Html $_.Metrica)</td><td>$(Encode-Html $_.ADO_NET)</td><td>$(Encode-Html $_.EF_Core)</td><td>$(Encode-Html $_.Notas)</td></tr>"
}) -join "`n"

$series = @(
    @{ Key = 'ADO_NET_ms'; Label = 'ADO.NET'; Color = '#2563eb' },
    @{ Key = 'EF_Variante_1_ms'; Label = 'EF Core variante 1'; Color = '#16a34a' },
    @{ Key = 'EF_Variante_2_ms'; Label = 'EF Core variante 2'; Color = '#f97316' }
)
$numberStyle = [System.Globalization.NumberStyles]::Float
$invariantCulture = [System.Globalization.CultureInfo]::InvariantCulture
$numericValues = foreach ($row in $resultados) {
    foreach ($serie in $series) {
        $number = 0.0
        if ([double]::TryParse($row.($serie.Key), $numberStyle, $invariantCulture, [ref]$number)) { $number }
    }
}
$maxValue = [math]::Max(1, ($numericValues | Measure-Object -Maximum).Maximum)
$chart = [System.Text.StringBuilder]::new()
$chartY = 24
foreach ($row in $resultados) {
    [void]$chart.AppendLine("<text x='4' y='$($chartY + 13)' font-size='10'>$(Encode-Html $row.Escenario)</text>")
    $barY = $chartY + 18
    foreach ($serie in $series) {
        $number = 0.0
        if ([double]::TryParse($row.($serie.Key), $numberStyle, $invariantCulture, [ref]$number)) {
            $width = [math]::Max(1, [math]::Round(560 * $number / $maxValue, 1))
            [void]$chart.AppendLine("<rect x='165' y='$barY' width='$width' height='9' fill='$($serie.Color)' />")
            [void]$chart.AppendLine("<text x='$([math]::Min(735, 171 + $width))' y='$($barY + 8)' font-size='9'>$([math]::Round($number, 2)) ms</text>")
        }
        $barY += 11
    }
    $chartY += 58
}
$legend = ($series | ForEach-Object {
    "<span><i style='background:$($_.Color)'></i>$($_.Label)</span>"
}) -join "`n"

$document = @"
<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<title>Evaluación comparativa: ADO.NET vs. EF Core</title>
<style>
@page { size: A4; margin: 18mm 17mm; }
* { box-sizing: border-box; }
body { font: 10.5pt/1.45 Arial, sans-serif; color: #172033; margin: 0; }
h1 { color: #12355b; font-size: 24pt; margin: 0 0 8mm; }
h2 { color: #12355b; font-size: 17pt; border-bottom: 1px solid #91a9c2; padding-bottom: 3mm; }
h3 { color: #1b4c7a; font-size: 12pt; margin: 4mm 0 2mm; }
p { margin: 2mm 0 3mm; }
ul { margin-top: 2mm; }
.page { min-height: 255mm; page-break-after: always; }
.cover { display: flex; flex-direction: column; justify-content: center; padding: 18mm; background: #f0f5fa; }
.cover h1 { font-size: 29pt; }
.subtitle { font-size: 16pt; color: #41617e; }
.meta { margin-top: 20mm; line-height: 2; }
.muted { color: #58677a; font-size: 9pt; }
table { border-collapse: collapse; width: 100%; margin: 4mm 0; font-size: 8.5pt; }
th { background: #173c60; color: white; text-align: left; }
td, th { border: 1px solid #c6d1dd; padding: 2mm; vertical-align: top; }
tr:nth-child(even) { background: #f3f6f9; }
.callout { background: #edf4fa; border-left: 4px solid #2670a9; padding: 4mm; margin: 4mm 0; }
.fill { color: #7a3d0c; background: #fff3d8; padding: 2mm; }
.legend { display: flex; gap: 8mm; margin: 4mm 0; font-size: 8.5pt; }
.legend i { display: inline-block; width: 10px; height: 10px; margin-right: 2mm; }
svg { width: 100%; height: auto; }
a { color: #185b91; }
footer { margin-top: 8mm; color: #68778a; font-size: 8pt; }
</style>
</head>
<body>
<section class="page cover">
<p class="subtitle">Evaluación comparativa aplicada</p>
<h1>TechStore<br>ADO.NET vs. Entity Framework Core</h1>
<p>Comparación de dos implementaciones equivalentes de acceso a datos con SQL Server.</p>
<div class="meta">
<div><b>Estudiante:</b> $(Encode-Html $NombreEstudiante)</div>
<div><b>Matrícula:</b> $(Encode-Html $Matricula)</div>
<div><b>Asignatura:</b> $(Encode-Html $Asignatura)</div>
<div><b>Docente:</b> $(Encode-Html $Docente)</div>
<div><b>Fecha:</b> $(Get-Date -Format 'dd/MM/yyyy')</div>
</div>
<footer>Documento generado a partir de las mediciones del equipo local y la información proporcionada por el estudiante.</footer>
</section>

<section class="page">
<h2>1. Introducción y objetivo</h2>
<p>ADO.NET permite ejecutar SQL explícito y mapear manualmente los resultados; Entity Framework Core es un ORM que permite consultar entidades con LINQ y traduce esas expresiones a SQL. La práctica compara ambas alternativas en el mismo módulo TechStore para observar rendimiento, cantidad de código, transacciones y mantenimiento.</p>
<p>El objetivo experimental es comparar operaciones equivalentes sobre SQL Server, con 50,000 productos, 5 categorías, 100 clientes y 1,000 ventas. Las cifras de rendimiento incluidas provienen del CSV generado por las pruebas en esta computadora; no son valores de referencia de internet.</p>
<h2>2. Arquitectura</h2>
<table><tr><th>Componente</th><th>Responsabilidad</th></tr>
<tr><td>TechStore.Dominio</td><td>Entidades y contratos comunes de producto y venta.</td></tr>
<tr><td>TechStore.Datos.Ado</td><td>SqlConnection, comandos parametrizados, SqlDataReader y SqlTransaction.</td></tr>
<tr><td>TechStore.Datos.Ef</td><td>DbContext, configuración Fluent API, LINQ y migraciones Code First.</td></tr>
<tr><td>TechStore.DatabaseFirst</td><td>Entidades y contexto generados con Scaffold-DbContext desde TechStoreDB.</td></tr>
<tr><td>TechStore.Consola</td><td>Menú de pruebas, demostración de Rollback y mediciones.</td></tr></table>
<div class="callout">Ambos repositorios de productos implementan el mismo contrato. ADO.NET se conecta a TechStoreDB; EF Core usa TechStoreDB_EF, creado con la migración inicial y cargado con los mismos registros.</div>
<h3>Modelo de datos</h3>
<p>El esquema contiene Categoria, Producto, Cliente, Venta y DetalleVenta. Incluye claves primarias y foráneas, restricciones UNIQUE y CHECK, e identidad. El atributo Marca en Producto se agregó para documentar el cambio de esquema pedido en las métricas.</p>
</section>

<section class="page">
<h2>3. Implementación y metodología</h2>
<h3>ADO.NET</h3>
<p>El repositorio usa consultas SQL con parámetros, mapea filas del lector a objetos y ejecuta la venta dentro de SqlTransaction. El ajuste de precios se expresa con un UPDATE SQL; el reporte se calcula con JOIN y GROUP BY.</p>
<h3>Entity Framework Core</h3>
<p>TechStoreContext configura las entidades, relaciones, restricciones y tipos de columna con Fluent API. Los repositorios usan LINQ y AsNoTracking en consultas de lectura. La migración Inicial define el esquema Code First; el modelo Database First se generó aparte a partir de la base SQL de ADO.NET.</p>
<p>El reporte LINQ se guarda como SQL legible en ConsultaReporteEF.sql. El ajuste masivo se mide cargando entidades y usando ExecuteUpdate.</p>
<h3>Procedimiento de medición</h3>
<p>Las pruebas se ejecutaron en configuración Release. Cada escenario se repite cinco veces, se descarta la primera ejecución de calentamiento y se promedian las otras cuatro. El temporizador usa Stopwatch y reporta milisegundos.</p>
<ul><li>Lecturas de 1,000, 10,000 y 50,000 productos; EF con seguimiento y AsNoTracking.</li>
<li>1,000 búsquedas por Id y 100 búsquedas con texto y categoría.</li>
<li>1,000 inserciones individuales; se eliminan los productos de prueba fuera del tiempo medido.</li>
<li>Ajuste de precios, reporte por categoría y 100 ventas de tres productos.</li></ul>
<p>La prueba de ventas restituye el inventario y elimina ventas temporales. Los precios también se restauran a sus valores originales.</p>
</section>

<section class="page">
<h2>4. Resultados de rendimiento</h2>
<p>Equipo: $(Encode-Html $procesador); memoria física: $memoriaGb GB. Motor: $(Encode-Html $versionSql). Configuración: Release; lecturas desde bases locales en el mismo equipo.</p>
<div class="callout">Se repitió la batería porque la primera ronda tuvo tiempos atípicos: ADO.NET registró 66,343 ms para 1,000 productos y 9,573 ms para 10,000; en la segunda ronda fueron 5.82 ms y 22.24 ms. La tabla muestra la segunda ronda y la diferencia refleja variabilidad entre ejecuciones; interpreta las mediciones con cautela.</div>
<table><thead><tr><th>Escenario</th><th>ADO.NET ms</th><th>EF Core variante 1 ms</th><th>EF Core variante 2 ms</th></tr></thead><tbody>
$resultRows
</tbody></table>
<p class="muted">En lectura, las variantes de EF son con seguimiento y AsNoTracking. En el ajuste, son entidades y ExecuteUpdate. Los escenarios que no tienen dos variantes EF dejan esa celda vacía.</p>
<h3>Gráfico comparativo</h3>
<div class="legend">$legend</div>
<svg viewBox="0 0 800 $($chartY + 5)" role="img" aria-label="Barras de milisegundos por escenario">
$($chart.ToString())
</svg>
</section>

<section class="page">
<h2>5. Métricas de desarrollo</h2>
<table><thead><tr><th>Métrica</th><th>ADO.NET</th><th>EF Core</th><th>Nota</th></tr></thead><tbody>
$metricRows
</tbody></table>
<h3>Prueba de cambio de esquema: Marca</h3>
<p>El cambio agregó Marca a Producto y se reflejó en el esquema y las consultas ADO.NET, el modelo Fluent API y la migración Code First, los archivos generados Database First, las cargas de datos y la interfaz de consola. La propiedad aparece en 10 archivos C# y 2 scripts SQL. Como esta carpeta no conserva el estado anterior ni historial Git, no se puede medir cuántas líneas exactas fueron modificadas sin inventar una cifra.</p>
<h3>Rollback</h3>
<p>La opción 5 intentó registrar una venta superior al stock y confirmó que no cambiaron las ventas, los detalles ni el inventario, tanto con ADO.NET como con EF Core.</p>
<p>El estudiante reporta 68 horas de trabajo acumuladas en tres días; el tercer día aún no había terminado. El tiempo corresponde al proyecto completo y no se atribuye artificialmente a cada tecnología.</p>
<ol><li>Dos intentos iniciales del benchmark completo terminaron por timeout SQL. Se optimizó la consulta y se elevó el CommandTimeout a 180 segundos; luego los siete escenarios finalizaron.</li>
<li>La primera ronda mostró latencias de lectura atípicas (66,343 ms para 1,000 productos y 9,573 ms para 10,000). Se repitió el benchmark y se reportan los resultados de la segunda ronda.</li></ol>
<p>Son incidentes de medición observados, no errores inventados de compilación. La compilación Release final reportó 0 errores y 0 advertencias.</p>
</section>

<section class="page">
<h2>6. Análisis y conclusión</h2>
<p>En la segunda ejecución, ADO.NET fue más rápido en las lecturas de 1,000 y 10,000 productos (5.82 frente a 11.68 ms con AsNoTracking; 22.24 frente a 24.30 ms), mientras que para 50,000 productos EF Core con AsNoTracking quedó ligeramente por delante (105.50 frente a 116.48 ms). EF con seguimiento fue más lento en esas lecturas, sobre todo al cargar 10,000 o más entidades. Para búsquedas repetidas por ID, ADO.NET tomó 206.61 ms frente a 572.65 ms en EF Core; en las búsquedas filtradas las dos opciones tardaron varios segundos y ADO.NET resultó apenas más rápido.</p>
<p>ADO.NET también fue más rápido al insertar 1,000 productos (550.12 frente a 1,172.33 ms). En cambio, el ajuste masivo con ExecuteUpdate fue el más veloz (15.88 ms), comparado con 96.46 ms en ADO.NET y 446.64 ms cargando y guardando entidades. El reporte agregado tardó casi lo mismo en ambos enfoques, y EF Core fue más rápido en la prueba de 100 ventas (286.89 frente a 336.08 ms). El repositorio ADO.NET contiene 293 líneas frente a 163 en EF Core; el ORM reduce código repetitivo, aunque eso no implica que gane en todos los tiempos.</p>
<p>La conclusión es que no hay un ganador universal: el resultado depende de la operación, del seguimiento de entidades, del SQL generado y del entorno. Además, la primera ronda tuvo tiempos de lectura extraordinariamente altos (66,343 ms para 1,000 filas y 9,573 ms para 10,000), frente a 5.82 y 22.24 ms en la segunda ronda. Esta variación limita la precisión de comparar una sola ejecución; se deben repetir las pruebas bajo condiciones controladas antes de generalizar.</p>
<h3>Recomendación para TechStore</h3>
<p>Recomiendo EF Core como opción principal para el CRUD y las consultas de negocio por su menor cantidad de código, mapeo y mantenimiento más directo. Para lecturas masivas usaría AsNoTracking; para actualizaciones masivas, ExecuteUpdate, que fue la variante más rápida en esta medición. Mantendría ADO.NET para operaciones de alta frecuencia o consultas que requieran control explícito del SQL, pero solo cuando benchmarks repetidos demuestren una ventaja relevante. La elección final debe considerar también mantenibilidad, transacciones y requisitos del equipo, no solo estos tiempos locales.</p>
<h3>Enfoque Model First</h3>
<p>EF Core no incluye el diseñador visual de Model First basado en `.edmx`. Ese enfoque pertenece a EF6 para proyectos de .NET Framework. Code First y Database First son los enfoques comparados en este proyecto; Model First con EF6 es un bonus opcional de la práctica.</p>
</section>

<section class="page">
<h2>7. Referencias</h2>
<ul>
<li>Microsoft. (s. f.). <i>ADO.NET</i>. Microsoft Learn. https://learn.microsoft.com/dotnet/framework/data/adonet/</li>
<li>Microsoft. (s. f.). <i>Microsoft.Data.SqlClient</i>. Microsoft Learn. https://learn.microsoft.com/sql/connect/ado-net/</li>
<li>Microsoft. (s. f.). <i>Entity Framework Core</i>. Microsoft Learn. https://learn.microsoft.com/ef/core/</li>
<li>Microsoft. (s. f.). <i>Entity Framework 6</i>. Microsoft Learn. https://learn.microsoft.com/ef/ef6/</li>
</ul>
<h3>Material de la asignatura</h3>
<p>Práctica TechStore: Evaluación comparativa entre ADO.NET y Entity Framework Core. Guía ADO.NET vs. Entity Framework.</p>
<div class="callout">Para la entrega, agrega las capturas de la migración, el scaffolding Database First, el Rollback verificado y la tabla de resultados. Revisa las instrucciones de la práctica y explica el código durante la demostración.</div>
<footer>Resultados recogidos localmente. Repite el benchmark si cambias el volumen, el equipo o la configuración.</footer>
</section>
</body>
</html>
"@

$htmlPath = Join-Path $PSScriptRoot 'InformeTecnico.html'
$pdfPath = Join-Path $PSScriptRoot 'InformeTecnico.pdf'
Set-Content -LiteralPath $htmlPath -Value $document -Encoding UTF8

$edgePath = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
if (Test-Path -LiteralPath $edgePath) {
    $fileUrl = ([System.Uri]$htmlPath).AbsoluteUri
    $arguments = '--headless --disable-gpu --no-pdf-header-footer --print-to-pdf="{0}" "{1}"' -f $pdfPath, $fileUrl
    Start-Process -FilePath $edgePath -ArgumentList $arguments -Wait
    if (-not (Test-Path -LiteralPath $pdfPath)) {
        throw 'No se generó el PDF. Abre InformeTecnico.html en Edge y usa Imprimir > Guardar como PDF.'
    }
    $pdfBytes = [System.IO.File]::ReadAllBytes($pdfPath)
    $pdfText = [System.Text.Encoding]::GetEncoding(28591).GetString($pdfBytes)
    $pageCount = [regex]::Matches($pdfText, '/Type\s*/Page\b').Count
    if ($pageCount -lt 5 -or $pageCount -gt 10) {
        throw "El PDF tiene $pageCount páginas; la práctica requiere entre 5 y 10."
    }
    Write-Output "PDF generado: $pdfPath ($pageCount páginas)"
}
else {
    Write-Output "HTML generado: $htmlPath. Imprímelo a PDF desde el navegador."
}
