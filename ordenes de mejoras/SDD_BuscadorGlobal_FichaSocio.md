# SDD — Buscador Global + Ficha del Socio
> Spec-Driven Development — Diseño basado en mockups fotográficos  
> Versión 1.0 — Mayo 2026  
> Leer COMPLETO antes de tocar cualquier archivo

---

## 0. CONTEXTO VISUAL — Análisis de los mockups

### Imagen 1 — Socio INACTIVO (Alvarez Enzo Gasto)
```
┌─────────────────────────────────────────────────┐
│  FICHA DEL SOCIO              [×]  [Editar Datos]│
│  [📷]  Nombre: ALVAREZ ENZO GASTO               │
│        DNI/ID: 36225242                          │
│        Fecha nacimiento: 18/02/1994  Sexo: F     │
│        Edad: 32 años                             │
│        Domicilio: reyes pasaje 21                │
│        Teléfono: 5493885181796                   │
│        Mail: enzoalvarezjr@gmail.com             │
│        Profesión:                                │
│        ¿Cómo nos conoció?:                       │
│        Observaciones: Sin dato                   │
│                                                  │
│            [✅ Restaurar Socio]                  │
│                                                  │
│  [🛒 Ver historial de compras] [📋 Ver todos]   │
└─────────────────────────────────────────────────┘
```

### Imagen 2 — Socio ACTIVO (Vilca Patricia)
```
┌─────────────────────────────────────────────────────────────────┐
│  FICHA DEL SOCIO              [×]  [Editar Datos]               │
│  [📷]  Nombre: VILCA PATRICIA      [Registrar Huella]           │
│        DNI/ID: 33173274            [Nueva Membresía]            │
│        Fecha nacimiento: 12/06/1987 [Cuenta Corriente]          │
│        Sexo: F   Edad: 38 años     [Rutinas]                    │
│        Domicilio: Sin dato         [Cobrar Cuenta]              │
│        Teléfono: 3886074534                                     │
│        Mail: Sin dato                                           │
│        Apto F. [Seguro]  [Anual]  [Casillero]                  │
│                                                                  │
│  ── ACTIVIDADES QUE REALIZA ─────────────────────────────────── │
│  Actividad              Vencimiento    Estado                    │
│  Gimnasio 3 Veces...    18/06/2026    AL DÍA 🟢                 │
│                                                                  │
│  [🗑 Dar de baja]                                               │
│  [🛒 Ver historial de compras] [📋 Ver todos los pagos]        │
└─────────────────────────────────────────────────────────────────┘
```

---

## 1. REGLAS — No negociables

```
- C# 7.3 estricto. Sin switch expressions ni features de C# 8+
- SQL Server LocalDB: DROP + CREATE en SPs
- Sin SQL inline — solo Stored Procedures
- La FichaSocioWindow es una Window con WindowStyle="None", Owner=MainWindow
- El buscador usa un Popup o ListBox desplegable (NO ComboBox nativo)
  porque necesita mostrar foto + nombre + DNI en cada fila
- Fondo oscuro: #0A0A14 para toda la ficha
- Título "FICHA DEL SOCIO" en color naranja/rojo: #FF4400
- Labels de datos en #FF4400 (rojo-naranja como en el mockup)
- Valores de datos en #E8E8FF (blanco)
- Botones del panel derecho: azul #1565C0 con borde redondeado
- Botón "Restaurar Socio": verde #2E7D32 (solo si inactivo)
- Botón "Dar de baja": rojo #C62828 (solo si activo)
- SesionManager para permisos: admin ve todo, empleado ve solo editar datos
```

---

## 2. FUNCIONALIDADES REQUERIDAS

### F-01 — Buscador global con múltiples resultados
- Campo de búsqueda en la barra superior del MainWindow (ya existe)
- Al escribir 2+ caracteres → despliega un **ListBox flotante** (Popup) con TODAS las coincidencias
- Busca simultáneamente por: nombre, apellido, DNI
- Cada fila del ListBox muestra: foto circular mini + nombre completo + DNI + badge estado (verde "Activo" / rojo "Inactivo")
- Al hacer click en una fila → cierra el popup y abre FichaSocioWindow con ese socio
- Al presionar Escape → cierra el popup
- Al presionar Enter → abre el primero de la lista

### F-02 — FichaSocioWindow: Modo INACTIVO
Botones visibles (panel derecho):
- `[Editar Datos]` → azul — abre formulario de edición de datos personales

Botón central:
- `[✅ Restaurar Socio]` → verde — reactiva al socio (activo = 1)

NO se muestran: actividades, membresías, rutinas, cobrar cuenta

Botones inferiores:
- `[🛒 Ver historial de compras]`
- `[📋 Ver todos los pagos y asistencias]`

### F-03 — FichaSocioWindow: Modo ACTIVO
Botones visibles (panel derecho, de arriba a abajo):
- `[👤 Editar Datos]` → azul
- `[✋ Registrar Huella]` → azul
- `[🎫 Nueva Membresía]` → azul
- `[💳 Cuenta Corriente]` → azul
- `[📋 Rutinas]` → azul
- `[💵 Cobrar Cuenta]` → verde más oscuro

Sección inferior dentro de la ficha:
- Título "ACTIVIDADES QUE REALIZA" en naranja
- Tabla con columnas: Actividad | Vencimiento | Estado
- Estado con punto de color: 🟢 "AL DÍA" / 🔴 "VENCIDA" / 🟡 "POR VENCER"

Botón inferior izquierdo:
- `[🗑 Dar de baja]` → rojo — desactiva al socio (activo = 0)

Botones inferiores:
- `[🛒 Ver historial de compras]`
- `[📋 Ver todos los pagos y asistencias]`

---

## 3. ARCHIVOS A CREAR/MODIFICAR

| Archivo | Cambio |
|---------|--------|
| `sp_BuscarSociosGlobal.sql` | 🟢 Nuevo — busca por nombre/apellido/DNI, retorna múltiples |
| `sp_ObtenerFichaSocio.sql` | 🟢 Nuevo — datos completos + membresías activas |
| `FichaSocioWindow.xaml` | 🔴 Reescribir completo con diseño del mockup |
| `FichaSocioWindow.xaml.cs` | 🔴 Reescribir con lógica de estados activo/inactivo |
| `MainWindow.xaml` | 🟡 Modificar buscador para mostrar Popup con lista |
| `MainWindow.xaml.cs` | 🟡 Modificar evento TextChanged del buscador |
| `SocioController.cs` | 🟡 Agregar métodos BuscarGlobal() y ObtenerFicha() |
| `SocioDao.cs` | 🟡 Agregar métodos para los nuevos SPs |

---

## 4. CÓDIGO — SQL

### SP 1 — `sp_BuscarSociosGlobal`

