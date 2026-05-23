# SDD — Fix Sueldos Docentes: Error Refresh + Pesos ARS + Tarifa Global
> Spec-Driven Development — Corrección de bugs + nuevas reglas de negocio  
> Versión 1.0 — Mayo 2026  
> **Leer COMPLETO antes de tocar cualquier archivo**

---

## 0. CONTEXTO VISUAL — Qué se ve mal y qué se pide

### Bugs detectados en pantalla
| Bug | Síntoma | Causa |
|-----|---------|-------|
| Error "No se permite Refresh durante AddNew o EditItem" | Al editar tarifa en el DataGrid | El `LostFocus` llama a `ItemsSource` mientras el DataGrid está en modo edición |
| Valores en dólares | Muestra `$28,28` en vez de `$28.280` | El formato usa `.ToString("N2")` con cultura en-US, no es-AR |
| Si pongo 4000 me sale 28 | El sueldo calcula mal | `tarifa_hora` en BD está en `0` o `NULL`, el cálculo usa el valor viejo antes del save |
| No hay input de tarifa global | Se edita celda por celda | Falta un campo global tipo "Tarifa para todos: $____/h" |

### Reglas de negocio relevadas en entrevista
| Regla | Decisión del dueño |
|-------|-------------------|
| Tarifa por hora | **Una sola variable global** para todos los profes (`$4.000/h`) |
| Cambio de tarifa | Un solo input editable, al cambiar aplica a todos |
| Gastos fijos | El admin los ingresa mes a mes en un formulario (alquiler, sistema, etc.) |
| Aguinaldo | **No calcular automático** — solo mostrar un aviso en diciembre y junio |
| Ganancia neta | No calcular en el sistema — mostrar cada concepto por separado |
| Moneda | **Pesos argentinos (ARS)** — formato: `$4.000,00` con punto en miles y coma en decimales |

---

## 1. CONDICIONES — Reglas no negociables

```
- C# 7.3 estricto
- SQL Server LocalDB: DROP + CREATE (nunca CREATE OR ALTER)
- Sin SQL inline en DAOs — solo Stored Procedures
- Moneda: System.Globalization.CultureInfo("es-AR") en TODOS los formateos
- El DataGrid NO debe estar en modo edición cuando se llama a ActualizarTotalSueldos()
- Auditor.Registrar() al guardar la tarifa global
- SesionManager.UsuarioId en lugar de IDs hardcodeados
```

---

## 2. DIAGNÓSTICO DETALLADO

### Bug 1 — Error "No se permite Refresh durante AddNew o EditItem"

**Causa exacta:** El evento `LostFocus` del TextBox editable dispara `CargarSueldosDocentes()` que hace `gridSueldos.ItemsSource = nuevaLista`. Esto reemplaza la colección mientras el DataGrid sigue en modo edición (`IsEditing = true`), lo que WPF no permite.

**Fix:** Nunca reasignar `ItemsSource` mientras el DataGrid está editando. En cambio:
1. Guardar en BD
2. Actualizar el objeto en memoria directamente (sin recargar la lista)
3. Forzar el refresh del DataGrid con `Items.Refresh()` recién después de `CommitEdit()`

### Bug 2 — Formato dólares vs pesos

**Causa:** `ToString("N2")` sin especificar cultura usa la del sistema (en-US → `$28.28`) en lugar de es-AR (`$28,28`). Para pesos argentinos el formato correcto es `ToString("N2", new CultureInfo("es-AR"))` que produce `28,28` y para moneda completa `ToString("C2", new CultureInfo("es-AR"))` que produce `$ 28,28`.

**Fix global:** Crear una clase helper `FormatoARS` con métodos estáticos reutilizables en toda la app.

### Bug 3 — Tarifa global en vez de por celda

**Causa de diseño:** El SDD anterior implementó la tarifa como editable por celda (cada instructor tiene la suya). El dueño confirmó que es **una sola tarifa para todos**.

**Fix de arquitectura:**
- Crear tabla `configuracion_sistema` con clave-valor
- La tarifa global vive ahí como `tarifa_hora_docentes = 4000`
- El reporte lee esa variable y la aplica a todos
- El admin la cambia desde un solo input arriba del reporte

---

## 3. PLAN DE EJECUCIÓN — En este orden exacto

```
PASO 1  → Crear tabla configuracion_sistema en SQL
PASO 2  → SP sp_ObtenerConfiguracion (leer valor)
PASO 3  → SP sp_ActualizarConfiguracion (escribir valor)
PASO 4  → SP sp_ReporteSueldosDocentes (reescribir usando tarifa global)
PASO 5  → Crear FormatoARS.cs en Helpers/
PASO 6  → Agregar ConfiguracionDao.cs en Models/DAO/
PASO 7  → Agregar ConfiguracionController.cs en Controllers/
PASO 8  → Modificar ReportesPage.xaml (quitar columna editable, agregar input global)
PASO 9  → Modificar ReportesPage.xaml.cs (fix error Refresh, fix formato ARS)
PASO 10 → Buscar y reemplazar TODOS los formateos de moneda en toda la app
```

