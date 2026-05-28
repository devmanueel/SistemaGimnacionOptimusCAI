# SDD — Nuevo flujo: Crear Socio + Membresía en ventana emergente
## Sistema Gimnasio OptimusCAI · SQL Server + WPF C# 7.3

---

## Contexto

### Flujo ACTUAL (a reemplazar)
1. Botón "Nuevo socio" → abre panel lateral con tabs (Datos / Contacto / Otros)
2. Completar datos → presionar "GUARDAR"
3. Mensaje de confirmación: "¿Querés asignarle una membresía?"
4. Al presionar Sí → navega a sección Membresías y abre panel lateral

### Flujo NUEVO
1. Botón "Nuevo socio" → abre **ventana emergente (Window)** con **2 pasos**
2. **Paso 1 — Datos del socio**: mismos campos que el panel lateral actual
3. Presionar "Siguiente" → valida y guarda el socio → pasa al **Paso 2**
4. **Paso 2 — Membresía**: combo de actividades, fecha de vencimiento (calculada automáticamente), combo de instructor (opcional), precio de la actividad seleccionada
5. Presionar "Cobrar" → guarda la membresía → pregunta si quiere registrar huella digital
6. Al responder (Sí o No) → cierra la ventana y recarga la tabla de socios

### Lo que NO cambia
- El panel lateral de **edición** de socio sigue igual (no se toca)
- Los SPs `sp_InsertarSocio` y `sp_InsertarMembresia` no se modifican
- El DAO, Controller y Entity de Socio no se modifican
- La lógica de huella se deja preparada pero no se implementa todavía

---

## Datos confirmados

| Dato | Valor |
|---|---|
| SP insertar socio | `sp_InsertarSocio` |
| SP insertar membresía | `sp_InsertarMembresia` (ya funciona) |
| SP listar actividades activas | `sp_ListarActividadesParaCombo` |
| SP listar instructores | `sp_ListarInstructoresParaCombo` (rolId = 2) |
| Campos del socio | Nombre*, Apellido*, DNI*, FechaNacimiento, Sexo, Teléfono*, Email, Domicilio, Profesión, ComoNosConoció, Observaciones, Foto |
| Instructor en membresía | Opcional (puede quedar en NULL) |
| Fecha vencimiento | Calculada automáticamente: hoy + 31 días |
| Registro de huella | Preguntar al final, pero NO implementar todavía |

### Verificaciones completadas ✅

| # | Verificación | Resultado |
|---|---|---|
| 1 | `sp_ListarActividadesParaCombo` devuelve columna `precio` | ✅ Confirmado. Devuelve id, nombre, tipo, dias_sesiones, precio, categoria, nivel. El binding `{Binding Precio}` y `actividad.Precio` son correctos. |
| 2 | `Actividad.Precio` es `decimal` | ✅ Confirmado. La entity tiene `public decimal Precio { get; set; }`. Sin cambios en XAML ni en code-behind. |
| 3 | Firma de `MembresiaController.Insertar` | ✅ Confirmado. `tipoPlan` es el último parámetro opcional (default `"mensual"`). La llamada en `EjecutarPaso2()` está alineada. Ver PASO 5 para detalle completo. |

---

## PASO 1 — SQL Server

No se requieren cambios en la BD. Los SPs existentes cubren todo el flujo nuevo.

Verificar que estos SPs existen y funcionan:

```sql
-- Listar actividades activas para el combo
EXEC sp_ListarActividadesParaCombo;

-- Listar instructores (rolId = 2)
EXEC sp_ListarInstructoresParaCombo;

-- Insertar socio (ya existe)
-- EXEC sp_InsertarSocio ...

-- Insertar membresía (ya existe)
-- EXEC sp_InsertarMembresia ...
```

---

## PASO 2 — Crear `NuevoSocioWindow.xaml`

Crear una nueva Window en la carpeta `Paginas` o `Ventanas`.

### Estructura visual

```
┌────────────────────────────────────────────────────────┐
│  NUEVO SOCIO                              [  ×  ]       │
│                                                        │
│  ● PASO 1: DATOS DEL SOCIO  ──────  PASO 2: MEMBRESÍA │
│  (barra de progreso con 2 pasos)                       │
│──────────────────────────────────────────────────────  │
│                                                        │
│  [contenido del paso actual]                           │
│                                                        │
│──────────────────────────────────────────────────────  │
│  [ Cancelar ]                    [ Siguiente / Cobrar ]│
└────────────────────────────────────────────────────────┘
```

### XAML completo