```sql
IF OBJECT_ID('sp_BuscarSociosGlobal','P') IS NOT NULL
    DROP PROCEDURE sp_BuscarSociosGlobal;
GO
CREATE PROCEDURE sp_BuscarSociosGlobal
    @Termino NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    -- Busca por nombre, apellido o DNI — retorna TODAS las coincidencias
    SELECT TOP 20
        s.id,
        s.nombre,
        s.apellido,
        s.nombre + ' ' + s.apellido     AS nombre_completo,
        s.dni,
        s.numero_socio,
        s.foto,
        s.activo,
        -- Membresía activa (si tiene)
        ISNULL(a.nombre, 'Sin membresía') AS actividad_actual,
        m.fecha_vencimiento
    FROM socios s
    LEFT JOIN membresias m
           ON m.socio_id = s.id
          AND m.estado = 'activa'
    LEFT JOIN actividades a
           ON a.id = m.actividad_id
    WHERE s.eliminado_en IS NULL
      AND (
          s.nombre    LIKE '%' + @Termino + '%'
       OR s.apellido  LIKE '%' + @Termino + '%'
       OR s.dni       LIKE '%' + @Termino + '%'
       OR (s.nombre + ' ' + s.apellido) LIKE '%' + @Termino + '%'
      )
    ORDER BY
        -- Primero los que empiezan con el término, luego los que contienen
        CASE WHEN s.apellido LIKE @Termino + '%' THEN 0 ELSE 1 END,
        s.apellido ASC,
        s.nombre   ASC;
END;
GO
```

### SP 2 — `sp_ObtenerFichaSocio`

```sql
IF OBJECT_ID('sp_ObtenerFichaSocio','P') IS NOT NULL
    DROP PROCEDURE sp_ObtenerFichaSocio;
GO
CREATE PROCEDURE sp_ObtenerFichaSocio
    @SocioId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    -- ─── Resultset 1: Datos del socio ────────────────────────────────
    SELECT
        s.id,
        s.nombre,
        s.apellido,
        s.nombre + ' ' + s.apellido     AS nombre_completo,
        s.dni,
        s.telefono,
        s.email,
        s.domicilio,
        s.numero_socio,
        s.foto,
        s.activo,
        s.observaciones,
        s.creado_en,
        -- Campos adicionales del mockup
        ISNULL(sfm.apto_fisico, 0)      AS apto_fisico,
        ISNULL(sfm.grupo_sanguineo,'')  AS grupo_sanguineo,
        -- Calcular edad si tiene fecha de nacimiento
        s.fecha_nacimiento,
        CASE
            WHEN s.fecha_nacimiento IS NOT NULL
            THEN DATEDIFF(YEAR, s.fecha_nacimiento, GETDATE())
            ELSE NULL
        END                              AS edad,
        -- Registrado por
        ISNULL(u.nombre + ' ' + u.apellido,'Sistema') AS registrado_por_nombre,
        -- Casillero asignado
        c.numero                         AS casillero_numero
    FROM socios s
    LEFT JOIN socios_ficha_medica sfm ON sfm.socio_id = s.id
    LEFT JOIN usuarios u ON u.id = s.registrado_por
    LEFT JOIN casilleros c ON c.socio_id = s.id AND c.estado = 'ocupado'
    WHERE s.id = @SocioId;

    -- ─── Resultset 2: Membresías/Actividades del socio ───────────────
    SELECT
        m.id                            AS membresia_id,
        a.nombre                        AS actividad_nombre,
        m.tipo_plan,
        m.fecha_inicio,
        m.fecha_vencimiento,
        m.estado,
        -- Estado amigable con días restantes
        CASE
            WHEN m.fecha_vencimiento < CAST(GETDATE() AS DATE)
                THEN 'VENCIDA'
            WHEN m.fecha_vencimiento <= DATEADD(DAY, 7, CAST(GETDATE() AS DATE))
                THEN 'POR VENCER'
            ELSE 'AL DÍA'
        END                             AS estado_display,
        DATEDIFF(DAY, CAST(GETDATE() AS DATE), m.fecha_vencimiento)
                                        AS dias_restantes
    FROM membresias m
    INNER JOIN actividades a ON a.id = m.actividad_id
    WHERE m.socio_id = @SocioId
      AND m.estado IN ('activa', 'vencida')
    ORDER BY m.estado ASC, m.fecha_vencimiento DESC;
END;
GO
```

---

## 5. CÓDIGO — Entity y DAO

### `Entities/FichaSocio.cs` (nuevo)

```csharp
// Entities/FichaSocio.cs — C# 7.3
using System;
using System.Collections.Generic;

namespace Entities
{
    /// <summary>Resultado completo de la ficha de un socio.</summary>
    public class FichaSocio
    {
        // ─── Datos personales ──────────────────────────────────────────
        public long     Id              { get; set; }
        public string   Nombre          { get; set; }
        public string   Apellido        { get; set; }
        public string   NombreCompleto  { get; set; }
        public string   Dni             { get; set; }
        public string   Telefono        { get; set; }
        public string   Email           { get; set; }
        public string   Domicilio       { get; set; }
        public int?     NumeroSocio     { get; set; }
        public byte[]   Foto            { get; set; }
        public bool     Activo          { get; set; }
        public string   Observaciones   { get; set; }
        public DateTime CreadoEn        { get; set; }
        public bool     AptoFisico      { get; set; }
        public string   GrupoSanguineo  { get; set; }
        public DateTime? FechaNacimiento{ get; set; }
        public int?     Edad            { get; set; }
        public string   RegistradoPor   { get; set; }
        public int?     CasilleroNumero { get; set; }

        // ─── Membresías ────────────────────────────────────────────────
        public List<FichaMembresia> Membresias { get; set; }
            = new List<FichaMembresia>();

        // ─── Calculadas ────────────────────────────────────────────────
        public string NumeroSocioTexto
            => NumeroSocio.HasValue ? "#" + NumeroSocio.Value.ToString("D4") : "—";

        public string EdadTexto
            => Edad.HasValue ? Edad.Value + " años" : "—";

        public string FechaNacTexto
            => FechaNacimiento.HasValue
               ? FechaNacimiento.Value.ToString("dd/MM/yyyy") : "Sin dato";

        public string EstadoTexto => Activo ? "ACTIVO" : "INACTIVO";
        public bool   TieneFoto   => Foto != null && Foto.Length > 0;
    }

    public class FichaMembresia
    {
        public long     MembresiaId      { get; set; }
        public string   ActividadNombre  { get; set; }
        public string   TipoPlan         { get; set; }
        public DateTime FechaInicio      { get; set; }
        public DateTime FechaVencimiento { get; set; }
        public string   Estado           { get; set; }
        public string   EstadoDisplay    { get; set; }  // "AL DÍA" / "POR VENCER" / "VENCIDA"
        public int      DiasRestantes    { get; set; }

        public string VencimientoTexto
            => FechaVencimiento.ToString("dd/MM/yyyy");

        public string DescripcionCompleta
            => ActividadNombre + " | " + TipoPlan;
    }

    /// <summary>Resultado resumido para el Popup de búsqueda.</summary>
    public class SocioResumenBusqueda
    {
        public long   Id             { get; set; }
        public string NombreCompleto { get; set; }
        public string Dni            { get; set; }
        public int?   NumeroSocio    { get; set; }
        public byte[] Foto           { get; set; }
        public bool   Activo         { get; set; }
        public string ActividadActual{ get; set; }

        public string EstadoTexto    => Activo ? "Activo" : "Inactivo";
        public string NumeroTexto    => NumeroSocio.HasValue
                                        ? "#" + NumeroSocio.Value.ToString("D4") : "";
    }
}
```

### Métodos a agregar en `SocioDao.cs`