---

## 4. CÓDIGO — Implementar exactamente esto

---

### PASO 1 — Crear tabla `configuracion_sistema`

```sql
-- Ejecutar en SQL Server Object Explorer → New Query → DB_CAI_Optimus.mdf

IF OBJECT_ID('configuracion_sistema') IS NULL
CREATE TABLE configuracion_sistema (
    clave         VARCHAR(100)   NOT NULL PRIMARY KEY,
    valor         NVARCHAR(500)  NOT NULL,
    descripcion   NVARCHAR(300)  NULL,
    actualizado_en DATETIME      NOT NULL DEFAULT GETDATE(),
    actualizado_por BIGINT       NULL REFERENCES usuarios(id)
);

-- Insertar valores iniciales si no existen
IF NOT EXISTS (SELECT 1 FROM configuracion_sistema WHERE clave = 'tarifa_hora_docentes')
    INSERT INTO configuracion_sistema (clave, valor, descripcion)
    VALUES ('tarifa_hora_docentes', '4000', 'Tarifa por hora en pesos ARS aplicada a todos los instructores');

IF NOT EXISTS (SELECT 1 FROM configuracion_sistema WHERE clave = 'nombre_gimnasio')
    INSERT INTO configuracion_sistema (clave, valor, descripcion)
    VALUES ('nombre_gimnasio', 'OptimusCAI Gym', 'Nombre del gimnasio para reportes PDF');

IF NOT EXISTS (SELECT 1 FROM configuracion_sistema WHERE clave = 'direccion_gimnasio')
    INSERT INTO configuracion_sistema (clave, valor, descripcion)
    VALUES ('direccion_gimnasio', 'Av. Ejemplo 1234, Jujuy', 'Dirección para encabezado PDF');

IF NOT EXISTS (SELECT 1 FROM configuracion_sistema WHERE clave = 'telefono_gimnasio')
    INSERT INTO configuracion_sistema (clave, valor, descripcion)
    VALUES ('telefono_gimnasio', '+54 388 000-0000', 'Teléfono para encabezado PDF');

-- Verificar:
SELECT clave, valor, descripcion FROM configuracion_sistema;
```

---

### PASO 2 — SP `sp_ObtenerConfiguracion`

```sql
IF OBJECT_ID('sp_ObtenerConfiguracion','P') IS NOT NULL DROP PROCEDURE sp_ObtenerConfiguracion;
GO
CREATE PROCEDURE sp_ObtenerConfiguracion
    @Clave VARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT clave, valor, descripcion, actualizado_en
    FROM configuracion_sistema
    WHERE clave = @Clave;
END;
GO
```

---

### PASO 3 — SP `sp_ActualizarConfiguracion`

```sql
IF OBJECT_ID('sp_ActualizarConfiguracion','P') IS NOT NULL DROP PROCEDURE sp_ActualizarConfiguracion;
GO
CREATE PROCEDURE sp_ActualizarConfiguracion
    @Clave          VARCHAR(100),
    @Valor          NVARCHAR(500),
    @ActualizadoPor BIGINT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM configuracion_sistema WHERE clave = @Clave)
    BEGIN
        RAISERROR('La clave de configuración no existe.', 16, 1);
        RETURN;
    END

    UPDATE configuracion_sistema
    SET valor           = @Valor,
        actualizado_en  = GETDATE(),
        actualizado_por = @ActualizadoPor
    WHERE clave = @Clave;

    SELECT @@ROWCOUNT AS filas_afectadas;
END;
GO
```

---

### PASO 4 — SP `sp_ReporteSueldosDocentes` (reescritura completa)

