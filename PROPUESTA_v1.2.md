# PROPUESTA — Fixes y mejoras OptimusCAI
> Versión 1.2 — Mayo 2026  
> Documento técnico para Claude Code

---

## PROBLEMA 9 — Flujo directo: Guardar Socio → Asignar Membresía

**Qué se pide:** Cuando el usuario guarda un socio nuevo exitosamente, en lugar de
quedarse en SociosPage, el sistema debe navegar automáticamente a MembresiasPage y
abrir el panel lateral de alta de membresía con los datos del socio recién creado
ya cargados (nombre, número de socio), listo para asignar actividad y método de pago.

---

### Contexto del flujo actual (bug/limitación)

```
[SociosPage] Usuario completa datos → presiona Guardar
    → SP sp_InsertarSocio devuelve el id del nuevo socio
    → Controller retorna (true, "Socio guardado")
    → SociosPage muestra notificación de éxito
    → *** el usuario queda en SociosPage sin membresía asignada ***
```

### Flujo nuevo esperado

```
[SociosPage] Usuario completa datos → presiona Guardar
    → sp_InsertarSocio devuelve id + número de socio
    → SocioController.Insertar() retorna (true, mensaje, socioId, numeroSocio)
    → SociosPage detecta que es un socio nuevo (no edición)
    → Llama a MainWindow para navegar a MembresiasPage
    → MembresiasPage abre el panel lateral con el socio pre-cargado
```

---

### Paso 1 — Modificar el SP `sp_InsertarSocio` para devolver más datos

El SP actual probablemente devuelve solo el `id` con `SCOPE_IDENTITY()`. Extender
el SELECT final para incluir también el `numero_socio` y `nombre_completo`:

```sql
-- Al final del SP sp_InsertarSocio, reemplazar:
--   SELECT SCOPE_IDENTITY() AS id;
-- Por:

DECLARE @NuevoId BIGINT = SCOPE_IDENTITY();

SELECT
    @NuevoId                        AS id,
    s.numero_socio,
    s.nombre + ' ' + s.apellido     AS nombre_completo
FROM socios s
WHERE s.id = @NuevoId;
```

---

### Paso 2 — Modificar `SocioController.Insertar()` para retornar el objeto creado

Cambiar la firma del método para devolver también el socio creado en caso de éxito.
Respetar C# 7.3 (tuplas, sin `out` pattern nuevo):

```csharp
// ANTES:
public (bool ok, string mensaje) Insertar(string nombre, string apellido, ...)
{
    long id = _dao.InsertarSocio(...);
    if (id <= 0) return (false, "No se pudo guardar el socio.");
    Auditor.Registrar("crear", "socio", id, ...);
    return (true, "Socio guardado correctamente.");
}

// DESPUÉS:
public (bool ok, string mensaje, Socio socioCreado) Insertar(string nombre, string apellido, ...)
{
    Socio socioCreado = _dao.InsertarSocio(...);  // DAO ahora retorna Socio en lugar de long
    
    if (socioCreado == null || socioCreado.Id <= 0)
        return (false, "No se pudo guardar el socio.", null);

    Auditor.Registrar("crear", "socio", socioCreado.Id, new Dictionary<string, object> {
        { "nombre",       nombre   },
        { "apellido",     apellido },
        { "numero_socio", socioCreado.NumeroSocio }
    });

    return (true, "Socio guardado correctamente.", socioCreado);
}
```

---

### Paso 3 — Modificar `SocioDao.InsertarSocio()` para retornar el Socio

```csharp
// ANTES: retornaba long (el id)
public long InsertarSocio(string nombre, string apellido, ...)

// DESPUÉS: retorna Socio (con id y numero_socio ya mapeados)
public Socio InsertarSocio(string nombre, string apellido, ...)
{
    using (SqlConnection con = new SqlConnection(_connStr))
    using (SqlCommand cmd = new SqlCommand("sp_InsertarSocio", con))
    {
        cmd.CommandType = CommandType.StoredProcedure;
        // ... parámetros igual que antes ...

        con.Open();
        using (SqlDataReader r = cmd.ExecuteReader())
        {
            if (r.Read())
            {
                return new Socio
                {
                    Id           = Convert.ToInt64(r["id"]),
                    NumeroSocio  = Convert.ToInt32(r["numero_socio"]),
                    NombreCompleto = r["nombre_completo"].ToString()
                    // El resto de propiedades no son necesarias para el redirect
                };
            }
        }
    }
    return null;
}
```