```csharp
/// <summary>Busca socios por nombre, apellido o DNI. Retorna múltiples resultados.</summary>
public List<SocioResumenBusqueda> BuscarGlobal(string termino)
{
    var lista = new List<SocioResumenBusqueda>();
    using (var conn = GetConnection())
    {
        conn.Open();
        using (var cmd = new SqlCommand("sp_BuscarSociosGlobal", conn))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@Termino", termino ?? string.Empty);
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    lista.Add(new SocioResumenBusqueda
                    {
                        Id             = Convert.ToInt64(r["id"]),
                        NombreCompleto = r["nombre_completo"].ToString(),
                        Dni            = r["dni"].ToString(),
                        NumeroSocio    = r["numero_socio"] != DBNull.Value
                                            ? (int?)Convert.ToInt32(r["numero_socio"]) : null,
                        Foto           = r["foto"] != DBNull.Value
                                            ? (byte[])r["foto"] : null,
                        Activo         = Convert.ToBoolean(r["activo"]),
                        ActividadActual= r["actividad_actual"].ToString()
                    });
        }
    }
    return lista;
}

/// <summary>Obtiene la ficha completa de un socio con sus membresías.</summary>
public FichaSocio ObtenerFicha(long socioId)
{
    FichaSocio ficha = null;
    using (var conn = GetConnection())
    {
        conn.Open();
        using (var cmd = new SqlCommand("sp_ObtenerFichaSocio", conn))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@SocioId", socioId);

            using (var r = cmd.ExecuteReader())
            {
                // Resultset 1: datos del socio
                if (r.Read())
                    ficha = new FichaSocio
                    {
                        Id              = Convert.ToInt64(r["id"]),
                        Nombre          = r["nombre"].ToString(),
                        Apellido        = r["apellido"].ToString(),
                        NombreCompleto  = r["nombre_completo"].ToString(),
                        Dni             = r["dni"].ToString(),
                        Telefono        = r["telefono"] as string,
                        Email           = r["email"] as string,
                        Domicilio       = r["domicilio"] as string,
                        NumeroSocio     = r["numero_socio"] != DBNull.Value
                                            ? (int?)Convert.ToInt32(r["numero_socio"]) : null,
                        Foto            = r["foto"] != DBNull.Value
                                            ? (byte[])r["foto"] : null,
                        Activo          = Convert.ToBoolean(r["activo"]),
                        Observaciones   = r["observaciones"] as string,
                        CreadoEn        = Convert.ToDateTime(r["creado_en"]),
                        AptoFisico      = Convert.ToBoolean(r["apto_fisico"]),
                        GrupoSanguineo  = r["grupo_sanguineo"] as string,
                        FechaNacimiento = r["fecha_nacimiento"] != DBNull.Value
                                            ? (DateTime?)Convert.ToDateTime(r["fecha_nacimiento"]) : null,
                        Edad            = r["edad"] != DBNull.Value
                                            ? (int?)Convert.ToInt32(r["edad"]) : null,
                        RegistradoPor   = r["registrado_por_nombre"] as string,
                        CasilleroNumero = r["casillero_numero"] != DBNull.Value
                                            ? (int?)Convert.ToInt32(r["casillero_numero"]) : null
                    };

                // Resultset 2: membresías
                if (ficha != null && r.NextResult())
                    while (r.Read())
                        ficha.Membresias.Add(new FichaMembresia
                        {
                            MembresiaId     = Convert.ToInt64(r["membresia_id"]),
                            ActividadNombre = r["actividad_nombre"].ToString(),
                            TipoPlan        = r["tipo_plan"].ToString(),
                            FechaInicio     = Convert.ToDateTime(r["fecha_inicio"]),
                            FechaVencimiento= Convert.ToDateTime(r["fecha_vencimiento"]),
                            Estado          = r["estado"].ToString(),
                            EstadoDisplay   = r["estado_display"].ToString(),
                            DiasRestantes   = Convert.ToInt32(r["dias_restantes"])
                        });
            }
        }
    }
    return ficha;
}
```

---

## 6. CÓDIGO — UI XAML

### `MainWindow.xaml` — Buscador con Popup de resultados

**Reemplazar** el TextBox del buscador global por esta estructura:

```xml
<!-- Buscador global con popup de resultados múltiples -->
<Grid x:Name="gridBuscador" Width="360" VerticalAlignment="Center"
      Margin="20,0,0,0">

    <!-- Campo de búsqueda -->
    <Border Background="#16162A" CornerRadius="10"
            BorderBrush="#252540" BorderThickness="1.5"
            Height="38">
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="38"/>
                <ColumnDefinition Width="*"/>
                <ColumnDefinition Width="Auto"/>
            </Grid.ColumnDefinitions>
            <TextBlock Grid.Column="0" Text="🔍" FontSize="14"
                       VerticalAlignment="Center"
                       HorizontalAlignment="Center"
                       Foreground="#6A6A9A"/>
            <TextBox x:Name="txtBuscadorGlobal" Grid.Column="1"
                     BorderThickness="0" Background="Transparent"
                     Foreground="#E8E8FF" CaretBrush="#00CFFF"
                     FontSize="12" VerticalContentAlignment="Center"
                     Padding="0,0,6,0"
                     TextChanged="txtBuscadorGlobal_TextChanged"
                     KeyDown="txtBuscadorGlobal_KeyDown"
                     LostFocus="txtBuscadorGlobal_LostFocus"/>
            <!-- Placeholder -->
            <TextBlock Grid.Column="1"
                       Text="Buscar socio por nombre, apellido o DNI..."
                       IsHitTestVisible="False"
                       VerticalAlignment="Center"
                       FontSize="11" Foreground="#3A3A5C">
                <TextBlock.Style>
                    <Style TargetType="TextBlock">
                        <Setter Property="Visibility" Value="Collapsed"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding Text, ElementName=txtBuscadorGlobal}"
                                         Value="">
                                <Setter Property="Visibility" Value="Visible"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </TextBlock.Style>
            </TextBlock>
            <!-- Botón limpiar -->
            <Button Grid.Column="2" x:Name="btnLimpiarBusqueda"
                    Content="✕" Width="28" Height="28"
                    Background="Transparent" BorderThickness="0"
                    Foreground="#6A6A9A" FontSize="11"
                    Cursor="Hand" Visibility="Collapsed"
                    Click="btnLimpiarBusqueda_Click"/>
        </Grid>
    </Border>

    <!-- Popup de resultados -->
    <Popup x:Name="popupResultados"
           PlacementTarget="{Binding ElementName=gridBuscador}"
           Placement="Bottom"
           AllowsTransparency="True"
           StaysOpen="False"
           Width="360">
        <Border Background="#12121E"
                BorderBrush="#252540" BorderThickness="1.5"
                CornerRadius="0,0,10,10"
                MaxHeight="320">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                </Grid.RowDefinitions>

                <!-- Contador de resultados -->
                <TextBlock x:Name="lblContadorResultados"
                           Grid.Row="0"
                           FontSize="10" Foreground="#6A6A9A"
                           Padding="12,6,12,4"/>

                <!-- Lista de resultados -->
                <ListBox x:Name="listResultados" Grid.Row="1"
                         Background="Transparent"
                         BorderThickness="0"
                         ScrollViewer.HorizontalScrollBarVisibility="Disabled"
                         SelectionChanged="listResultados_SelectionChanged">
                    <ListBox.ItemContainerStyle>
                        <Style TargetType="ListBoxItem">
                            <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
                            <Setter Property="Padding" Value="0"/>
                            <Setter Property="Background" Value="Transparent"/>
                            <Setter Property="Cursor" Value="Hand"/>
                            <Style.Triggers>
                                <Trigger Property="IsMouseOver" Value="True">
                                    <Setter Property="Background"
                                            Value="#1A1A2E"/>
                                </Trigger>
                                <Trigger Property="IsSelected" Value="True">
                                    <Setter Property="Background"
                                            Value="#1A1840"/>
                                </Trigger>
                            </Style.Triggers>
                        </Style>
                    </ListBox.ItemContainerStyle>

                    <ListBox.ItemTemplate>
                        <DataTemplate>
                            <!-- Fila de resultado: foto + datos + badge estado -->
                            <Border Padding="10,8" BorderBrush="#1A1A2E"
                                    BorderThickness="0,0,0,1">
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="38"/>
                                        <ColumnDefinition Width="*"/>
                                        <ColumnDefinition Width="Auto"/>
                                    </Grid.ColumnDefinitions>

                                    <!-- Avatar circular -->
                                    <Grid Grid.Column="0">
                                        <Ellipse Width="34" Height="34">
                                            <Ellipse.Fill>
                                                <LinearGradientBrush>
                                                    <GradientStop Color="#00CFFF" Offset="0"/>
                                                    <GradientStop Color="#A78BFA" Offset="1"/>
                                                </LinearGradientBrush>
                                            </Ellipse.Fill>
                                        </Ellipse>
                                        <Ellipse Width="30" Height="30">
                                            <Ellipse.Fill>
                                                <ImageBrush
                                                    ImageSource="{Binding Foto,
                                                    Converter={StaticResource BytesAImagen}}"
                                                    Stretch="UniformToFill"/>
                                            </Ellipse.Fill>
                                        </Ellipse>
                                    </Grid>

                                    <!-- Nombre + DNI + actividad -->
                                    <StackPanel Grid.Column="1"
                                                VerticalAlignment="Center"
                                                Margin="8,0,0,0">
                                        <TextBlock Text="{Binding NombreCompleto}"
                                                   FontSize="12" FontWeight="SemiBold"
                                                   Foreground="#E8E8FF"/>
                                        <StackPanel Orientation="Horizontal">
                                            <TextBlock Text="{Binding Dni}"
                                                       FontSize="10" FontFamily="Consolas"
                                                       Foreground="#6A6A9A"/>
                                            <TextBlock Text=" · "
                                                       FontSize="10" Foreground="#3A3A5C"/>
                                            <TextBlock Text="{Binding NumeroTexto}"
                                                       FontSize="10" Foreground="#FF6B35"/>
                                        </StackPanel>
                                    </StackPanel>

                                    <!-- Badge activo/inactivo -->
                                    <Border Grid.Column="2"
                                            CornerRadius="10" Padding="8,2"
                                            VerticalAlignment="Center">
                                        <Border.Style>
                                            <Style TargetType="Border">
                                                <Setter Property="Background"
                                                        Value="#0A2A14"/>
                                                <Style.Triggers>
                                                    <DataTrigger Binding="{Binding Activo}"
                                                                 Value="False">
                                                        <Setter Property="Background"
                                                                Value="#2A0A0A"/>
                                                    </DataTrigger>
                                                </Style.Triggers>
                                            </Style>
                                        </Border.Style>
                                        <TextBlock Text="{Binding EstadoTexto}"
                                                   FontSize="9" FontWeight="Bold">
                                            <TextBlock.Style>
                                                <Style TargetType="TextBlock">
                                                    <Setter Property="Foreground"
                                                            Value="#00E676"/>
                                                    <Style.Triggers>
                                                        <DataTrigger
                                                            Binding="{Binding Activo}"
                                                            Value="False">
                                                            <Setter Property="Foreground"
                                                                    Value="#FF5555"/>
                                                        </DataTrigger>
                                                    </Style.Triggers>
                                                </Style>
                                            </TextBlock.Style>
                                        </TextBlock>
                                    </Border>
                                </Grid>
                            </Border>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>
            </Grid>
        </Border>
    </Popup>
</Grid>
```

---

### `FichaSocioWindow.xaml` — Diseño completo según mockup