```sql
IF OBJECT_ID('sp_ReporteSueldosDocentes','P') IS NOT NULL DROP PROCEDURE sp_ReporteSueldosDocentes;
GO
CREATE PROCEDURE sp_ReporteSueldosDocentes
    @FechaDesde DATE = NULL,
    @FechaHasta DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @FechaDesde IS NULL
        SET @FechaDesde = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);
    IF @FechaHasta IS NULL
        SET @FechaHasta = CAST(GETDATE() AS DATE);

    -- Leer la tarifa global desde configuracion_sistema
    DECLARE @TarifaGlobal DECIMAL(10,2) = 0;
    SELECT @TarifaGlobal = CAST(valor AS DECIMAL(10,2))
    FROM configuracion_sistema
    WHERE clave = 'tarifa_hora_docentes';

    -- Alerta aguinaldo: avisar si estamos en junio o diciembre
    DECLARE @MesActual INT = MONTH(GETDATE());
    DECLARE @AlertaAguinaldo BIT = CASE 
        WHEN @MesActual IN (6, 12) THEN 1 ELSE 0 
    END;

    -- ─── Resultset 1: Resumen por instructor ─────────────────────────────
    SELECT
        u.id                                                        AS instructor_id,
        u.nombre + ' ' + u.apellido                                 AS nombre_completo,
        u.foto,
        @TarifaGlobal                                               AS tarifa_hora,
        ISNULL(a.nombre, '—')                                       AS actividad_nombre,
        COUNT(DISTINCT ia.fecha)                                    AS dias_trabajados,
        ISNULL(SUM(ia.horas_trabajadas), 0)                         AS horas_totales,
        -- Sueldo: horas totales × tarifa global (NO usa tarifa_hora de la tabla usuarios)
        ISNULL(SUM(ia.horas_trabajadas), 0) * @TarifaGlobal         AS sueldo_estimado,
        -- Ingresos generados por los socios de su actividad en el período
        ISNULL((
            SELECT SUM(cm2.monto)
            FROM caja_movimientos cm2
            INNER JOIN membresias m2
                    ON m2.id = cm2.referencia_id
                   AND LOWER(LTRIM(RTRIM(cm2.referencia_tipo))) = 'membresia'
            INNER JOIN turnos t2
                    ON t2.actividad_id = m2.actividad_id
                   AND t2.instructor_id = u.id
                   AND t2.activo = 1
            WHERE LOWER(LTRIM(RTRIM(cm2.tipo))) = 'ingreso'
              AND CAST(cm2.creado_en AS DATE) BETWEEN @FechaDesde AND @FechaHasta
        ), 0)                                                        AS ingresos_generados,
        @AlertaAguinaldo                                             AS alerta_aguinaldo
    FROM usuarios u
    LEFT JOIN instructor_asistencias ia
           ON ia.instructor_id = u.id
          AND ia.fecha BETWEEN @FechaDesde AND @FechaHasta
    LEFT JOIN (
        SELECT DISTINCT instructor_id, actividad_id
        FROM turnos WHERE activo = 1 AND instructor_id IS NOT NULL
    ) t ON t.instructor_id = u.id
    LEFT JOIN actividades a ON a.id = t.actividad_id
    WHERE u.rol_id = 2
      AND u.activo = 1
      AND u.eliminado_en IS NULL
    GROUP BY u.id, u.nombre, u.apellido, u.foto, a.nombre
    ORDER BY u.apellido ASC;

    -- ─── Resultset 2: Tarifa global actual ───────────────────────────────
    SELECT @TarifaGlobal AS tarifa_global, @AlertaAguinaldo AS alerta_aguinaldo;
END;
GO
```

---

### PASO 5 — `Helpers/FormatoARS.cs` (clase nueva)

**Ubicación:** `SistemaGimnacionOptimusCAI/Helpers/FormatoARS.cs`

```csharp
// Helpers/FormatoARS.cs — C# 7.3
// Centraliza el formato de moneda en pesos argentinos (ARS)
// Usar esta clase en TODA la app en lugar de .ToString("N2") directo

using System.Globalization;

namespace SistemaGimnacionOptimusCAI.Helpers
{
    public static class FormatoARS
    {
        // Cultura Argentina: punto en miles, coma en decimales
        // Ejemplo: 4000 → "$ 4.000,00"
        private static readonly CultureInfo Cultura = new CultureInfo("es-AR");

        /// <summary>
        /// Formatea como moneda ARS con símbolo: $ 4.000,00
        /// </summary>
        public static string Moneda(decimal valor)
        {
            return valor.ToString("C2", Cultura);
        }

        /// <summary>
        /// Formatea como número con separadores ARS: 4.000,00
        /// (sin símbolo $, útil para campos editables)
        /// </summary>
        public static string Numero(decimal valor)
        {
            return valor.ToString("N2", Cultura);
        }

        /// <summary>
        /// Formatea como moneda abreviada sin decimales: $ 4.000
        /// Útil para cards de stats donde no necesitás centavos
        /// </summary>
        public static string MonedaCorta(decimal valor)
        {
            return "$ " + valor.ToString("N0", Cultura);
        }

        /// <summary>
        /// Convierte un string ingresado por el usuario a decimal ARS.
        /// Acepta: "4000", "4.000", "4.000,00", "4000,00"
        /// Retorna false si el formato es inválido.
        /// </summary>
        public static bool TryParsear(string texto, out decimal resultado)
        {
            resultado = 0;
            if (string.IsNullOrWhiteSpace(texto)) return false;

            // Limpiar símbolos de moneda y espacios
            string limpio = texto
                .Replace("$", "")
                .Replace(" ", "")
                .Trim();

            // Intentar con cultura argentina primero
            if (decimal.TryParse(limpio,
                NumberStyles.Any, Cultura, out resultado))
                return true;

            // Fallback: cultura invariante (punto como decimal)
            if (decimal.TryParse(limpio,
                NumberStyles.Any, CultureInfo.InvariantCulture, out resultado))
                return true;

            return false;
        }
    }
}
```