---

### Paso 4 — Agregar método de navegación en `MainWindow.xaml.cs`

El MainWindow ya maneja la navegación entre páginas con un Frame. Agregar un método
público que permita a cualquier Page solicitarle una navegación con datos:

```csharp
// En MainWindow.xaml.cs — agregar método público:

/// <summary>
/// Navega a MembresiasPage y opcionalmente pre-carga un socio para alta de membresía.
/// </summary>
public void NavegarAMembresiasConSocio(Socio socio)
{
    // 1. Marcar el botón activo en el sidebar (el mismo mecanismo que ya existe)
    MarcarBotonActivo("Membresías");  // ajustar al nombre exacto del botón en el menú

    // 2. Instanciar la página pasando el socio
    var pagina = new MembresiasPage(socio);

    // 3. Navegar
    FrameContenido.Navigate(pagina);  // ajustar al nombre del Frame en MainWindow.xaml
}
```

> **Nota para Claude Code**: verificar el nombre exacto del Frame y el mecanismo de
> botón activo en el MainWindow existente. El método debe seguir el mismo patrón
> que ya se usa al hacer click en los items del sidebar.

---

### Paso 5 — Modificar `SociosPage.xaml.cs` — disparar la navegación tras guardar

```csharp
// En el método que maneja el click de Guardar (BtnGuardar_Click o similar):

private void BtnGuardar_Click(object sender, RoutedEventArgs e)
{
    // ... validaciones existentes ...

    var resultado = _controller.Insertar(
        txtNombre.Text.Trim(),
        txtApellido.Text.Trim(),
        // ... resto de parámetros ...
        registradoPor: USUARIO_ACTUAL_ID
    );

    if (!resultado.ok)
    {
        NotificacionWindow.MostrarError(resultado.mensaje);
        return;
    }

    // NUEVO: si es un socio nuevo (no edición), preguntar si quiere asignar membresía
    if (_esNuevo && resultado.socioCreado != null)
    {
        bool asignar = NotificacionWindow.MostrarConfirmacion(
            "Socio guardado correctamente.\n\n¿Querés asignarle una membresía ahora?",
            "¡Socio creado!"
        );

        if (asignar)
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.NavegarAMembresiasConSocio(resultado.socioCreado);
            return;  // salir sin limpiar el form — ya nos vamos de la página
        }
    }

    // Flujo normal: limpiar form y recargar lista
    NotificacionWindow.MostrarExito(resultado.mensaje);
    LimpiarFormulario();
    CargarSocios();
}
```

> **Variante sin confirmación**: si se prefiere navegar directo sin preguntar,
> reemplazar el bloque con confirmación por la llamada directa:
> ```csharp
> var mainWindow = Window.GetWindow(this) as MainWindow;
> mainWindow?.NavegarAMembresiasConSocio(resultado.socioCreado);
> return;
> ```

---

### Paso 6 — Modificar `MembresiasPage.xaml.cs` — aceptar socio pre-cargado