```xml
<Window x:Class="SistemaGimnacionOptimusCAI.FichaSocioWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:helpers="clr-namespace:SistemaGimnacionOptimusCAI.Helpers"
        Title="Ficha del Socio"
        Width="860" Height="600"
        WindowStyle="None"
        ResizeMode="NoResize"
        WindowStartupLocation="CenterOwner"
        AllowsTransparency="False"
        Background="#0A0A14">

    <Window.Resources>
        <helpers:ByteToImageConverter x:Key="BytesAImagen"/>

        <!-- Estilo botón panel derecho -->
        <Style x:Key="BtnFichaEstilo" TargetType="Button">
            <Setter Property="Height"          Value="34"/>
            <Setter Property="Width"           Value="160"/>
            <Setter Property="Cursor"          Value="Hand"/>
            <Setter Property="FontSize"        Value="11"/>
            <Setter Property="FontWeight"      Value="SemiBold"/>
            <Setter Property="Foreground"      Value="White"/>
            <Setter Property="Margin"          Value="0,0,0,6"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border Background="{TemplateBinding Background}"
                                CornerRadius="6"
                                Padding="8,0">
                            <ContentPresenter HorizontalAlignment="Left"
                                              VerticalAlignment="Center"/>
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="44"/>  <!-- Barra título -->
            <RowDefinition Height="*"/>   <!-- Contenido -->
            <RowDefinition Height="Auto"/><!-- Barra inferior -->
        </Grid.RowDefinitions>

        <!-- ── BARRA TÍTULO ────────────────────────────────────── -->
        <Border Grid.Row="0" Background="#12121E"
                BorderBrush="#1A1A2E" BorderThickness="0,0,0,1">
            <Grid>
                <TextBlock Text="FICHA DEL SOCIO"
                           FontFamily="Bahnschrift SemiBold"
                           FontSize="16" FontWeight="Bold"
                           Foreground="#FF4400"
                           HorizontalAlignment="Center"
                           VerticalAlignment="Center"/>
                <Button Click="btnCerrar_Click"
                        HorizontalAlignment="Right"
                        VerticalAlignment="Center"
                        Margin="0,0,10,0"
                        Width="28" Height="28"
                        Background="#2A0A0A"
                        Foreground="#FF5555"
                        BorderThickness="1"
                        BorderBrush="#5A1A1A"
                        Cursor="Hand"
                        Content="✕">
                    <Button.Template>
                        <ControlTemplate TargetType="Button">
                            <Border Background="{TemplateBinding Background}"
                                    BorderBrush="{TemplateBinding BorderBrush}"
                                    BorderThickness="{TemplateBinding BorderThickness}"
                                    CornerRadius="6">
                                <ContentPresenter HorizontalAlignment="Center"
                                                  VerticalAlignment="Center"/>
                            </Border>
                        </ControlTemplate>
                    </Button.Template>
                </Button>
            </Grid>
        </Border>

        <!-- ── CONTENIDO PRINCIPAL ─────────────────────────────── -->
        <Grid Grid.Row="1" Margin="20,16,20,0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="80"/>   <!-- Foto -->
                <ColumnDefinition Width="*"/>    <!-- Datos -->
                <ColumnDefinition Width="180"/>  <!-- Panel botones -->
            </Grid.ColumnDefinitions>

            <!-- Foto circular -->
            <Grid Grid.Column="0" VerticalAlignment="Top" Margin="0,0,12,0">
                <Ellipse Width="68" Height="68">
                    <Ellipse.Fill>
                        <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                            <GradientStop Color="#FF4400" Offset="0"/>
                            <GradientStop Color="#A78BFA" Offset="1"/>
                        </LinearGradientBrush>
                    </Ellipse.Fill>
                </Ellipse>
                <Ellipse Width="64" Height="64">
                    <Ellipse.Fill>
                        <ImageBrush x:Name="imgFoto" Stretch="UniformToFill"/>
                    </Ellipse.Fill>
                </Ellipse>
                <!-- Icono cámara si no hay foto -->
                <TextBlock x:Name="lblIconoFoto" Text="📷"
                           FontSize="22"
                           HorizontalAlignment="Center"
                           VerticalAlignment="Center"
                           Opacity="0.5"/>
            </Grid>

            <!-- Datos del socio -->
            <StackPanel Grid.Column="1" Margin="0,0,16,0">

                <!-- Nombre grande + número de socio -->
                <StackPanel Orientation="Horizontal" Margin="0,0,0,10">
                    <TextBlock x:Name="lblNombreCompleto"
                               FontFamily="Bahnschrift SemiBold"
                               FontSize="18" FontWeight="Bold"
                               Foreground="#E8E8FF"/>
                    <TextBlock x:Name="lblNumeroSocio"
                               FontSize="12" FontFamily="Consolas"
                               Foreground="#FF6B35"
                               VerticalAlignment="Bottom"
                               Margin="10,0,0,2"/>
                </StackPanel>

                <!-- Grid de datos — 2 columnas -->
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <StackPanel Grid.Column="0">
                        <StackPanel Orientation="Horizontal" Margin="0,0,0,5">
                            <TextBlock Text="DNI/ID: " Foreground="#FF4400"
                                       FontSize="11" FontWeight="SemiBold"/>
                            <TextBlock x:Name="lblDni" Foreground="#E8E8FF"
                                       FontFamily="Consolas" FontSize="11"/>
                        </StackPanel>
                        <StackPanel Orientation="Horizontal" Margin="0,0,0,5">
                            <TextBlock Text="Fecha nacimiento: " Foreground="#FF4400"
                                       FontSize="11" FontWeight="SemiBold"/>
                            <TextBlock x:Name="lblFechaNac" Foreground="#E8E8FF"
                                       FontSize="11"/>
                        </StackPanel>
                        <StackPanel Orientation="Horizontal" Margin="0,0,0,5">
                            <TextBlock Text="Domicilio: " Foreground="#FF4400"
                                       FontSize="11" FontWeight="SemiBold"/>
                            <TextBlock x:Name="lblDomicilio" Foreground="#E8E8FF"
                                       FontSize="11" TextWrapping="Wrap" MaxWidth="200"/>
                        </StackPanel>
                        <StackPanel Orientation="Horizontal" Margin="0,0,0,5">
                            <TextBlock Text="Teléfono: " Foreground="#FF4400"
                                       FontSize="11" FontWeight="SemiBold"/>
                            <TextBlock x:Name="lblTelefono" Foreground="#E8E8FF"
                                       FontFamily="Consolas" FontSize="11"/>
                        </StackPanel>
                        <StackPanel Orientation="Horizontal" Margin="0,0,0,5">
                            <TextBlock Text="Mail: " Foreground="#FF4400"
                                       FontSize="11" FontWeight="SemiBold"/>
                            <TextBlock x:Name="lblMail" Foreground="#E8E8FF"
                                       FontSize="11"/>
                        </StackPanel>
                        <StackPanel Orientation="Horizontal" Margin="0,0,0,5">
                            <TextBlock Text="Observaciones: " Foreground="#FF4400"
                                       FontSize="11" FontWeight="SemiBold"/>
                            <TextBlock x:Name="lblObservaciones" Foreground="#A0A0C0"
                                       FontSize="11" FontStyle="Italic"
                                       TextWrapping="Wrap" MaxWidth="200"/>
                        </StackPanel>
                    </StackPanel>

                    <!-- Columna derecha: sexo, edad, extras -->
                    <StackPanel Grid.Column="1">
                        <StackPanel Orientation="Horizontal" Margin="0,0,0,5">
                            <TextBlock Text="Sexo: " Foreground="#FF4400"
                                       FontSize="11" FontWeight="SemiBold"/>
                            <TextBlock x:Name="lblSexo" Foreground="#E8E8FF"
                                       FontSize="11"/>
                        </StackPanel>
                        <StackPanel Orientation="Horizontal" Margin="0,0,0,5">
                            <TextBlock Text="Edad: " Foreground="#FF4400"
                                       FontSize="11" FontWeight="SemiBold"/>
                            <TextBlock x:Name="lblEdad" Foreground="#E8E8FF"
                                       FontSize="11"/>
                        </StackPanel>
                        <!-- Badges: Apto F / Seguro / Casillero -->
                        <StackPanel x:Name="panelBadges" Orientation="Horizontal"
                                    Margin="0,8,0,0">
                            <Border x:Name="badgeAptoF" Background="#0A2A14"
                                    CornerRadius="6" Padding="8,3" Margin="0,0,6,0"
                                    Visibility="Collapsed">
                                <TextBlock Text="Apto F." FontSize="10"
                                           FontWeight="Bold" Foreground="#00E676"/>
                            </Border>
                            <Border x:Name="badgeCasillero" Background="#0A1A2A"
                                    CornerRadius="6" Padding="8,3" Margin="0,0,6,0"
                                    Visibility="Collapsed">
                                <TextBlock x:Name="lblCasillero" FontSize="10"
                                           FontWeight="Bold" Foreground="#00CFFF"/>
                            </Border>
                        </StackPanel>
                    </StackPanel>
                </Grid>

                <!-- ── ACTIVIDADES (solo si ACTIVO) ─────────────── -->
                <Border x:Name="panelActividades"
                        Visibility="Collapsed"
                        BorderBrush="#252540" BorderThickness="0,1,0,0"
                        Margin="0,14,0,0" Padding="0,12,0,0">
                    <StackPanel>
                        <TextBlock Text="ACTIVIDADES QUE REALIZA"
                                   FontFamily="Bahnschrift SemiBold"
                                   FontSize="12" FontWeight="Bold"
                                   Foreground="#FF4400" Margin="0,0,0,8"/>

                        <!-- Header tabla -->
                        <Grid Margin="0,0,0,4">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="120"/>
                                <ColumnDefinition Width="120"/>
                            </Grid.ColumnDefinitions>
                            <TextBlock Grid.Column="0" Text="Actividad"
                                       FontSize="10" FontWeight="Bold"
                                       Foreground="#6A6A9A"/>
                            <TextBlock Grid.Column="1" Text="Vencimiento"
                                       FontSize="10" FontWeight="Bold"
                                       Foreground="#6A6A9A"
                                       HorizontalAlignment="Center"/>
                            <TextBlock Grid.Column="2" Text="Estado"
                                       FontSize="10" FontWeight="Bold"
                                       Foreground="#6A6A9A"
                                       HorizontalAlignment="Center"/>
                        </Grid>
                        <Border Height="1" Background="#252540" Margin="0,0,0,6"/>

                        <!-- Filas de membresías -->
                        <ItemsControl x:Name="listaActividades">
                            <ItemsControl.ItemTemplate>
                                <DataTemplate>
                                    <Grid Margin="0,0,0,6">
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="120"/>
                                            <ColumnDefinition Width="120"/>
                                        </Grid.ColumnDefinitions>
                                        <TextBlock Grid.Column="0"
                                                   Text="{Binding DescripcionCompleta}"
                                                   FontSize="11" Foreground="#C0C0E0"/>
                                        <TextBlock Grid.Column="1"
                                                   Text="{Binding VencimientoTexto}"
                                                   FontSize="11" Foreground="#A0A0C0"
                                                   HorizontalAlignment="Center"/>
                                        <StackPanel Grid.Column="2"
                                                    Orientation="Horizontal"
                                                    HorizontalAlignment="Center">
                                            <Ellipse Width="8" Height="8"
                                                     VerticalAlignment="Center"
                                                     Margin="0,0,5,0">
                                                <Ellipse.Style>
                                                    <Style TargetType="Ellipse">
                                                        <Setter Property="Fill" Value="#00E676"/>
                                                        <Style.Triggers>
                                                            <DataTrigger
                                                                Binding="{Binding EstadoDisplay}"
                                                                Value="VENCIDA">
                                                                <Setter Property="Fill" Value="#FF5555"/>
                                                            </DataTrigger>
                                                            <DataTrigger
                                                                Binding="{Binding EstadoDisplay}"
                                                                Value="POR VENCER">
                                                                <Setter Property="Fill" Value="#FFA726"/>
                                                            </DataTrigger>
                                                        </Style.Triggers>
                                                    </Style>
                                                </Ellipse.Style>
                                            </Ellipse>
                                            <TextBlock Text="{Binding EstadoDisplay}"
                                                       FontSize="10" FontWeight="Bold">
                                                <TextBlock.Style>
                                                    <Style TargetType="TextBlock">
                                                        <Setter Property="Foreground" Value="#00E676"/>
                                                        <Style.Triggers>
                                                            <DataTrigger
                                                                Binding="{Binding EstadoDisplay}"
                                                                Value="VENCIDA">
                                                                <Setter Property="Foreground" Value="#FF5555"/>
                                                            </DataTrigger>
                                                            <DataTrigger
                                                                Binding="{Binding EstadoDisplay}"
                                                                Value="POR VENCER">
                                                                <Setter Property="Foreground" Value="#FFA726"/>
                                                            </DataTrigger>
                                                        </Style.Triggers>
                                                    </Style>
                                                </TextBlock.Style>
                                            </TextBlock>
                                        </StackPanel>
                                    </Grid>
                                </DataTemplate>
                            </ItemsControl.ItemTemplate>
                        </ItemsControl>
                    </StackPanel>
                </Border>

            </StackPanel>

            <!-- ── PANEL BOTONES DERECHO ───────────────────────── -->
            <StackPanel Grid.Column="2" VerticalAlignment="Top">

                <!-- Botones siempre visibles -->
                <Button x:Name="btnEditarDatos" Content="👤  Editar Datos"
                        Style="{StaticResource BtnFichaEstilo}"
                        Background="#1565C0"
                        Click="btnEditarDatos_Click"/>

                <!-- Botones solo si ACTIVO -->
                <Button x:Name="btnRegistrarHuella" Content="✋  Registrar Huella"
                        Style="{StaticResource BtnFichaEstilo}"
                        Background="#1565C0"
                        Visibility="Collapsed"
                        Click="btnRegistrarHuella_Click"/>

                <Button x:Name="btnNuevaMembresia" Content="🎫  Nueva Membresía"
                        Style="{StaticResource BtnFichaEstilo}"
                        Background="#1565C0"
                        Visibility="Collapsed"
                        Click="btnNuevaMembresia_Click"/>

                <Button x:Name="btnCuentaCorriente" Content="💳  Cuenta Corriente"
                        Style="{StaticResource BtnFichaEstilo}"
                        Background="#1565C0"
                        Visibility="Collapsed"
                        Click="btnCuentaCorriente_Click"/>

                <Button x:Name="btnRutinas" Content="📋  Rutinas"
                        Style="{StaticResource BtnFichaEstilo}"
                        Background="#1565C0"
                        Visibility="Collapsed"
                        Click="btnRutinas_Click"/>

                <Button x:Name="btnCobrarCuenta" Content="💵  Cobrar Cuenta"
                        Style="{StaticResource BtnFichaEstilo}"
                        Background="#1B5E20"
                        Visibility="Collapsed"
                        Click="btnCobrarCuenta_Click"/>

            </StackPanel>
        </Grid>

        <!-- ── BARRA INFERIOR ──────────────────────────────────── -->
        <Border Grid.Row="2" Background="#0D0D22"
                BorderBrush="#1A1A2E" BorderThickness="0,1,0,0"
                Padding="20,10">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <!-- Botón DAR DE BAJA o RESTAURAR según estado -->
                <Button x:Name="btnDarDeBaja"
                        Content="🗑  Dar de baja"
                        Height="36" Width="140"
                        Background="#C62828" Foreground="White"
                        FontSize="11" FontWeight="SemiBold"
                        Cursor="Hand"
                        HorizontalAlignment="Left"
                        Visibility="Collapsed"
                        Click="btnDarDeBaja_Click">
                    <Button.Template>
                        <ControlTemplate TargetType="Button">
                            <Border Background="{TemplateBinding Background}"
                                    CornerRadius="8">
                                <ContentPresenter HorizontalAlignment="Center"
                                                  VerticalAlignment="Center"/>
                            </Border>
                        </ControlTemplate>
                    </Button.Template>
                </Button>

                <Button x:Name="btnRestaurarSocio"
                        Content="✅  Restaurar Socio"
                        Height="36" Width="160"
                        Background="#2E7D32" Foreground="White"
                        FontSize="11" FontWeight="SemiBold"
                        Cursor="Hand"
                        HorizontalAlignment="Left"
                        Visibility="Collapsed"
                        Click="btnRestaurarSocio_Click">
                    <Button.Template>
                        <ControlTemplate TargetType="Button">
                            <Border Background="{TemplateBinding Background}"
                                    CornerRadius="8">
                                <ContentPresenter HorizontalAlignment="Center"
                                                  VerticalAlignment="Center"/>
                            </Border>
                        </ControlTemplate>
                    </Button.Template>
                </Button>

                <!-- Links inferiores derecha -->
                <StackPanel Grid.Column="1" Orientation="Horizontal" Spacing="16">
                    <Button x:Name="btnHistorialCompras"
                            Background="Transparent" BorderThickness="0"
                            Cursor="Hand" Click="btnHistorialCompras_Click">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="🛒" FontSize="13" Margin="0,0,5,0"/>
                            <TextBlock Text="Ver historial de compras"
                                       FontSize="11" Foreground="#6A6A9A"
                                       TextDecorations="Underline"/>
                        </StackPanel>
                    </Button>
                    <Button x:Name="btnVerPagos"
                            Background="Transparent" BorderThickness="0"
                            Cursor="Hand" Margin="12,0,0,0"
                            Click="btnVerPagos_Click">
                        <StackPanel Orientation="Horizontal">
                            <TextBlock Text="📋" FontSize="13" Margin="0,0,5,0"/>
                            <TextBlock Text="Ver todos los pagos y asistencias"
                                       FontSize="11" Foreground="#6A6A9A"
                                       TextDecorations="Underline"/>
                        </StackPanel>
                    </Button>
                </StackPanel>
            </Grid>
        </Border>
    </Grid>
</Window>
```