```xml
<Window x:Class="SistemaGimnacionOptimusCAI.Ventanas.NuevoSocioWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:fa="http://schemas.fontawesome.io/icons/"
        xmlns:helpers="clr-namespace:SistemaGimnacionOptimusCAI.Helpers"
        Title="Nuevo Socio"
        Width="560" Height="680"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent">

    <Window.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="/MiDiccionario.xaml"/>
            </ResourceDictionary.MergedDictionaries>

            <helpers:ByteToImageConverter x:Key="BytesAImagen"/>

            <Style x:Key="LabelErrorEstilo" TargetType="TextBlock">
                <Setter Property="FontSize"     Value="11"/>
                <Setter Property="Foreground"   Value="#FF5555"/>
                <Setter Property="Margin"       Value="4,-10,0,8"/>
                <Setter Property="FontStyle"    Value="Italic"/>
                <Setter Property="Visibility"   Value="Collapsed"/>
                <Setter Property="TextWrapping" Value="Wrap"/>
            </Style>
        </ResourceDictionary>
    </Window.Resources>

    <Border Background="{StaticResource Bg1}"
            CornerRadius="14"
            BorderBrush="{StaticResource Border1}"
            BorderThickness="1">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="3"/>      <!-- barra verde top -->
                <RowDefinition Height="Auto"/>   <!-- header -->
                <RowDefinition Height="Auto"/>   <!-- pasos -->
                <RowDefinition Height="*"/>      <!-- contenido -->
                <RowDefinition Height="Auto"/>   <!-- botones -->
            </Grid.RowDefinitions>

            <!-- Barra verde superior -->
            <Border Grid.Row="0" Background="{StaticResource GreenMid}" CornerRadius="14,14,0,0"/>

            <!-- Header -->
            <Grid Grid.Row="1" Margin="24,18,24,0">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>

                <StackPanel Grid.Column="0">
                    <TextBlock x:Name="lblTitulo" Text="NUEVO SOCIO"
                               FontSize="16" FontWeight="Bold"
                               Foreground="{StaticResource TextPrimary}"/>
                    <TextBlock x:Name="lblSubtitulo" Text="Completá los datos del nuevo socio"
                               FontSize="12" Foreground="{StaticResource TextMuted}"
                               Margin="0,2,0,0"/>
                </StackPanel>

                <Button Grid.Column="1" Content="✕"
                        Width="28" Height="28"
                        Background="Transparent" BorderThickness="0"
                        Foreground="{StaticResource TextMuted}"
                        FontSize="14" Cursor="Hand"
                        Click="btnCerrar_Click"/>
            </Grid>

            <!-- Indicador de pasos -->
            <Grid Grid.Row="2" Margin="24,16,24,0">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="40"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>

                <!-- Paso 1 -->
                <StackPanel Grid.Column="0" Orientation="Horizontal">
                    <Border x:Name="circuloPaso1" Width="28" Height="28" CornerRadius="14"
                            Background="{StaticResource GreenMain}">
                        <TextBlock Text="1" Foreground="Black" FontWeight="Bold" FontSize="13"
                                   HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <TextBlock x:Name="lblPaso1" Text="Datos del socio"
                               Foreground="{StaticResource GreenMain}"
                               FontSize="12" FontWeight="SemiBold"
                               VerticalAlignment="Center" Margin="8,0,0,0"/>
                </StackPanel>

                <!-- Línea conectora -->
                <Border Grid.Column="1" Height="2" Background="{StaticResource Border1}"
                        VerticalAlignment="Center" Margin="4,0"/>

                <!-- Paso 2 -->
                <StackPanel Grid.Column="2" Orientation="Horizontal">
                    <Border x:Name="circuloPaso2" Width="28" Height="28" CornerRadius="14"
                            Background="{StaticResource Bg3}"
                            BorderBrush="{StaticResource Border1}" BorderThickness="1">
                        <TextBlock Text="2" Foreground="{StaticResource TextMuted}"
                                   FontWeight="Bold" FontSize="13"
                                   HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                    <TextBlock x:Name="lblPaso2" Text="Membresía"
                               Foreground="{StaticResource TextMuted}"
                               FontSize="12" FontWeight="SemiBold"
                               VerticalAlignment="Center" Margin="8,0,0,0"/>
                </StackPanel>
            </Grid>

            <!-- Contenido (los dos pasos) -->
            <ScrollViewer Grid.Row="3"
                          VerticalScrollBarVisibility="Auto"
                          Padding="0,0,12,0"
                          Margin="24,16,12,0">
                <StackPanel>

                    <!-- ══ PASO 1: DATOS DEL SOCIO ══ -->
                    <StackPanel x:Name="panelPaso1">

                        <!-- Foto -->
                        <StackPanel HorizontalAlignment="Center" Margin="0,0,0,16">
                            <Grid>
                                <Ellipse Width="90" Height="90">
                                    <Ellipse.Fill><SolidColorBrush Color="#1C2A1C"/></Ellipse.Fill>
                                </Ellipse>
                                <Ellipse Width="84" Height="84" HorizontalAlignment="Center">
                                    <Ellipse.Fill>
                                        <ImageBrush x:Name="imgFoto" Stretch="UniformToFill"/>
                                    </Ellipse.Fill>
                                </Ellipse>
                                <Button Width="24" Height="24"
                                        VerticalAlignment="Bottom" HorizontalAlignment="Right"
                                        Cursor="Hand" Click="btnSubirFoto_Click">
                                    <Button.Template>
                                        <ControlTemplate TargetType="Button">
                                            <Border CornerRadius="12" Background="{StaticResource GreenMain}">
                                                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                                            </Border>
                                        </ControlTemplate>
                                    </Button.Template>
                                    <TextBlock Text="📷" FontSize="11"/>
                                </Button>
                            </Grid>
                        </StackPanel>

                        <!-- Nombre y Apellido en fila -->
                        <Grid Margin="0,0,0,0">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="12"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <StackPanel Grid.Column="0">
                                <TextBlock Text="NOMBRE *" Style="{StaticResource LabelCampoEstilo}"/>
                                <TextBox x:Name="txtNombre" Style="{StaticResource InputEstilo}"
                                         LostFocus="txtNombre_LostFocus"/>
                                <TextBlock x:Name="errNombre" Style="{StaticResource LabelErrorEstilo}"/>
                            </StackPanel>
                            <StackPanel Grid.Column="2">
                                <TextBlock Text="APELLIDO *" Style="{StaticResource LabelCampoEstilo}"/>
                                <TextBox x:Name="txtApellido" Style="{StaticResource InputEstilo}"
                                         LostFocus="txtApellido_LostFocus"/>
                                <TextBlock x:Name="errApellido" Style="{StaticResource LabelErrorEstilo}"/>
                            </StackPanel>
                        </Grid>

                        <!-- DNI y Fecha de nacimiento en fila -->
                        <Grid Margin="0,0,0,0">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="12"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <StackPanel Grid.Column="0">
                                <TextBlock Text="DNI * (7 u 8 dígitos)" Style="{StaticResource LabelCampoEstilo}"/>
                                <TextBox x:Name="txtDni" Style="{StaticResource InputEstilo}"
                                         MaxLength="8"
                                         PreviewTextInput="txtDni_PreviewTextInput"
                                         DataObject.Pasting="txtDni_Pasting"
                                         LostFocus="txtDni_LostFocus"/>
                                <TextBlock x:Name="errDni" Style="{StaticResource LabelErrorEstilo}"/>
                            </StackPanel>
                            <StackPanel Grid.Column="2">
                                <TextBlock Text="FECHA DE NACIMIENTO" Style="{StaticResource LabelCampoEstilo}"/>
                                <DatePicker x:Name="dpNacimiento"
                                            Style="{StaticResource DatePickerEstilo}"
                                            Margin="0,0,0,16"/>
                            </StackPanel>
                        </Grid>

                        <!-- Sexo y Teléfono en fila -->
                        <Grid Margin="0,0,0,0">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="12"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <StackPanel Grid.Column="0">
                                <TextBlock Text="SEXO" Style="{StaticResource LabelCampoEstilo}"/>
                                <ComboBox x:Name="cmbSexo" Style="{StaticResource ComboBoxEstilo}" Margin="0,0,0,14">
                                    <ComboBoxItem Content="Masculino" Tag="M"/>
                                    <ComboBoxItem Content="Femenino"  Tag="F"/>
                                    <ComboBoxItem Content="Otro"      Tag="Otro" IsSelected="True"/>
                                </ComboBox>
                            </StackPanel>
                            <StackPanel Grid.Column="2">
                                <TextBlock Text="CELULAR *" Style="{StaticResource LabelCampoEstilo}"/>
                                <TextBox x:Name="txtTelefono" Style="{StaticResource InputEstilo}"
                                         MaxLength="10"
                                         PreviewTextInput="txtTelefono_PreviewTextInput"
                                         DataObject.Pasting="txtTelefono_Pasting"
                                         LostFocus="txtTelefono_LostFocus"/>
                                <TextBlock x:Name="errTelefono" Style="{StaticResource LabelErrorEstilo}"/>
                            </StackPanel>
                        </Grid>

                        <!-- Email y Domicilio en fila -->
                        <Grid Margin="0,0,0,0">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="12"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <StackPanel Grid.Column="0">
                                <TextBlock Text="EMAIL" Style="{StaticResource LabelCampoEstilo}"/>
                                <TextBox x:Name="txtEmail" Style="{StaticResource InputEstilo}"
                                         LostFocus="txtEmail_LostFocus"/>
                                <TextBlock x:Name="errEmail" Style="{StaticResource LabelErrorEstilo}"/>
                            </StackPanel>
                            <StackPanel Grid.Column="2">
                                <TextBlock Text="DOMICILIO" Style="{StaticResource LabelCampoEstilo}"/>
                                <TextBox x:Name="txtDomicilio" Style="{StaticResource InputEstilo}"/>
                            </StackPanel>
                        </Grid>

                        <!-- Profesión y ¿Cómo nos conoció? en fila -->
                        <Grid Margin="0,0,0,0">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="12"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <StackPanel Grid.Column="0">
                                <TextBlock Text="PROFESIÓN" Style="{StaticResource LabelCampoEstilo}"/>
                                <TextBox x:Name="txtProfesion" Style="{StaticResource InputEstilo}"/>
                            </StackPanel>
                            <StackPanel Grid.Column="2">
                                <TextBlock Text="¿CÓMO NOS CONOCIÓ?" Style="{StaticResource LabelCampoEstilo}"/>
                                <ComboBox x:Name="cmbComoConocio"
                                          Style="{StaticResource ComboBoxEstilo}" Margin="0,0,0,14">
                                    <ComboBoxItem Content="Recomendación de amigo"/>
                                    <ComboBoxItem Content="Redes sociales"/>
                                    <ComboBoxItem Content="Pasé por la calle"/>
                                    <ComboBoxItem Content="Publicidad"/>
                                    <ComboBoxItem Content="Familiar"/>
                                    <ComboBoxItem Content="Otro" IsSelected="True"/>
                                </ComboBox>
                            </StackPanel>
                        </Grid>

                        <!-- Observaciones -->
                        <TextBlock Text="OBSERVACIONES" Style="{StaticResource LabelCampoEstilo}"/>
                        <Border Background="{StaticResource Bg2}" CornerRadius="8"
                                BorderBrush="{StaticResource Border2}" BorderThickness="1"
                                Margin="0,0,0,14">
                            <TextBox x:Name="txtObservaciones"
                                     Background="Transparent" BorderThickness="0"
                                     Foreground="{StaticResource TextPrimary}"
                                     CaretBrush="{StaticResource GreenMain}"
                                     FontSize="13" Padding="12,10"
                                     AcceptsReturn="True" TextWrapping="Wrap"
                                     Height="70" VerticalScrollBarVisibility="Auto"/>
                        </Border>
                    </StackPanel>

                    <!-- ══ PASO 2: MEMBRESÍA ══ -->
                    <StackPanel x:Name="panelPaso2" Visibility="Collapsed">

                        <!-- Info del socio recién creado -->
                        <Border Background="{StaticResource Bg2}" CornerRadius="10"
                                BorderBrush="{StaticResource Border2}" BorderThickness="0.5"
                                Padding="14,12" Margin="0,0,0,20">
                            <StackPanel Orientation="Horizontal">
                                <fa:ImageAwesome Icon="CheckCircle"
                                                 Foreground="{StaticResource GreenMain}"
                                                 Height="18" Width="18"
                                                 VerticalAlignment="Center" Margin="0,0,10,0"/>
                                <StackPanel>
                                    <TextBlock x:Name="lblSocioCreado"
                                               Foreground="{StaticResource TextPrimary}"
                                               FontSize="14" FontWeight="Bold"/>
                                    <TextBlock x:Name="lblNumeroSocio"
                                               Foreground="{StaticResource GreenMain}"
                                               FontSize="12"/>
                                </StackPanel>
                            </StackPanel>
                        </Border>

                        <!-- Actividad -->
                        <TextBlock Text="ACTIVIDAD *" Style="{StaticResource LabelCampoEstilo}"/>
                        <ComboBox x:Name="cmbActividad"
                                  Style="{StaticResource ComboBoxEstilo}"
                                  Margin="0,0,0,14"
                                  SelectionChanged="cmbActividad_SelectionChanged">
                            <ComboBox.ItemTemplate>
                                <DataTemplate>
                                    <StackPanel Orientation="Horizontal">
                                        <TextBlock Text="{Binding Nombre}" FontSize="13"/>
                                        <TextBlock Text=" — $" FontSize="13"
                                                   Foreground="{StaticResource TextMuted}"/>
                                        <TextBlock Text="{Binding Precio, StringFormat=N0}" FontSize="13"
                                                   Foreground="{StaticResource TextMuted}"/>
                                    </StackPanel>
                                </DataTemplate>
                            </ComboBox.ItemTemplate>
                        </ComboBox>

                        <!-- Precio de la actividad seleccionada -->
                        <Border x:Name="panelPrecio" Visibility="Collapsed"
                                Background="{StaticResource Bg2}" CornerRadius="10"
                                BorderBrush="{StaticResource GreenMain}" BorderThickness="1"
                                Padding="14,12" Margin="0,0,0,20">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="Auto"/>
                                </Grid.ColumnDefinitions>
                                <StackPanel Grid.Column="0">
                                    <TextBlock Text="PRECIO A COBRAR"
                                               Foreground="{StaticResource TextMuted}"
                                               FontSize="11" FontWeight="Bold"/>
                                    <TextBlock x:Name="lblActividad"
                                               Foreground="{StaticResource TextSecondary}"
                                               FontSize="12" Margin="0,2,0,0"/>
                                </StackPanel>
                                <TextBlock x:Name="lblPrecio" Grid.Column="1"
                                           FontSize="28" FontWeight="Bold"
                                           Foreground="{StaticResource GreenMain}"
                                           VerticalAlignment="Center"/>
                            </Grid>
                        </Border>

                        <!-- Fecha de vencimiento y Instructor en fila -->
                        <Grid Margin="0,0,0,0">
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="12"/>
                                <ColumnDefinition Width="*"/>
                            </Grid.ColumnDefinitions>
                            <StackPanel Grid.Column="0">
                                <TextBlock Text="FECHA DE VENCIMIENTO" Style="{StaticResource LabelCampoEstilo}"/>
                                <DatePicker x:Name="dpVencimiento"
                                            Style="{StaticResource DatePickerEstilo}"
                                            IsEnabled="False"
                                            Margin="0,0,0,14"/>
                                <TextBlock Text="Se calcula automáticamente (hoy + 31 días)"
                                           FontSize="10" FontStyle="Italic"
                                           Foreground="{StaticResource TextMuted}"
                                           Margin="0,-10,0,14"/>
                            </StackPanel>
                            <StackPanel Grid.Column="2">
                                <TextBlock Text="INSTRUCTOR A CARGO (opcional)"
                                           Style="{StaticResource LabelCampoEstilo}"/>
                                <ComboBox x:Name="cmbInstructor"
                                          Style="{StaticResource ComboBoxEstilo}"
                                          Margin="0,0,0,14">
                                    <ComboBox.ItemTemplate>
                                        <DataTemplate>
                                            <TextBlock Text="{Binding NombreCompleto}"/>
                                        </DataTemplate>
                                    </ComboBox.ItemTemplate>
                                </ComboBox>
                            </StackPanel>
                        </Grid>

                        <!-- Método de pago -->
                        <TextBlock Text="MÉTODO DE PAGO" Style="{StaticResource LabelCampoEstilo}"/>
                        <ComboBox x:Name="cmbMetodoPago"
                                  Style="{StaticResource ComboBoxEstilo}"
                                  Margin="0,0,0,14">
                            <ComboBoxItem Content="Efectivo"       Tag="efectivo"    IsSelected="True"/>
                            <ComboBoxItem Content="Transferencia"  Tag="transferencia"/>
                            <ComboBoxItem Content="Tarjeta"        Tag="tarjeta"/>
                        </ComboBox>

                    </StackPanel>
                </StackPanel>
            </ScrollViewer>

            <!-- Botones -->
            <Grid Grid.Row="4" Margin="24,12,24,20">
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="*"/>
                </Grid.ColumnDefinitions>
                <Button Grid.Column="0" x:Name="btnCancelar" Content="Cancelar"
                        Style="{StaticResource BotonDangerLargoEstilo}"
                        Margin="0,0,8,0" Click="btnCancelar_Click"/>
                <Button Grid.Column="1" x:Name="btnAccion" Content="SIGUIENTE →"
                        Style="{StaticResource BotonPrincipalEstilo}"
                        Margin="8,0,0,0" Click="btnAccion_Click"/>
            </Grid>
        </Grid>
    </Border>
</Window>
```