---

### PASO 6 — `Models/DAO/ConfiguracionDao.cs` (nuevo)

```csharp
// Models/DAO/ConfiguracionDao.cs — C# 7.3
using System;
using System.Data;
using System.Data.SqlClient;

namespace Models.Dao
{
    public class ConfiguracionDao : ConnectionToDB
    {
        public string ObtenerValor(string clave)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ObtenerConfiguracion", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Clave", clave);
                    using (var r = cmd.ExecuteReader())
                        if (r.Read()) return r["valor"].ToString();
                }
            }
            return null;
        }

        public decimal ObtenerDecimal(string clave, decimal valorDefault = 0)
        {
            string val = ObtenerValor(clave);
            if (string.IsNullOrEmpty(val)) return valorDefault;
            decimal resultado;
            return decimal.TryParse(val,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out resultado) ? resultado : valorDefault;
        }

        public bool ActualizarValor(string clave, string valor, long? actualizadoPor)
        {
            using (var conn = GetConnection())
            {
                conn.Open();
                using (var cmd = new SqlCommand("sp_ActualizarConfiguracion", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Clave",          clave);
                    cmd.Parameters.AddWithValue("@Valor",          valor);
                    cmd.Parameters.AddWithValue("@ActualizadoPor",
                        (object)actualizadoPor ?? DBNull.Value);
                    var filas = cmd.ExecuteScalar();
                    return filas != null && Convert.ToInt32(filas) > 0;
                }
            }
        }
    }
}
```

---

### PASO 7 — `Controllers/ConfiguracionController.cs` (nuevo)

```csharp
// Controllers/ConfiguracionController.cs — C# 7.3
using Models.Dao;
using System;
using System.Collections.Generic;

namespace Controllers
{
    public class ConfiguracionController
    {
        private readonly ConfiguracionDao _dao = new ConfiguracionDao();

        public decimal ObtenerTarifaHoraDocentes()
        {
            try { return _dao.ObtenerDecimal("tarifa_hora_docentes", 4000); }
            catch { return 4000; }
        }

        public (bool ok, string mensaje) ActualizarTarifaHoraDocentes(
            decimal nuevaTarifa, long actualizadoPor)
        {
            if (nuevaTarifa <= 0)
                return (false, "La tarifa debe ser mayor a $0.");

            if (nuevaTarifa > 999999)
                return (false, "La tarifa parece demasiado alta. Verificá el valor.");

            try
            {
                // Guardar con punto como separador decimal (InvariantCulture)
                string valorStr = nuevaTarifa.ToString(
                    "F2", System.Globalization.CultureInfo.InvariantCulture);

                bool ok = _dao.ActualizarValor(
                    "tarifa_hora_docentes", valorStr, actualizadoPor);

                if (ok)
                {
                    Auditor.Registrar("editar", "configuracion", null,
                        new Dictionary<string, object>
                        {
                            { "clave",       "tarifa_hora_docentes" },
                            { "valor_nuevo", nuevaTarifa }
                        });
                    return (true, "Tarifa actualizada a " +
                        SistemaGimnacionOptimusCAI.Helpers.FormatoARS.MonedaCorta(nuevaTarifa) +
                        "/h. Se aplica a todos los instructores.");
                }

                return (false, "No se pudo actualizar la tarifa.");
            }
            catch (Exception ex)
            {
                return (false, "Error al guardar: " + ex.Message);
            }
        }

        public string ObtenerNombreGimnasio()
        {
            try { return _dao.ObtenerValor("nombre_gimnasio") ?? "OptimusCAI Gym"; }
            catch { return "OptimusCAI Gym"; }
        }

        public string ObtenerDireccion()
        {
            try { return _dao.ObtenerValor("direccion_gimnasio") ?? ""; }
            catch { return ""; }
        }

        public string ObtenerTelefono()
        {
            try { return _dao.ObtenerValor("telefono_gimnasio") ?? ""; }
            catch { return ""; }
        }
    }
}
```

---

### PASO 8 — `ReportesPage.xaml` — Tab Sueldos Docentes (reescribir sección)