```csharp
// ANTES: constructor sin parámetros
public MembresiasPage()
{
    InitializeComponent();
    // ... carga inicial ...
}

// DESPUÉS: constructor sobrecargado que acepta un socio pre-cargado
private Socio _socioPreCargado = null;

public MembresiasPage() : this(null) { }

public MembresiasPage(Socio socioPreCargado)
{
    InitializeComponent();
    _socioPreCargado = socioPreCargado;
    // ... carga inicial existente (CargarMembresias, CargarActividades, etc.) ...
}

// En el evento Loaded de la página:
private void Page_Loaded(object sender, RoutedEventArgs e)
{
    // ... lógica de carga existente ...

    // NUEVO: si venimos desde SociosPage con un socio pre-cargado, abrir el panel
    if (_socioPreCargado != null)
    {
        AbrirPanelNuevaMembresia(_socioPreCargado);
        _socioPreCargado = null;  // limpiar para no re-abrir si la página se recarga
    }
}

// Método que abre el panel lateral y pre-carga los datos del socio:
private void AbrirPanelNuevaMembresia(Socio socio)
{
    _esNuevo  = true;
    _idEditar = 0;

    // Mostrar el panel lateral (el mismo mecanismo que ya existe al hacer click en "Nueva Membresía")
    PanelLateral.Visibility = Visibility.Visible;  // ajustar al nombre real del panel

    // Pre-cargar el ComboBox de socios con el socio recibido
    // Opción A — si el combo carga todos los socios:
    foreach (var item in cmbSocio.Items)
    {
        var combo = item as SocioComboItem;
        if (combo != null && combo.Id == socio.Id)
        {
            cmbSocio.SelectedItem = item;
            break;
        }
    }

    // Opción B — si el combo está vacío hasta que se selecciona:
    // cmbSocio.Items.Add(new SocioComboItem { Id = socio.Id, TextoCombo = socio.NombreCompleto });
    // cmbSocio.SelectedIndex = 0;

    // Limpiar el resto del formulario
    cmbActividad.SelectedIndex  = -1;
    cmbMetodoPago.SelectedIndex = -1;
    txtObservaciones.Text       = string.Empty;

    // Enfocar el ComboBox de actividad (siguiente campo a completar)
    cmbActividad.Focus();
}
```

> **Nota para Claude Code**: verificar el nombre real del panel lateral en
> `MembresiasPage.xaml` y el mecanismo de apertura que ya existe (puede ser
> una columna con Width animada, un StackPanel con Visibility, etc.).
> Replicar ese mecanismo exacto en `AbrirPanelNuevaMembresia()`.

---

### Diagrama del flujo completo

```
SociosPage
│
│  Usuario completa: nombre, apellido, DNI, teléfono, foto, etc.
│  Usuario presiona [GUARDAR]
│
├─► SocioController.Insertar()
│       └─► SocioDao.InsertarSocio()  →  sp_InsertarSocio
│               └─► Retorna: { id, numero_socio, nombre_completo }
│
├─► ¿Éxito? NO  →  NotificacionWindow.MostrarError()  →  fin
│
└─► ¿Éxito? SÍ
        │
        ├─► NotificacionWindow.MostrarConfirmacion("¿Asignar membresía ahora?")
        │
        ├─► Usuario dice NO  →  LimpiarFormulario() + CargarSocios()  →  fin
        │
        └─► Usuario dice SÍ
                │
                └─► MainWindow.NavegarAMembresiasConSocio(socioCreado)
                        │
                        └─► MembresiasPage(socio)
                                └─► Page_Loaded → AbrirPanelNuevaMembresia()
                                        ├─► Panel lateral visible
                                        ├─► cmbSocio seleccionado ← socio recién creado
                                        ├─► cmbActividad en foco (vacío, a completar)
                                        └─► Listo para guardar membresía
```

---

### Archivos a modificar

| Archivo | Tipo de cambio |
|---------|---------------|
| `SP_Socios.sql` | Extender SELECT final de `sp_InsertarSocio` |
| `Models/DAO/SocioDao.cs` | `InsertarSocio()` retorna `Socio` en lugar de `long` |
| `Controllers/SocioController.cs` | `Insertar()` retorna `(bool, string, Socio)` |
| `SistemaGimnasio/Paginas/SociosPage.xaml.cs` | Disparar navegación post-guardado |
| `SistemaGimnasio/MainWindow.xaml.cs` | Agregar método `NavegarAMembresiasConSocio()` |
| `SistemaGimnasio/Paginas/MembresiasPage.xaml.cs` | Constructor sobrecargado + `AbrirPanelNuevaMembresia()` |

---

### Estimación

| Prioridad | Problema | Estimación |
|-----------|----------|------------|
| 🟡 9 | Flujo directo Guardar Socio → Membresía | 1.5 h |

---

*Propuesta generada Mayo 2026 — Sistema OptimusCAI Gym v1.2*