---

## 7. CÓDIGO — Code-Behind

### `MainWindow.xaml.cs` — Buscador con popup

```csharp
// Campos privados a agregar en la clase MainWindow:
private readonly SocioController _socioCtrl = new SocioController();
private System.Windows.Threading.DispatcherTimer _timerBusqueda;

// ── En el constructor, inicializar el timer ──────────────────────────
private void InicializarBuscador()
{
    _timerBusqueda = new System.Windows.Threading.DispatcherTimer
    {
        Interval = TimeSpan.FromMilliseconds(300) // debounce 300ms
    };
    _timerBusqueda.Tick += TimerBusqueda_Tick;
}

// ── Evento TextChanged ───────────────────────────────────────────────
private void txtBuscadorGlobal_TextChanged(object sender, TextChangedEventArgs e)
{
    string texto = txtBuscadorGlobal.Text?.Trim() ?? string.Empty;
    btnLimpiarBusqueda.Visibility = texto.Length > 0
        ? Visibility.Visible : Visibility.Collapsed;

    if (texto.Length < 2)
    {
        popupResultados.IsOpen = false;
        _timerBusqueda.Stop();
        return;
    }

    // Reiniciar el timer (debounce — espera que el usuario termine de escribir)
    _timerBusqueda.Stop();
    _timerBusqueda.Start();
}

private void TimerBusqueda_Tick(object sender, EventArgs e)
{
    _timerBusqueda.Stop();
    EjecutarBusqueda(txtBuscadorGlobal.Text?.Trim());
}

private void EjecutarBusqueda(string termino)
{
    if (string.IsNullOrEmpty(termino) || termino.Length < 2) return;

    try
    {
        var resultados = _socioCtrl.BuscarGlobal(termino);
        listResultados.ItemsSource = resultados;

        if (resultados.Count == 0)
        {
            lblContadorResultados.Text = "Sin resultados para \"" + termino + "\"";
        }
        else
        {
            string s = resultados.Count == 1
                ? "1 socio encontrado"
                : resultados.Count + " socios encontrados";
            lblContadorResultados.Text = s;
        }

        popupResultados.IsOpen = resultados.Count > 0 || true; // siempre abrir
    }
    catch
    {
        popupResultados.IsOpen = false;
    }
}

// ── Selección de resultado ───────────────────────────────────────────
private void listResultados_SelectionChanged(object sender, SelectionChangedEventArgs e)
{
    var socio = listResultados.SelectedItem as SocioResumenBusqueda;
    if (socio == null) return;

    popupResultados.IsOpen   = false;
    txtBuscadorGlobal.Text   = string.Empty;
    listResultados.ItemsSource = null;
    btnLimpiarBusqueda.Visibility = Visibility.Collapsed;

    AbrirFichaSocio(socio.Id);
}

private void AbrirFichaSocio(long socioId)
{
    try
    {
        var ficha = _socioCtrl.ObtenerFicha(socioId);
        if (ficha == null)
        {
            // NotificacionWindow.MostrarError("No se encontró el socio.");
            return;
        }
        var ventana = new FichaSocioWindow(ficha) { Owner = this };
        ventana.ShowDialog();
    }
    catch (Exception ex)
    {
        // NotificacionWindow.MostrarError("Error al abrir la ficha.\n" + ex.Message);
    }
}

// ── Teclas del buscador ──────────────────────────────────────────────
private void txtBuscadorGlobal_KeyDown(object sender, KeyEventArgs e)
{
    if (e.Key == Key.Escape)
    {
        popupResultados.IsOpen = false;
        txtBuscadorGlobal.Text = string.Empty;
    }
    else if (e.Key == Key.Enter && listResultados.Items.Count > 0)
    {
        listResultados.SelectedIndex = 0;
    }
    else if (e.Key == Key.Down && listResultados.Items.Count > 0)
    {
        listResultados.Focus();
        listResultados.SelectedIndex = 0;
    }
}

private void txtBuscadorGlobal_LostFocus(object sender, RoutedEventArgs e)
{
    // Pequeño delay para permitir el click en el popup
    Task.Delay(200).ContinueWith(_ =>
        Dispatcher.Invoke(() =>
        {
            if (!listResultados.IsMouseOver)
                popupResultados.IsOpen = false;
        }));
}

private void btnLimpiarBusqueda_Click(object sender, RoutedEventArgs e)
{
    txtBuscadorGlobal.Text         = string.Empty;
    popupResultados.IsOpen         = false;
    btnLimpiarBusqueda.Visibility  = Visibility.Collapsed;
    txtBuscadorGlobal.Focus();
}
```