**Buscar la sección del tab "SUELDOS DOCENTES" y reemplazar COMPLETA con:**

```xml
<!-- ══ TAB SUELDOS DOCENTES ══ -->
<!-- Panel tarifa global — editable por admin -->
<Border Background="#12121E" CornerRadius="10"
        BorderBrush="#252540" BorderThickness="1"
        Padding="20,14" Margin="0,0,0,14">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>

        <!-- Ícono + etiqueta -->
        <StackPanel Grid.Column="0" Orientation="Horizontal"
                    VerticalAlignment="Center" Margin="0,0,20,0">
            <TextBlock Text="💰" FontSize="20" Margin="0,0,10,0"
                       VerticalAlignment="Center"/>
            <StackPanel VerticalAlignment="Center">
                <TextBlock Text="TARIFA POR HORA — TODOS LOS INSTRUCTORES"
                           FontFamily="Bahnschrift SemiBold"
                           FontSize="11" FontWeight="Bold"
                           Foreground="#A78BFA"/>
                <TextBlock Text="Un solo valor aplicado a todos. Cambiarlo afecta el cálculo de todos."
                           FontSize="10" Foreground="#6A6A9A" Margin="0,2,0,0"/>
            </StackPanel>
        </StackPanel>

        <!-- Input de tarifa -->
        <Border Grid.Column="1" Background="#16162A" CornerRadius="10"
                BorderBrush="#A78BFA" BorderThickness="1.5"
                Height="46" Width="220" HorizontalAlignment="Left">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="Auto"/>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <TextBlock Grid.Column="0" Text="$" FontSize="16"
                           FontWeight="Bold" Foreground="#A78BFA"
                           VerticalAlignment="Center" Margin="14,0,6,0"/>
                <TextBox x:Name="txtTarifaGlobal"
                         Grid.Column="1"
                         BorderThickness="0" Background="Transparent"
                         Foreground="#E8E8FF" CaretBrush="#A78BFA"
                         FontFamily="Consolas" FontSize="16"
                         FontWeight="SemiBold"
                         VerticalContentAlignment="Center"
                         KeyDown="txtTarifaGlobal_KeyDown"/>
                <TextBlock Grid.Column="2" Text="/h" FontSize="12"
                           Foreground="#6A6A9A"
                           VerticalAlignment="Center" Margin="4,0,12,0"/>
            </Grid>
        </Border>

        <!-- Botón guardar tarifa -->
        <Button Grid.Column="2" x:Name="btnGuardarTarifa"
                Content="✓ APLICAR A TODOS"
                Click="btnGuardarTarifa_Click"
                Background="#1A1440" Foreground="#A78BFA"
                BorderBrush="#A78BFA" BorderThickness="1.5"
                FontSize="11" FontWeight="Bold"
                Cursor="Hand" Margin="12,0,0,0" Height="46" Padding="16,0">
            <Button.Template>
                <ControlTemplate TargetType="Button">
                    <Border Background="{TemplateBinding Background}"
                            BorderBrush="{TemplateBinding BorderBrush}"
                            BorderThickness="{TemplateBinding BorderThickness}"
                            CornerRadius="10" Padding="{TemplateBinding Padding}">
                        <ContentPresenter HorizontalAlignment="Center"
                                          VerticalAlignment="Center"/>
                    </Border>
                </ControlTemplate>
            </Button.Template>
        </Button>
    </Grid>
</Border>

<!-- Alerta aguinaldo (visible solo en junio y diciembre) -->
<Border x:Name="panelAlertaAguinaldo" Visibility="Collapsed"
        Background="#2A1F00" CornerRadius="10"
        BorderBrush="#FFA726" BorderThickness="1"
        Padding="16,12" Margin="0,0,0,14">
    <StackPanel Orientation="Horizontal">
        <TextBlock Text="⚠️" FontSize="18" Margin="0,0,12,0"
                   VerticalAlignment="Center"/>
        <StackPanel VerticalAlignment="Center">
            <TextBlock Text="CORRESPONDE PAGAR AGUINALDO ESTE MES"
                       FontFamily="Bahnschrift SemiBold"
                       FontSize="12" FontWeight="Bold" Foreground="#FFA726"/>
            <TextBlock Text="Recordá liquidar el aguinaldo de cada instructor. El sistema no lo calcula automáticamente."
                       FontSize="11" Foreground="#A0804A" Margin="0,2,0,0"/>
        </StackPanel>
    </StackPanel>
</Border>

<!-- DataGrid de sueldos — SIN columna editable, tarifa es global -->
<DataGrid x:Name="gridSueldos"
          IsReadOnly="True"
          AutoGenerateColumns="False"
          CanUserAddRows="False"
          CanUserResizeRows="False"
          Background="Transparent"
          BorderThickness="0"
          RowHeaderWidth="0"
          RowHeight="58"
          GridLinesVisibility="Horizontal"
          HorizontalGridLinesBrush="#1A1A2E">
    <DataGrid.Columns>

        <!-- Foto + nombre -->
        <DataGridTemplateColumn Header="INSTRUCTOR" Width="2.5*">
            <DataGridTemplateColumn.CellTemplate>
                <DataTemplate>
                    <StackPanel Orientation="Horizontal" VerticalAlignment="Center"
                                Margin="8,0">
                        <Grid Margin="0,0,10,0">
                            <Ellipse Width="36" Height="36">
                                <Ellipse.Fill>
                                    <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                                        <GradientStop Color="#A78BFA" Offset="0"/>
                                        <GradientStop Color="#00CFFF" Offset="1"/>
                                    </LinearGradientBrush>
                                </Ellipse.Fill>
                            </Ellipse>
                            <Ellipse Width="32" Height="32">
                                <Ellipse.Fill>
                                    <ImageBrush ImageSource="{Binding Foto,
                                                Converter={StaticResource BytesAImagen}}"
                                                Stretch="UniformToFill"/>
                                </Ellipse.Fill>
                            </Ellipse>
                        </Grid>
                        <StackPanel VerticalAlignment="Center">
                            <TextBlock Text="{Binding NombreCompleto}"
                                       FontSize="13" FontWeight="SemiBold"
                                       Foreground="#E8E8FF"/>
                            <TextBlock Text="{Binding ActividadNombre}"
                                       FontSize="11" Foreground="#A78BFA"/>
                        </StackPanel>
                    </StackPanel>
                </DataTemplate>
            </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>

        <!-- Días -->
        <DataGridTemplateColumn Header="DÍAS" Width="0.8*">
            <DataGridTemplateColumn.CellTemplate>
                <DataTemplate>
                    <TextBlock Text="{Binding DiasTrabajTexto}"
                               FontSize="12" Foreground="#E8E8FF"
                               VerticalAlignment="Center"
                               HorizontalAlignment="Center"/>
                </DataTemplate>
            </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>

        <!-- Horas -->
        <DataGridTemplateColumn Header="HORAS" Width="0.8*">
            <DataGridTemplateColumn.CellTemplate>
                <DataTemplate>
                    <TextBlock Text="{Binding HorasTexto}"
                               FontFamily="Consolas" FontSize="12"
                               FontWeight="Bold" Foreground="#00E676"
                               VerticalAlignment="Center"
                               HorizontalAlignment="Center"/>
                </DataTemplate>
            </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>

        <!-- Tarifa (solo lectura, viene del global) -->
        <DataGridTemplateColumn Header="TARIFA/H" Width="1*">
            <DataGridTemplateColumn.CellTemplate>
                <DataTemplate>
                    <TextBlock Text="{Binding TarifaTexto}"
                               FontFamily="Consolas" FontSize="12"
                               Foreground="#A78BFA"
                               VerticalAlignment="Center"
                               HorizontalAlignment="Center"/>
                </DataTemplate>
            </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>

        <!-- Sueldo estimado -->
        <DataGridTemplateColumn Header="SUELDO ESTIMADO" Width="1.2*">
            <DataGridTemplateColumn.CellTemplate>
                <DataTemplate>
                    <TextBlock Text="{Binding SueldoTexto}"
                               FontFamily="Consolas" FontSize="13"
                               FontWeight="Bold" Foreground="#00CFFF"
                               VerticalAlignment="Center"
                               HorizontalAlignment="Center"/>
                </DataTemplate>
            </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>

        <!-- Ingresos generados -->
        <DataGridTemplateColumn Header="INGRESOS GENERADOS" Width="1.4*">
            <DataGridTemplateColumn.CellTemplate>
                <DataTemplate>
                    <TextBlock Text="{Binding IngresosGenerTexto}"
                               FontFamily="Consolas" FontSize="12"
                               Foreground="#FF6B35"
                               VerticalAlignment="Center"
                               HorizontalAlignment="Center"/>
                </DataTemplate>
            </DataGridTemplateColumn.CellTemplate>
        </DataGridTemplateColumn>

    </DataGrid.Columns>
</DataGrid>
```

