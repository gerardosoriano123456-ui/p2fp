# p2fp

TechStore: aplicación de consola en .NET 8 que compara el rendimiento y la implementación de acceso a datos con ADO.NET y Entity Framework Core sobre SQL Server, incluyendo migraciones, Database First y benchmarks.

## Descripción

Proyecto de consola para implementar el mismo módulo con ADO.NET y EF Core, comparar sus operaciones y medir los siete escenarios solicitados en la práctica.

## Requisitos

- .NET SDK 8 o superior.
- SQL Server local con instancia predeterminada `localhost` (probado con SQL Server 2025 Developer).
- La conexión usa autenticación de Windows. Si tu instancia es distinta, cambia las cadenas de conexión en `src/TechStore.Consola/Program.cs` y `src/TechStore.Consola/PruebasRendimiento.cs`.
- `dotnet-ef` 8.0.11 o superior:

  ```powershell
  dotnet tool install --global dotnet-ef --version 8.0.11
  ```

## Preparar las bases

Abre PowerShell en la carpeta `TechStore` y ejecuta:

```powershell
sqlcmd -S localhost -E -C -b -i .\TechStoreDB.sql
dotnet ef database update --project .\src\TechStore.Datos.Ef --startup-project .\src\TechStore.Consola
sqlcmd -S localhost -E -C -b -i .\SeedTechStoreDbEf.sql
```

`TechStoreDB.sql` crea las tablas de ADO.NET y los datos de prueba (50,000 productos, 5 categorías, 100 clientes y 1,000 ventas). También crea `TechStoreDB_EF` vacía si todavía no existe. EF Core crea su esquema con la migración Code First; luego `SeedTechStoreDbEf.sql` copia los mismos registros.

**Aviso:** `TechStoreDB.sql` elimina y vuelve a crear las tablas de `TechStoreDB`. No lo ejecutes sobre datos que quieras conservar. El script de carga para EF Core se niega a duplicar registros si la base ya está poblada.

## Ejecutar y probar

```powershell
dotnet build .\TechStore.sln -c Release
dotnet run -c Release --project .\src\TechStore.Consola
```

El menú permite listar y buscar en ambos proveedores, registrar ventas, verificar un Rollback por falta de stock y correr las pruebas.

La opción **4** ejecuta los siete escenarios comparativos. Mide cinco veces cada uno, descarta el calentamiento y promedia las cuatro mediciones restantes. En la lectura compara EF con seguimiento y `AsNoTracking`; las pruebas de inserción y venta limpian sus registros temporales y restauran el stock y los precios originales.

Al finalizar, genera `ResultadosRendimiento.csv` y `ConsultaReporteEF.sql` en la carpeta desde la que se ejecutó el comando. Los resultados dependen del equipo: no los sustituyas por cifras de otra computadora.

La opción **5** comprueba que una venta por encima del stock disponible no cambie inventario, ventas ni detalles, con ADO.NET y EF Core.

## Métricas e informe

Después de ejecutar la opción 4 desde la carpeta `TechStore`, genera las métricas de código:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\MetricasDesarrollo.ps1
```

El CSV incluye el tiempo total de desarrollo indicado, los incidentes reales de las pruebas y el alcance del cambio de esquema. Luego genera el informe y su PDF:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\GenerarInforme.ps1
```

El informe usa los datos del estudiante ya configurados, los resultados medidos y una gráfica. Revisa las capturas requeridas y confirma que las métricas personales reflejen tu experiencia antes de entregar.

## Capturas de ejecución

Las evidencias proporcionadas están en `Capturas/`: migración aplicada, scaffolding Database First, rollback, benchmarks y una ejecución Database First que terminó por timeout. Esta última documenta un fallo, no una prueba exitosa; repite Database First y agrega la captura del resultado correcto antes de entregar.

## Proyectos

- `TechStore.Dominio`: modelos y contratos comunes.
- `TechStore.Datos.Ado`: implementación con `Microsoft.Data.SqlClient`.
- `TechStore.Datos.Ef`: `TechStoreContext`, repositorios, Code First y migraciones.
- `TechStore.DatabaseFirst`: entidades y contexto generados con `Scaffold-DbContext` desde `TechStoreDB`.
- `TechStore.Consola`: menú, validación de transacciones y medición.

El cambio de esquema de la práctica se incluye en `Producto.Marca`. La migración Code First crea las cinco tablas; el modelo Database First refleja la misma columna.

## Volver a generar Database First

Con `TechStoreDB` creada y accesible:

```powershell
dotnet ef dbcontext scaffold "Server=.;Database=TechStoreDB;Trusted_Connection=True;TrustServerCertificate=True;" Microsoft.EntityFrameworkCore.SqlServer --project .\src\TechStore.DatabaseFirst --startup-project .\src\TechStore.DatabaseFirst --output-dir Models --context TechStoreDbContext --force --no-onconfiguring --use-database-names
```

## Para completar la entrega académica

- Ejecuta las pruebas en tu propia computadora y conserva el CSV generado.
- Revisa que el informe describa correctamente el tiempo total de desarrollo reportado y los incidentes observados durante las pruebas.
- Explica el análisis y la recomendación del informe con tus propias palabras durante la demostración.
- Agrega tus capturas de la migración, scaffolding, Rollback y tabla de resultados.
- Confirma que la entrega esté publicada en el repositorio de GitHub indicado por la asignatura.

El informe y los datos de medición deben poder explicarse durante la demostración. La guía general pide describir Model First con EF6 y explicar que EF Core no incluye ese diseñador `.edmx`; no es requisito desarrollar ese bonus de EF6.

## Panel web para probar la tienda

Se agregó una interfaz web sencilla en `TechStore.Web` que reutiliza los repositorios actuales. Con las bases de datos preparadas, ejecútala desde la carpeta raíz:

```powershell
dotnet run --project .\src\TechStore.Web
```

Abre la dirección local que muestre la consola (normalmente `http://localhost:5000`). El panel permite consultar productos con ADO.NET o EF Core, filtrar por nombre y categoría, y registrar una venta de prueba indicando el Id. de cliente. Las ventas se guardan únicamente en la base de datos del proveedor seleccionado. ADO.NET usa `TechStoreDB` y EF Core usa `TechStoreDB_EF`, igual que la aplicación de consola.

### Iniciar con F5 en Visual Studio

Abre `TechStore.sln`, en el Explorador de soluciones haz clic derecho en `TechStore.Web` y selecciona **Establecer como proyecto de inicio**. Luego presiona **F5**: Visual Studio abrirá el panel en `http://localhost:5080`. La sección “Revisión de la práctica” muestra los archivos disponibles y el CSV de rendimiento más reciente. Para generar o actualizar las mediciones, ejecuta la opción 4 de la consola y vuelve a pulsar “Actualizar revisión” en el panel.

En VS Code, selecciona la configuración **TechStore Web (F5)** en Run and Debug y presiona **F5**. VS Code compila el proyecto web y abre el navegador cuando el servidor esté listo.