### `FichaSocioWindow.xaml.cs` — Lógica de estados

```csharp
// SistemaGimnacionOptimusCAI/FichaSocioWindow.xaml.cs — C# 7.3
using Controllers;
using Entities;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace SistemaGimnacionOptimusCAI
{
    public partial class FichaSocioWindow : Window
    {
        private readonly SocioController _ctrl = new SocioController();
        private FichaSocio _ficha;

        public FichaSocioWindow(FichaSocio ficha)
        {
            InitializeComponent();
            _ficha = ficha;
            CargarDatos();
        }

        private void CargarDatos()
        {
            // ─── Foto ─────────────────────────────────────────────────
            if (_ficha.TieneFoto)
            {
                imgFoto.ImageSource    = BytesABitmapImage(_ficha.Foto);
                lblIconoFoto.Visibility = Visibility.Collapsed;
            }

            // ─── Datos personales ─────────────────────────────────────
            lblNombreCompleto.Text = _ficha.NombreCompleto.ToUpper();
            lblNumeroSocio.Text    = _ficha.NumeroSocioTexto;
            lblDni.Text            = _ficha.Dni;
            lblFechaNac.Text       = _ficha.FechaNacTexto;
            lblEdad.Text           = _ficha.EdadTexto;
            lblDomicilio.Text      = string.IsNullOrEmpty(_ficha.Domicilio)
                                        ? "Sin dato" : _ficha.Domicilio;
            lblTelefono.Text       = string.IsNullOrEmpty(_ficha.Telefono)
                                        ? "Sin dato" : _ficha.Telefono;
            lblMail.Text           = string.IsNullOrEmpty(_ficha.Email)
                                        ? "Sin dato" : _ficha.Email;
            lblObservaciones.Text  = string.IsNullOrEmpty(_ficha.Observaciones)
                                        ? "Sin dato" : _ficha.Observaciones;

            // ─── Badges ───────────────────────────────────────────────
            if (_ficha.AptoFisico)
                badgeAptoF.Visibility = Visibility.Visible;

            if (_ficha.CasilleroNumero.HasValue)
            {
                lblCasillero.Text       = "Casillero #" + _ficha.CasilleroNumero;
                badgeCasillero.Visibility = Visibility.Visible;
            }

            // ─── Estado: ACTIVO o INACTIVO ────────────────────────────
            if (_ficha.Activo)
                ConfigurarModoActivo();
            else
                ConfigurarModoInactivo();
        }

        private void ConfigurarModoActivo()
        {
            // Mostrar botones del panel derecho
            btnRegistrarHuella.Visibility  = Visibility.Visible;
            btnNuevaMembresia.Visibility   = Visibility.Visible;
            btnCuentaCorriente.Visibility  = Visibility.Visible;
            btnRutinas.Visibility          = Visibility.Visible;
            btnCobrarCuenta.Visibility     = Visibility.Visible;

            // Botón dar de baja
            btnDarDeBaja.Visibility        = Visibility.Visible;
            btnRestaurarSocio.Visibility   = Visibility.Collapsed;

            // Sección de actividades
            if (_ficha.Membresias.Count > 0)
            {
                panelActividades.Visibility = Visibility.Visible;
                listaActividades.ItemsSource = _ficha.Membresias;
            }
        }

        private void ConfigurarModoInactivo()
        {
            // Solo botón editar datos — ocultar todo lo demás
            btnRegistrarHuella.Visibility  = Visibility.Collapsed;
            btnNuevaMembresia.Visibility   = Visibility.Collapsed;
            btnCuentaCorriente.Visibility  = Visibility.Collapsed;
            btnRutinas.Visibility          = Visibility.Collapsed;
            btnCobrarCuenta.Visibility     = Visibility.Collapsed;

            // Botón restaurar
            btnRestaurarSocio.Visibility   = Visibility.Visible;
            btnDarDeBaja.Visibility        = Visibility.Collapsed;

            // Sin actividades
            panelActividades.Visibility    = Visibility.Collapsed;
        }

        // ── EVENTOS DE BOTONES ────────────────────────────────────────

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
            => Close();

        private void btnEditarDatos_Click(object sender, RoutedEventArgs e)
        {
            // Navegar al módulo Socios con este socio preseleccionado
            // Opción A: cerrar la ficha y navegar
            // Opción B: abrir formulario inline
            // → Implementar según el patrón del proyecto
            Close();
            // (MainWindow navegará a SociosPage con el ID para preseleccionar)
        }

        private void btnRestaurarSocio_Click(object sender, RoutedEventArgs e)
        {
            bool confirmar = MessageBox.Show(
                "¿Restaurar al socio " + _ficha.NombreCompleto + "?\n" +
                "El socio volvería a estar activo en el sistema.",
                "Restaurar socio",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) == MessageBoxResult.Yes;

            if (!confirmar) return;

            try
            {
                var r = _ctrl.RestaurarSocio(_ficha.Id);
                if (r.ok)
                {
                    MessageBox.Show(r.mensaje, "Listo",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                else
                {
                    MessageBox.Show(r.mensaje, "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnDarDeBaja_Click(object sender, RoutedEventArgs e)
        {
            bool confirmar = MessageBox.Show(
                "¿Dar de baja al socio " + _ficha.NombreCompleto + "?\n" +
                "El socio quedará inactivo. Sus datos se conservan.",
                "Dar de baja",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes;

            if (!confirmar) return;

            try
            {
                var r = _ctrl.DarDeBajaSocio(_ficha.Id,
                    SesionManager.HaySesion ? SesionManager.UsuarioId : 1);
                if (r.ok)
                {
                    MessageBox.Show(r.mensaje, "Listo",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    Close();
                }
                else
                {
                    MessageBox.Show(r.mensaje, "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnNuevaMembresia_Click(object sender, RoutedEventArgs e)
        {
            // Cerrar ficha y abrir MembresiasPage con socio preseleccionado
            Close();
        }

        private void btnRegistrarHuella_Click(object sender, RoutedEventArgs e)
        {
            // Placeholder: módulo de huella dactilar (requiere hardware)
            MessageBox.Show("Módulo de huella dactilar — requiere hardware biométrico.",
                "Registrar huella", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnCuentaCorriente_Click(object sender, RoutedEventArgs e)
        {
            // Abrir vista de cuenta corriente del socio
            Close();
        }

        private void btnRutinas_Click(object sender, RoutedEventArgs e)
        {
            // Abrir módulo de rutinas con socio preseleccionado
            Close();
        }

        private void btnCobrarCuenta_Click(object sender, RoutedEventArgs e)
        {
            // Abrir modal de cobro
        }

        private void btnHistorialCompras_Click(object sender, RoutedEventArgs e)
        {
            // Abrir historial de compras del socio
        }

        private void btnVerPagos_Click(object sender, RoutedEventArgs e)
        {
            // Abrir historial de pagos y asistencias
        }

        // ── HELPER ───────────────────────────────────────────────────
        private static BitmapImage BytesABitmapImage(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption  = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                return bmp;
            }
        }
    }
}
```