---

## PASO 3 — Crear `NuevoSocioWindow.xaml.cs`

Crear el code-behind en la misma carpeta.

```csharp
// ============================================================
//  Archivo: NuevoSocioWindow.xaml.cs
//
//  Ventana emergente de 2 pasos para crear socio + membresía.
//  Paso 1: datos del socio → guarda con sp_InsertarSocio
//  Paso 2: membresía → guarda con sp_InsertarMembresia
//  Compatible con C# 7.3
// ============================================================

using Controllers;
using Entities;
using Microsoft.Win32;
using SistemaGimnacionOptimusCAI.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace SistemaGimnacionOptimusCAI.Ventanas
{
    public partial class NuevoSocioWindow : Window
    {
        // ── Controllers ───────────────────────────────────────
        private readonly SocioController      _socioCtrl      = new SocioController();
        private readonly ActividadController  _actividadCtrl  = new ActividadController();
        private readonly UsuarioController    _usuarioCtrl    = new UsuarioController();
        private readonly MembresiaController  _membresiaCtrl  = new MembresiaController();

        // ── Estado interno ────────────────────────────────────
        private int    _pasoActual  = 1;
        private long   _socioId     = 0;
        private int    _numeroSocio = 0;
        private byte[] _fotoBytes   = null;

        public NuevoSocioWindow()
        {
            InitializeComponent();
            CargarCombos();
            dpVencimiento.SelectedDate = DateTime.Today.AddDays(31);
        }

        // ── Carga de combos ───────────────────────────────────
        private void CargarCombos()
        {
            try
            {
                // Actividades activas
                var actividades = _actividadCtrl.ObtenerActividadesActivas();
                cmbActividad.ItemsSource = actividades;

                // Instructores (rolId = 2)
                var instructores = _usuarioCtrl.ObtenerUsuariosActivosPorRol(2);
                // Agregar opción "Ninguno" al inicio
                var listaInstructores = new List<object>();
                listaInstructores.Add(new { NombreCompleto = "Ninguno", Id = (long?)null });
                foreach (var inst in instructores)
                    listaInstructores.Add(inst);
                cmbInstructor.ItemsSource  = listaInstructores;
                cmbInstructor.SelectedIndex = 0;
            }
            catch { /* silencioso */ }
        }

        // ── Botón principal (Siguiente / Cobrar) ──────────────
        private void btnAccion_Click(object sender, RoutedEventArgs e)
        {
            if (_pasoActual == 1)
                EjecutarPaso1();
            else
                EjecutarPaso2();
        }

        // ── PASO 1: validar y guardar socio ───────────────────
        private void EjecutarPaso1()
        {
            if (!ValidarPaso1()) return;

            string sexo = "Otro";
            var sexoItem = cmbSexo.SelectedItem as ComboBoxItem;
            if (sexoItem?.Tag != null) sexo = sexoItem.Tag.ToString();

            string comoConocio = (cmbComoConocio.SelectedItem as ComboBoxItem)?.Content?.ToString()
                                 ?? string.Empty;

            var resultado = _socioCtrl.Insertar(
                nombre:          txtNombre.Text.Trim(),
                apellido:        txtApellido.Text.Trim(),
                dni:             txtDni.Text.Trim(),
                fechaNacimiento: dpNacimiento.SelectedDate,
                sexo:            sexo,
                telefono:        txtTelefono.Text.Trim(),
                domicilio:       txtDomicilio.Text.Trim(),
                profesion:       txtProfesion.Text.Trim(),
                email:           txtEmail.Text.Trim(),
                comoNosConocio:  comoConocio,
                observaciones:   txtObservaciones.Text.Trim(),
                foto:            _fotoBytes,
                registradoPor:   SesionManager.HaySesion ? (long?)SesionManager.UsuarioId : null);

            if (!resultado.ok)
            {
                NotificacionWindow.MostrarError(resultado.mensaje);
                return;
            }

            // Guardar datos del socio creado para el paso 2
            _socioId     = resultado.socioCreado.Id;
            _numeroSocio = resultado.socioCreado.NumeroSocio;

            // Actualizar UI del paso 2
            lblSocioCreado.Text  = resultado.socioCreado.Apellido + ", " + resultado.socioCreado.Nombre;
            lblNumeroSocio.Text  = "#" + _numeroSocio.ToString("D4") + " — Socio registrado correctamente";

            // Pasar al paso 2
            IrAPaso2();
        }

        // ── PASO 2: validar y guardar membresía ───────────────
        private void EjecutarPaso2()
        {
            if (cmbActividad.SelectedItem == null)
            {
                NotificacionWindow.MostrarAdvertencia("Seleccioná una actividad para continuar.");
                return;
            }

            var actividad    = cmbActividad.SelectedItem as Actividad;
            var metodoPagoItem = cmbMetodoPago.SelectedItem as ComboBoxItem;
            string metodoPago  = metodoPagoItem?.Tag?.ToString() ?? "efectivo";

            // Instructor (puede ser null)
            long? instructorId = null;
            if (cmbInstructor.SelectedIndex > 0)
            {
                var inst = cmbInstructor.SelectedItem as Usuario;
                if (inst != null) instructorId = inst.Id;
            }

            DateTime fechaInicio      = DateTime.Today;
            DateTime fechaVencimiento = DateTime.Today.AddDays(31);

            var resultado = _membresiaCtrl.Insertar(
                socioId:          _socioId,
                actividadId:      actividad.Id,
                instructorId:     instructorId,
                fechaInicio:      fechaInicio,
                fechaVencimiento: fechaVencimiento,
                montoPagado:      actividad.Precio,
                metodoPago:       metodoPago,
                registradoPor:    SesionManager.HaySesion ? SesionManager.UsuarioId : 0L,
                observaciones:    null);

            if (!resultado.ok)
            {
                NotificacionWindow.MostrarError(resultado.mensaje);
                return;
            }

            // Preguntar por huella digital (no implementado todavía)
            bool registrarHuella = NotificacionWindow.MostrarConfirmacion(
                "Membresía creada correctamente.\n\n¿Querés registrar la huella digital del socio ahora?",
                "¡Todo listo!");

            if (registrarHuella)
            {
                // TODO: implementar registro de huella en una versión futura
                NotificacionWindow.MostrarAdvertencia(
                    "El registro de huella digital estará disponible próximamente.",
                    "Próximamente");
            }

            // Cerrar la ventana con resultado exitoso
            DialogResult = true;
            Close();
        }

        // ── Navegación entre pasos ────────────────────────────
        private void IrAPaso2()
        {
            _pasoActual = 2;

            // Mostrar paso 2, ocultar paso 1
            panelPaso1.Visibility = Visibility.Collapsed;
            panelPaso2.Visibility = Visibility.Visible;

            // Actualizar indicador visual
            circuloPaso2.Background    = (System.Windows.Media.Brush)FindResource("GreenMain");
            lblPaso2.Foreground        = (System.Windows.Media.Brush)FindResource("GreenMain");

            // Actualizar textos
            lblTitulo.Text      = "NUEVA MEMBRESÍA";
            lblSubtitulo.Text   = "Asigná una actividad al nuevo socio";
            btnAccion.Content   = "COBRAR ✓";
            btnCancelar.Content = "← Volver";
        }

        private void VolverAPaso1()
        {
            _pasoActual = 2; // No se puede volver al paso 1 si el socio ya fue guardado
            // El botón Cancelar en paso 2 solo cierra la ventana
        }

        // ── Cambio de actividad → mostrar precio ──────────────
        private void cmbActividad_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var actividad = cmbActividad.SelectedItem as Actividad;
            if (actividad == null)
            {
                panelPrecio.Visibility = Visibility.Collapsed;
                return;
            }

            lblActividad.Text      = actividad.Nombre;
            lblPrecio.Text         = "$" + actividad.Precio.ToString("N0");
            panelPrecio.Visibility = Visibility.Visible;
        }

        // ── Foto ──────────────────────────────────────────────
        private void btnSubirFoto_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title  = "Seleccionar foto del socio",
                Filter = "Imágenes (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png"
            };
            if (dialog.ShowDialog() != true) return;

            try
            {
                _fotoBytes = File.ReadAllBytes(dialog.FileName);
                imgFoto.ImageSource = BytesABitmapImage(_fotoBytes);
            }
            catch (Exception ex)
            {
                NotificacionWindow.MostrarError("No se pudo cargar la imagen.\n" + ex.Message);
            }
        }

        // ── Botones Cancelar / Cerrar ─────────────────────────
        private void btnCancelar_Click(object sender, RoutedEventArgs e)
        {
            if (_pasoActual == 2 && _socioId > 0)
            {
                // El socio ya fue guardado — preguntar si cerrar de todas formas
                bool cerrar = NotificacionWindow.MostrarConfirmacion(
                    "El socio ya fue registrado. ¿Cerrar sin asignar membresía?",
                    "¿Cerrar?");
                if (!cerrar) return;

                DialogResult = true; // igual recargamos la tabla
            }
            else
            {
                DialogResult = false;
            }
            Close();
        }

        private void btnCerrar_Click(object sender, RoutedEventArgs e)
        {
            btnCancelar_Click(sender, e);
        }

        // ── Validaciones Paso 1 ───────────────────────────────
        private bool ValidarPaso1()
        {
            bool ok = true;

            var e1 = Validador.ValidarNombre(txtNombre.Text, "El nombre");
            AplicarError(txtNombre, errNombre, e1);
            if (e1 != null) ok = false;

            var e2 = Validador.ValidarNombre(txtApellido.Text, "El apellido");
            AplicarError(txtApellido, errApellido, e2);
            if (e2 != null) ok = false;

            var e3 = Validador.ValidarDni(txtDni.Text);
            AplicarError(txtDni, errDni, e3);
            if (e3 != null) ok = false;

            if (dpNacimiento.SelectedDate.HasValue && dpNacimiento.SelectedDate.Value > DateTime.Today)
            {
                NotificacionWindow.MostrarError("La fecha de nacimiento no puede ser futura.");
                return false;
            }

            var e4 = Validador.ValidarTelefono(txtTelefono.Text);
            AplicarError(txtTelefono, errTelefono, e4);
            if (e4 != null) ok = false;

            var e5 = Validador.ValidarEmail(txtEmail.Text);
            AplicarError(txtEmail, errEmail, e5);
            if (e5 != null) ok = false;

            if (!ok)
                NotificacionWindow.MostrarAdvertencia("Hay campos con errores. Revisalos antes de continuar.");

            return ok;
        }

        // ── Validaciones inline al perder foco ────────────────
        private void txtNombre_LostFocus(object sender, RoutedEventArgs e)
            => AplicarError(txtNombre, errNombre, Validador.ValidarNombre(txtNombre.Text, "El nombre"));

        private void txtApellido_LostFocus(object sender, RoutedEventArgs e)
            => AplicarError(txtApellido, errApellido, Validador.ValidarNombre(txtApellido.Text, "El apellido"));

        private void txtDni_LostFocus(object sender, RoutedEventArgs e)
            => AplicarError(txtDni, errDni, Validador.ValidarDni(txtDni.Text));

        private void txtTelefono_LostFocus(object sender, RoutedEventArgs e)
            => AplicarError(txtTelefono, errTelefono, Validador.ValidarTelefono(txtTelefono.Text));

        private void txtEmail_LostFocus(object sender, RoutedEventArgs e)
            => AplicarError(txtEmail, errEmail, Validador.ValidarEmail(txtEmail.Text));

        private void txtDni_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
            => e.Handled = !Regex.IsMatch(e.Text, @"^\d$");

        private void txtDni_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string texto = (string)e.DataObject.GetData(typeof(string));
                if (!Regex.IsMatch(texto, @"^\d+$")) e.CancelCommand();
            }
            else e.CancelCommand();
        }

        private void txtTelefono_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
            => e.Handled = !Validador.EsCaracterTelefonoValido(e.Text);

        private void txtTelefono_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string texto = (string)e.DataObject.GetData(typeof(string)) ?? string.Empty;
                var sb = new System.Text.StringBuilder();
                foreach (char c in texto) if (char.IsDigit(c)) sb.Append(c);
                string resultado = sb.Length > 10 ? sb.ToString().Substring(0, 10) : sb.ToString();
                if (resultado.Length > 0)
                {
                    var tb = sender as TextBox;
                    if (tb != null) { tb.Text = resultado; tb.CaretIndex = tb.Text.Length; }
                }
                e.CancelCommand();
            }
            else e.CancelCommand();
        }

        // ── Helper: aplicar/limpiar error en campo ────────────
        private void AplicarError(TextBox campo, TextBlock label, string mensaje)
        {
            if (mensaje != null)
            {
                campo.BorderBrush     = System.Windows.Media.Brushes.Red;
                campo.BorderThickness = new Thickness(1.5);
                label.Text            = mensaje;
                label.Visibility      = Visibility.Visible;
            }
            else
            {
                campo.ClearValue(TextBox.BorderBrushProperty);
                campo.ClearValue(TextBox.BorderThicknessProperty);
                label.Text       = string.Empty;
                label.Visibility = Visibility.Collapsed;
            }
        }

        // ── Helper: bytes → imagen ────────────────────────────
        private static BitmapImage BytesABitmapImage(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption   = BitmapCacheOption.OnLoad;
                bmp.StreamSource  = ms;
                bmp.EndInit();
                return bmp;
            }
        }
    }
}
```