---

### PASO 9 — `ReportesPage.xaml.cs` — Reescribir la sección de sueldos

**Buscar y reemplazar los métodos relacionados con sueldos. Implementar exactamente:**

```csharp
// ── CAMPOS PRIVADOS — agregar a la clase ─────────────────────────────

private readonly ConfiguracionController _configCtrl = new ConfiguracionController();
private List<ResumenDocente> _docentesActuales = new List<ResumenDocente>();
private decimal _tarifaGlobalActual = 4000;

// ── AL CARGAR EL TAB DE SUELDOS ──────────────────────────────────────

private void CargarTabSueldos()
{
    CargarTarifaGlobal();
    CargarSueldosDocentes();
}

private void CargarTarifaGlobal()
{
    try
    {
        _tarifaGlobalActual = _configCtrl.ObtenerTarifaHoraDocentes();
        // Mostrar en el input sin símbolo $ — solo el número
        txtTarifaGlobal.Text = _tarifaGlobalActual.ToString("N0",
            new System.Globalization.CultureInfo("es-AR"));
    }
    catch
    {
        txtTarifaGlobal.Text = "4000";
    }
}

private void CargarSueldosDocentes()
{
    try
    {
        // NO reasignar ItemsSource si el DataGrid está editando
        // (eso causa el error "Refresh durante AddNew o EditItem")
        if (gridSueldos.IsEditing)
            gridSueldos.CommitEdit(DataGridEditingUnit.Row, true);

        var desde = dpSueldosDesde.SelectedDate;
        var hasta = dpSueldosHasta.SelectedDate;

        _docentesActuales = _controller.ObtenerSueldosDocentes(desde, hasta);

        // Recalcular con la tarifa actual ANTES de bindear
        // (el SP ya usa la tarifa global, pero si se acaba de cambiar
        //  y no se guardó aún, recalcular en memoria)
        foreach (var d in _docentesActuales)
        {
            d.TarifaHora     = _tarifaGlobalActual;
            d.SueldoEstimado = d.HorasTotales * _tarifaGlobalActual;
        }

        gridSueldos.ItemsSource = _docentesActuales;
        ActualizarTotalSueldos();

        // Mostrar alerta aguinaldo si corresponde
        bool esJunioODiciembre = DateTime.Today.Month == 6
                                 || DateTime.Today.Month == 12;
        panelAlertaAguinaldo.Visibility = esJunioODiciembre
            ? Visibility.Visible : Visibility.Collapsed;
    }
    catch (Exception ex)
    {
        NotificacionWindow.MostrarError("Error al cargar sueldos.\n" + ex.Message);
    }
}

private void ActualizarTotalSueldos()
{
    decimal total = 0;
    if (_docentesActuales != null)
        foreach (var d in _docentesActuales)
            total += d.SueldoEstimado;

    if (lblTotalSueldos != null)
        lblTotalSueldos.Text = Helpers.FormatoARS.Moneda(total);
}

// ── GUARDAR TARIFA GLOBAL ─────────────────────────────────────────────

private void btnGuardarTarifa_Click(object sender, RoutedEventArgs e)
{
    decimal nuevaTarifa;
    if (!Helpers.FormatoARS.TryParsear(txtTarifaGlobal.Text, out nuevaTarifa))
    {
        NotificacionWindow.MostrarError(
            "Ingresá un número válido. Ejemplo: 4000 o 4.000,00");
        return;
    }

    var resultado = _configCtrl.ActualizarTarifaHoraDocentes(
        nuevaTarifa, SesionManager.UsuarioId);

    if (resultado.ok)
    {
        _tarifaGlobalActual = nuevaTarifa;
        NotificacionWindow.MostrarExito(resultado.mensaje);
        // Recargar para que el DataGrid muestre los sueldos recalculados
        CargarSueldosDocentes();
    }
    else
    {
        NotificacionWindow.MostrarError(resultado.mensaje);
    }
}

// Permitir guardar con Enter en el input de tarifa
private void txtTarifaGlobal_KeyDown(object sender, KeyEventArgs e)
{
    if (e.Key == Key.Enter)
        btnGuardarTarifa_Click(sender, e);
}
```