---

## 8. CHECKLIST DE VERIFICACIÓN

### ✅ Buscador con múltiples resultados
```
□ Escribir "vi" → popup aparece con todos los socios que
  tengan "vi" en nombre, apellido o DNI
□ Cada fila del popup muestra: foto, nombre, DNI, badge Activo/Inactivo
□ Click en una fila → popup se cierra, abre FichaSocioWindow
□ Presionar Escape → cierra el popup
□ Presionar Enter → selecciona el primer resultado
□ Presionar ↓ → mueve el foco al ListBox
□ Escribir un DNI completo → aparece exactamente 1 resultado
□ Escribir algo sin resultados → popup muestra "Sin resultados para..."
```

### ✅ Ficha modo INACTIVO
```
□ Buscar un socio inactivo → abre la ficha
□ Solo se ve el botón "Editar Datos" en el panel derecho
□ Se ve el botón verde "✅ Restaurar Socio" abajo a la izquierda
□ NO se ve: Registrar Huella, Nueva Membresía, Rutinas, etc.
□ NO se ve la sección "ACTIVIDADES QUE REALIZA"
□ Hacer click en "Restaurar Socio" → confirmación → socio queda activo
□ Al reabrir la ficha de ese socio → ahora muestra modo ACTIVO
```

### ✅ Ficha modo ACTIVO
```
□ Buscar un socio activo → abre la ficha
□ Panel derecho muestra todos los botones: Editar Datos,
  Registrar Huella, Nueva Membresía, Cuenta Corriente, Rutinas, Cobrar Cuenta
□ Sección "ACTIVIDADES QUE REALIZA" visible con tabla de membresías
□ Estado "AL DÍA" → punto verde + texto verde
□ Estado "VENCIDA" → punto rojo + texto rojo
□ Estado "POR VENCER" → punto naranja + texto naranja
□ Botón "🗑 Dar de baja" visible abajo a la izquierda en rojo
□ Click en "Dar de baja" → confirmación → socio queda inactivo
```

---

## 9. ERRORES COMUNES Y SOLUCIONES

| Error | Causa | Solución |
|-------|-------|----------|
| Popup no aparece | `PlacementTarget` incorrecto | Verificar que apunta al `Grid` contenedor, no al `TextBox` |
| Click en popup cierra antes de registrar | `LostFocus` del TextBox | Usar `Task.Delay(200)` antes de cerrar |
| `FichaSocio` no tiene `fecha_nacimiento` | Columna no existe en tabla `socios` | Verificar schema; si no existe usar NULL en el SP |
| Resultados duplicados por múltiples membresías | El JOIN con membresías genera varias filas | Usar `SELECT DISTINCT` o agrupar por `s.id` en el SP |
| Botones del panel visible en modo incorrecto | El `Collapsed`/`Visible` no se aplica | Verificar que `ConfigurarModoActivo/Inactivo` se llama después de `InitializeComponent` |

---

*SDD Buscador Global + Ficha Socio — OptimusCAI Gym v1.0 — Mayo 2026*  
*Diseño basado en mockups fotográficos del sistema real*