---

## PASO 4 — Modificar `SociosPage.xaml.cs`

### 4.1 Reemplazar `btnNuevo_Click`

Reemplazar el método existente con este:

```csharp
private void btnNuevo_Click(object sender, RoutedEventArgs e)
{
    var ventana = new NuevoSocioWindow
    {
        Owner = Window.GetWindow(this)
    };

    bool? resultado = ventana.ShowDialog();

    // Si el socio (o socio + membresía) fue creado, recargar la tabla
    if (resultado == true)
    {
        CargarSocios();
        ActualizarStats();
    }
}
```

### 4.2 Agregar el using al inicio del archivo

```csharp
using SistemaGimnacionOptimusCAI.Ventanas;
```

### 4.3 Eliminar el bloque de SesionManager.AbrirPanelAlNavegar del constructor

En el constructor de `SociosPage`, **eliminar** estas líneas si ya no se usan:

```csharp
// ELIMINAR estas líneas:
if (SesionManager.AbrirPanelAlNavegar)
{
    SesionManager.AbrirPanelAlNavegar = false;
    btnNuevo_Click(null, null);
}
```

---

## PASO 5 — `MembresiaController.Insertar` (firma verificada)

La firma fue verificada contra el código real del controller. No requiere cambios.

```csharp
// Firma real — MembresiaController.Insertar:
public (bool ok, string mensaje, long nuevoId) Insertar(
    long     socioId,
    long     actividadId,
    long?    instructorId,
    DateTime fechaInicio,
    DateTime fechaVencimiento,
    decimal  montoPagado,
    string   metodoPago,
    long     registradoPor,     // long, no long? — pasar 0L si no hay sesión
    string   observaciones,
    string   tipoPlan = "mensual")  // opcional, último parámetro
```