---

### PASO 10 — Buscar y reemplazar formatos de moneda en toda la app

Hacer **Ctrl+Shift+H** en Visual Studio (Buscar y reemplazar en todos los archivos):

#### Reemplazos a hacer:

| Buscar | Reemplazar |
|--------|-----------|
| `.ToString("N2")` en archivos `.cs` | `.ToString("N2", new System.Globalization.CultureInfo("es-AR"))` |
| `"$" + x.ToString("N2")` | `FormatoARS.Moneda(x)` |
| `"$" + d.SueldoEstimado.ToString("N2")` | `FormatoARS.Moneda(d.SueldoEstimado)` |

> **NOTA:** No reemplazar en archivos de iTextSharp ni ClosedXML — esas librerías manejan su propio formato. Solo reemplazar en `.cs` de la UI y Entities.

#### En `Entities/ReporteFinanciero.cs` — corregir los campos de texto:

```csharp
// ANTES:
public string SueldoTexto        => "$" + SueldoEstimado.ToString("N2");
public string TarifaTexto        => "$" + TarifaHora.ToString("N2") + "/h";
public string IngresosGenerTexto => "$" + IngresosGenerados.ToString("N2");

// DESPUÉS (usando FormatoARS):
public string SueldoTexto        => SistemaGimnacionOptimusCAI.Helpers.FormatoARS.Moneda(SueldoEstimado);
public string TarifaTexto        => SistemaGimnacionOptimusCAI.Helpers.FormatoARS.MonedaCorta(TarifaHora) + "/h";
public string IngresosGenerTexto => SistemaGimnacionOptimusCAI.Helpers.FormatoARS.Moneda(IngresosGenerados);
```