**Notas confirmadas:**
- `tipoPlan` va al final como parámetro opcional con default `"mensual"`. En `EjecutarPaso2()` no se pasa explícitamente, por lo que usa el default. Esto es correcto.
- `registradoPor` es `long` (no `long?`). La llamada usa `SesionManager.HaySesion ? SesionManager.UsuarioId : 0L`. Correcto.
- El controller ignora `fechaInicio` y `fechaVencimiento` recibidas y las recalcula internamente con `DateTime.Today` y `DateTime.Today.AddDays(31)`. Los valores que pasa el code-behind son redundantes pero no generan error.
- `montoPagado` toma directamente `actividad.Precio` (tipo `decimal`). Confirmado.

La llamada en `EjecutarPaso2()` ya está alineada con esta firma. **No se requieren ajustes.**

---

## Orden de ejecución

```
SQL Server
  1. Verificar que sp_ListarActividadesParaCombo devuelve precio
  2. Verificar que sp_ListarInstructoresParaCombo funciona con rolId = 2
  3. Verificar que sp_InsertarMembresia funciona correctamente

Visual Studio
  4. Crear carpeta "Ventanas" en el proyecto UI (si no existe)
  5. Crear NuevoSocioWindow.xaml con el XAML del paso 2
  6. Crear NuevoSocioWindow.xaml.cs con el code-behind del paso 3
  7. Registrar el namespace en el proyecto si es necesario
  8. Verificar la firma de MembresiaController.Insertar (paso 5)
     y ajustar la llamada en EjecutarPaso2() si es diferente
  9. Reemplazar btnNuevo_Click en SociosPage.xaml.cs (paso 4.1)
  10. Agregar el using de Ventanas en SociosPage.xaml.cs (paso 4.2)
  11. Eliminar bloque SesionManager.AbrirPanelAlNavegar (paso 4.3)
  12. Compilar y corregir errores de nombre de namespace si los hay

Pruebas
  13. Abrir sección Socios → presionar "Nuevo socio"
      → debe abrirse la ventana emergente (no el panel lateral)
  14. Paso 1: completar datos y presionar "Siguiente"
      → debe validar, guardar el socio y pasar al paso 2
  15. Paso 1: dejar campos obligatorios vacíos
      → debe mostrar errores inline sin avanzar
  16. Paso 2: seleccionar actividad
      → debe mostrar el precio automáticamente
  17. Paso 2: presionar "Cobrar"
      → debe guardar la membresía y preguntar por huella
  18. Paso 2: responder Sí a la huella
      → debe mostrar mensaje "próximamente" y cerrar
  19. Paso 2: responder No a la huella
      → debe cerrar directamente
  20. Cerrar la ventana con la X o Cancelar en paso 1
      → debe cerrar sin guardar nada
  21. Cancelar en paso 2 (socio ya guardado)
      → debe preguntar si cerrar sin membresía y recargar tabla
  22. Verificar que el panel lateral de EDICIÓN sigue funcionando igual
  23. Verificar que la tabla se recarga correctamente al cerrar la ventana
```

---

## Notas importantes

- **`Actividad.Precio`** — verificar que la entity `Actividad` tiene la propiedad `Precio` de tipo `decimal`. Si se llama diferente, ajustar el binding en el XAML y la llamada en `EjecutarPaso2()`.
- **Namespace de la ventana** — el SDD usa `SistemaGimnacionOptimusCAI.Ventanas`. Si preferís otra carpeta, ajustar el namespace en el XAML y el using en `SociosPage.xaml.cs`.
- **El panel lateral existente NO se toca** — solo se reemplaza `btnNuevo_Click`. Editar y todas las demás funciones del panel lateral siguen igual.
- **`SesionManager.AbrirPanelAlNavegar`** — si esta propiedad se usa en otras partes del sistema, no eliminarla, solo el bloque del constructor de `SociosPage`.
- **Instructor "Ninguno"** — se agrega manualmente al inicio del combo. Si `UsuarioController.ObtenerUsuariosActivosPorRol` devuelve un tipo específico, puede ser necesario crear una clase anónima compatible o agregar una opción vacía de otra forma.