---

## 5. CHECKLIST DE VERIFICACIÓN

### ✅ Fix error "Refresh durante AddNew o EditItem"
```
□ Abrir Reportes → Sueldos Docentes
□ El DataGrid NO tiene columnas editables (es IsReadOnly="True")
□ No hay doble click para editar celdas
□ La tarifa se edita SOLO desde el input de arriba
□ Al hacer click en "APLICAR A TODOS" no aparece ningún error
```

### ✅ Fix formato pesos argentinos
```
□ Los valores muestran "$ 4.000,00" (punto en miles, coma en decimales)
□ El campo TARIFA/H muestra "$ 4.000/h"
□ El campo SUELDO ESTIMADO muestra "$ 28.000,00" (no "$28.28")
□ El TOTAL A PAGAR muestra "$ 84.000,00" (suma de todos)
```

### ✅ Fix tarifa global
```
□ El input de tarifa muestra el valor actual (ej: 4.000)
□ Al cambiar a 5000 y clickear "APLICAR A TODOS":
  - Aparece notificación verde
  - Todos los sueldos se recalculan con la nueva tarifa
  - El total se actualiza
□ Al recargar la página el valor persiste (está en BD)
□ Ejecutar en SQL: SELECT valor FROM configuracion_sistema
  WHERE clave = 'tarifa_hora_docentes';
  → debe mostrar '5000.00'
```

### ✅ Alerta aguinaldo
```
□ Si el mes actual es junio o diciembre → aparece banner naranja
□ En otros meses → el banner está oculto
□ Para probar: temporalmente cambiar la condición a mes == DateTime.Today.Month
  para forzar que aparezca, verificar, y revertir
```

---

## 6. ERRORES COMUNES Y SOLUCIONES

| Error | Causa | Solución |
|-------|-------|----------|
| `"No se permite Refresh durante AddNew o EditItem"` | `ItemsSource` se reasigna con el DataGrid en edición | Agregar `if (gridSueldos.IsEditing) gridSueldos.CommitEdit(...)` antes del reload |
| `FormatoARS no existe` | No se creó el archivo o falta el `using` | Crear `Helpers/FormatoARS.cs` y agregar `using SistemaGimnacionOptimusCAI.Helpers;` |
| `sp_ObtenerConfiguracion no existe` | No se ejecutó el SQL del PASO 1 | Ejecutar PASO 1 y PASO 2 en el SQL Explorer |
| `Sueldo sigue en $0` | La tabla `configuracion_sistema` no tiene el valor | Ejecutar `SELECT * FROM configuracion_sistema` y verificar que existe la clave `tarifa_hora_docentes` |
| `TarifaTexto no compila` | `FormatoARS` está en otro namespace | Usar el namespace completo o agregar el `using` correspondiente |
| `dpSueldosDesde no existe` | El nombre del DatePicker es distinto | Buscar en el XAML el DatePicker de la sección sueldos y usar su nombre real |

---

## 7. NOTAS PARA EL FUTURO

### Si quieren subir la tarifa por inflación
```
Solo cambiar el valor en el input "Tarifa por hora" y clickear "APLICAR A TODOS".
No hay que tocar código. El valor se guarda en la tabla configuracion_sistema.
```

### Si en el futuro quieren tarifa diferente por instructor
```
Agregar columna tarifa_hora en tabla usuarios.
Modificar sp_ReporteSueldosDocentes para usar ISNULL(u.tarifa_hora, @TarifaGlobal).
Agregar columna editable en el DataGrid (usando CellEditingTemplate).
El instructor usa su tarifa propia si la tiene, si no usa la global.
```

### Gastos fijos mensuales (alquiler, sistema, etc.)
```
Pendiente de implementar según SDD_Reportes.md.
El admin los carga mes a mes en un formulario.
Se muestran en el reporte como egresos separados.
No se descuentan automáticamente del sueldo de los profes.
```

---

*SDD Fix Sueldos Docentes — OptimusCAI Gym v1.1 — Mayo 2026*  
*Bugs: error Refresh DataGrid + formato dólares + tarifa global ARS*
