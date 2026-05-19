# SPEC — Sistema de Gestión OptimusCAI Gym
> Spec-Driven Development (SDD)  
> Versión: 1.0 — Mayo 2026  
> Propietario: Manuel Mendoza  
> Para ser leído por: el propietario del proyecto y por Claude Code al retomar trabajo

---

## 1. CONDICIONES — Reglas No Negociables

Estas reglas aplican a CADA archivo generado. Violarlas rompe el proyecto.

### Lenguaje y Runtime
- **C# 7.3 estricto** — NO usar features de C# 8+:
  - ❌ `switch expressions` (`x switch { ... }`)
  - ❌ `using` simplificado (`using var x = ...`)
  - ❌ `is` patterns nuevos (`if (x is { Nombre: "y" })`)
  - ❌ tipos de referencia anulables (`string?`)
  - ✅ Tuples de C# 7: `(bool ok, string mensaje)` — sí permitidas
- **.NET Framework** (no .NET Core / .NET 5+)
- **WPF + XAML** para toda la UI
- **SQL Server LocalDB** — archivo `DB_CAI_Optimus.mdf`

### SQL / Stored Procedures
- ❌ NO usar `CREATE OR ALTER PROCEDURE` — LocalDB no lo soporta
- ✅ Siempre usar este patrón:
  ```sql
  IF OBJECT_ID('sp_Nombre', 'P') IS NOT NULL DROP PROCEDURE sp_Nombre;
  GO
  CREATE PROCEDURE sp_Nombre ...
  GO
  ```
- Si se usa un TYPE personalizado: verificar con `sys.types` antes de crearlo
- TODA la lógica de negocio va en SPs — los DAOs solo mapean resultados

### WPF / XAML
- ❌ NO usar `LetterSpacing` — no existe en WPF
- ❌ NO usar `DropShadowEffect` dentro de un `Trigger.TargetName`
- `Tag` en XAML es siempre `string` → en code-behind usar `Convert.ToInt32(item.Tag)`
- Los `DataTrigger` de estado (activo/inactivo) van en el XAML, no en code-behind

### Arquitectura
- Cada módulo tiene exactamente **6 archivos**: SP, Entity, Dao, Controller, Page.xaml, Page.xaml.cs
- Namespace del SP helper de auditoría: `Controllers` (no crear namespace propio para evitar dependencia circular Vista ↔ Controllers)
- `Validador.cs` vive en `Controllers` por la misma razón

### Acceso a datos
- SOLO Stored Procedures — cero SQL inline en los DAOs
- Columnas opcionales que pueden no venir del SP: usar `LeerColumnaSegura()`:
  ```csharp
  private static string LeerColumnaSegura(SqlDataReader r, string columna)
  {
      for (int i = 0; i < r.FieldCount; i++)
          if (r.GetName(i).Equals(columna, StringComparison.OrdinalIgnoreCase))
              return r[columna] as string;
      return null;
  }
  ```

### Autenticación
- El Login NO hashea la contraseña — lo hace `UsuarioController.Login()` internamente
- El hash es SHA-256 y se aplica una sola vez en el Controller (doble hash = bug)

### Patrones de integración
- `ventas_items` usa columnas `precio_unitario` Y `subtotal` (ambas NOT NULL en la tabla real)
- `ventas` NO tiene columna `estado`
- DayOfWeek SQL Server: 1=Dom, 2=Lun … 7=Sab → convertir con CASE manual a convención app (1=Lun, 7=Dom)
- WhatsApp URL: `Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })`
- `Auditor.Registrar()` falla silenciosamente — NUNCA puede romper la operación principal

### Convenciones de código
- SesionManager: `SesionManager.HaySesion ? SesionManager.UsuarioId : 1` para fallback
- En módulos con USUARIO_ACTUAL_ID: usar `private long USUARIO_ACTUAL_ID => SesionManager.UsuarioId;` (property, no constante)
- Todos los módulos respetan `SesionManager` para obtener el usuario activo

---

## 2. ESPECIFICACIÓN — Qué queremos construir

### Descripción del producto
**Sistema de gestión integral para el gimnasio OptimusCAI** — una aplicación de escritorio Windows (WPF) para administrar todas las operaciones del gimnasio: socios, membresías, instructores, rutinas, caja, ventas del kiosco, comunicación por WhatsApp y trazabilidad de cambios.

### Para quién va dirigido
| Rol | Descripción |
|-----|-------------|
| **Admin** | Dueño o gerente del gimnasio. Accede a todo el sistema |
| **Empleado** | Recepcionista o instructor. Accede solo a operaciones del día a día |

### Stack técnico
```
UI:         WPF + XAML (.NET Framework)
Lenguaje:   C# 7.3
Base datos: SQL Server LocalDB → DB_CAI_Optimus.mdf
Arquitectura: 4 proyectos en una solución Visual Studio
```

### Estructura de proyectos
```
SistemaGimnacionOptimusCAI.sln
├── Entities/           → Clases de dominio (POCOs)
├── Models/
│   └── DAO/            → Acceso a datos (solo SPs)
├── Controllers/        → Lógica de negocio + validaciones
└── SistemaGimnacionOptimusCAI/  → UI WPF
    ├── App.xaml
    ├── LoginWindow.xaml + .cs
    ├── MainWindow.xaml + .cs
    ├── MiDiccionario.xaml      → Estilos compartidos
    ├── Helpers/
    │   ├── ByteToImageConverter.cs
    │   └── NotificacionWindow.xaml + .cs
    └── Paginas/                → Todas las Pages WPF
```

### Base de datos — 20 tablas
```sql
roles, usuarios, socios, actividades, membresias,
caja_movimientos, casilleros, productos, ventas,
ventas_items, turnos, instructor_asistencias,
rutinas, rutina_bloques, rutina_ejercicios,
rutina_asignaciones, whatsapp_mensajes,
auditoria, registros_acceso, huellas_dactilares
```

### Features requeridos — 15 módulos

#### Módulos de gestión de personas
| Módulo | Descripción | Roles |
|--------|-------------|-------|
| **Login** | Autenticación con DNI + contraseña (SHA-256). Guarda sesión en `SesionManager` | Todos |
| **Usuarios** | CRUD completo de admins e instructores. Soft-delete, foto, rol | Solo Admin |
| **Socios** | CRUD de miembros del gimnasio. Número de socio auto, foto, observaciones | Todos |

#### Módulos de operación diaria
| Módulo | Descripción | Roles |
|--------|-------------|-------|
| **Membresías** | Alta de membresías. Genera automáticamente movimiento en caja | Todos |
| **Asistencias** | Registro de entrada de socios por DNI o número de socio | Todos |
| **Casilleros** | Asignación de casilleros a socios. Estados: libre / ocupado / mantenimiento | Solo Admin |

#### Módulos de actividad física
| Módulo | Descripción | Roles |
|--------|-------------|-------|
| **Actividades** | Disciplinas del gimnasio (boxeo, gym, etc.) con días de semana y precio | Solo Admin |
| **Turnos** | Calendario semanal (Lun-Dom). Cada turno tiene actividad, instructor y horario | Todos |
| **Instructor Asistencias** | Fichaje de instructores (entrada/salida) por turno. Panel de turnos del día | Todos |
| **Rutinas** | Plantillas de entrenamiento: rutina → bloques → ejercicios → asignación a socios | Todos |

#### Módulos financieros
| Módulo | Descripción | Roles |
|--------|-------------|-------|
| **Caja** | Ingresos y egresos. Recibe automáticamente de membresías y ventas | Todos |
| **Productos** | Stock del kiosco. Ajuste de stock con + / − | Todos |
| **Ventas** | POS con carrito, descuento auto de stock, movimiento auto en caja | Todos |

#### Módulos de comunicación y control
| Módulo | Descripción | Roles |
|--------|-------------|-------|
| **WhatsApp** | Mensajería a socios. Genera avisos de vencimiento de membresía. Abre wa.me | Todos |
| **Auditoría** | Log de cambios: quién hizo qué y cuándo. Timeline filtrable | Solo Admin |

#### Sistema de menú (navegación)
- **Login** → **MainWindow** con sidebar y Frame
- Menú construido dinámicamente según rol (`SesionManager.EsAdmin`)
- Admin ve 14 opciones, Empleado ve 10
- Click en item → instancia la Page y navega en el Frame
- Botón cerrar sesión → `SesionManager.Cerrar()` + volver al Login

### Identidad visual
- Fondo base: `#0A0A14` / `#0D0D22` (dark profundo)
- Acento primario: `#00CFFF` (cyan)
- Acento secundario: `#A78BFA` (violeta)
- Acento naranja: `#FF6B35` (acciones, admin)
- Verde éxito: `#00E676`
- Rojo error: `#FF4444`
- Verde WhatsApp: `#25D366`
- Fuente principal: `Bahnschrift SemiBold, Segoe UI`
- Fuente monospace (horas, código): `Consolas`
- Cards con `CornerRadius="10/12"`, sin sombras, bordes sutiles `#252540`
- Línea decorativa superior en cada panel: gradiente de 3-4px

### Patrones de UI por módulo
| Patrón | Módulos que lo usan |
|--------|---------------------|
| DataGrid + panel lateral deslizable | Usuarios, Socios, Membresías, Caja, Productos |
| Grid visual / cards dinámicas | Casilleros (grilla de lockers), Turnos (calendario semanal) |
| Master/Detail con lista izquierda | Rutinas, WhatsApp, Auditoría |
| Cards de fichaje por turno | Instructor Asistencias |
| POS con carrito | Ventas |

---

## 3. CLASIFICACIÓN — Estado actual / Lo que falta

### ✅ Completado y entregado

| # | Módulo | Archivos | Estado |
|---|--------|----------|--------|
| 1 | Login + SesionManager | SP_Login.sql, SesionManager.cs, LoginWindow.xaml/.cs | ✅ |
| 2 | Usuarios | SP_Usuarios.sql, Usuario.cs, UsuarioDao.cs, UsuarioController.cs, UsuariosPage.xaml/.cs | ✅ |
| 3 | Socios | SP_Socios.sql, Socio.cs, SocioDao.cs, SocioController.cs, SociosPage.xaml/.cs | ✅ |
| 4 | Actividades | SP_Actividades.sql, Actividad.cs, ActividadDao.cs, ActividadController.cs, ActividadesPage.xaml/.cs | ✅ |
| 5 | Membresías | SP_Membresias.sql, Membresia.cs, MembresiaDao.cs, MembresiaController.cs, MembresiasPage.xaml/.cs | ✅ |
| 6 | Caja | SP_Caja.sql, CajaMovimiento.cs, CajaDao.cs, CajaController.cs, CajaPage.xaml/.cs | ✅ |
| 7 | Asistencias socios | SP_Asistencias.sql, Asistencia.cs, AsistenciaDao.cs, AsistenciaController.cs, AsistenciasPage.xaml/.cs | ✅ |
| 8 | Casilleros | SP_Casilleros.sql, Casillero.cs, CasilleroDao.cs, CasilleroController.cs, CasillerosPage.xaml/.cs | ✅ |
| 9 | Productos | SP_Productos.sql, Producto.cs, ProductoDao.cs, ProductoController.cs, ProductosPage.xaml/.cs | ✅ |
| 10 | Ventas | SP_Ventas.sql, Venta.cs+VentaItem.cs+ItemCarrito.cs, VentaDao.cs, VentaController.cs, VentasPage.xaml/.cs | ✅ |
| 11 | Turnos | SP_Turnos.sql, Turno.cs, TurnoDao.cs, TurnoController.cs, TurnosPage.xaml/.cs | ✅ |
| 12 | Instructor Asistencias | SP_InstructorAsistencias.sql, InstructorAsistencia.cs+TurnoHoy.cs, InstructorAsistenciaDao.cs, InstructorAsistenciaController.cs, InstructorAsistenciasPage.xaml/.cs | ✅ |
| 13 | Rutinas | SP_Rutinas.sql, Rutina.cs+RutinaBloque.cs+RutinaEjercicio.cs+RutinaAsignacion.cs, RutinaDao.cs, RutinaController.cs, RutinasPage.xaml/.cs | ✅ |
| 14 | WhatsApp | SP_Whatsapp.sql, WhatsappMensaje.cs, WhatsappDao.cs, WhatsappController.cs, WhatsappPage.xaml/.cs | ✅ |
| 15 | Auditoría | SP_Auditoria.sql, AuditoriaEntry.cs, AuditoriaDao.cs, AuditoriaController.cs+Auditor.cs, AuditoriaPage.xaml/.cs | ✅ |
| 16 | Menú + Navegación | App.xaml, MainWindow.xaml/.cs, LoginWindow.xaml.cs | ✅ |

### ⚠️ Pendientes / Deuda técnica identificada

| Item | Descripción | Prioridad |
|------|-------------|-----------|
| **Auditor.Registrar() en controllers** | Los 15 módulos NO tienen llamadas a `Auditor.Registrar()` en sus métodos Insertar/Modificar/Eliminar. La tabla auditoría va a quedar vacía si no se agregan. | 🔴 Alta |
| **USUARIO_ACTUAL_ID hardcodeado** | Membresías, Caja y Ventas pueden tener `private const long USUARIO_ACTUAL_ID = 1;` en lugar de `private long USUARIO_ACTUAL_ID => SesionManager.UsuarioId;` | 🔴 Alta |
| **`domicilio` en UsuarioDao.MapearUsuario** | El SP de login no devuelve `domicilio` → crash. Fix: usar `LeerColumnaSegura()` para esa columna | 🔴 Alta (ya identificado) |
| **MiDiccionario.xaml** | El estilo `BotonChipEstilo` es requerido por WhatsAppPage pero puede no existir en el diccionario. También `BotonNaranjaEstilo`, `BotonPrincipalEstilo`, `BotonCerrarEstilo`, `InputEstilo`, `PasswordEstilo` deben estar definidos | 🟡 Media |
| **SocioComboItem** | `RutinaController` y `WhatsappController` llaman a `_membreCtrl.ListarSociosParaCombo()` que devuelve `List<SocioComboItem>`. Esta clase debe existir en Entities con propiedad `TextoCombo` y `Id` | 🟡 Media |
| **NotificacionWindow.MostrarAdvertencia** | Varios módulos llaman a este método — verificar que exista en la clase `NotificacionWindow` | 🟡 Media |
| **Dashboard** | No se implementó. Pantalla inicial con KPIs de todos los módulos (socios activos, ingresos del día, vencimientos próximos) | 🟢 Baja |
| **Huellas Dactilares** | Tabla `huellas_dactilares` en la BD pero requiere SDK biométrico real (DigitalPersona / ZKTeco). No se puede implementar sin hardware | ⚪ Fuera de scope |

### 🐛 Bugs conocidos / fixes ya aplicados en sesión

| Bug | Fix aplicado |
|-----|-------------|
| `CREATE OR ALTER PROCEDURE` en LocalDB | Reemplazado por patrón DROP + CREATE en todos los SPs |
| `LetterSpacing` en XAML | Eliminado — no existe en WPF |
| `DropShadowEffect` en Trigger | Reemplazado por cambio de BorderBrush/BorderThickness |
| `ventas` sin columna `estado` | Quitado del SP y del mapeo |
| `ventas_items` — columnas `precio_unitario` y `subtotal` | Ambas obligatorias en la tabla real, incluidas en todos los INSERTs |
| DayOfWeek SQL Server (1=Dom) | Convertido con CASE manual en todos los SPs de Turnos |
| Login con doble hash | El hash SHA-256 se aplica UNA sola vez en `UsuarioController.Login()` |
| `domicilio` en MapearUsuario | Usar `LeerColumnaSegura()` — columna no siempre viene del SP de login |

---

## 4. PLAN — Arquitectura teórica para el propietario

### Cómo fluye el sistema

```
INICIO DEL PROGRAMA
       │
       ▼
  LoginWindow.xaml
  ┌─────────────────┐
  │ DNI + Contraseña│
  │ SHA-256 hash    │
  │ UsuarioController│
  │ .Login()        │
  └────────┬────────┘
           │ Login exitoso
           ▼
  SesionManager.Iniciar()
  Guarda: UsuarioId, Nombre,
  Apellido, DNI, RolNombre
           │
           ▼
  MainWindow.xaml
  ┌──────────────────────────────────────────┐
  │  SIDEBAR (240px)    │  FRAME CONTENIDO   │
  │  ┌──────────────┐   │                    │
  │  │ Logo         │   │  [Página activa]   │
  │  │ Card usuario │   │                    │
  │  │ (iniciales + │   │  Cada click en el  │
  │  │  nombre +    │   │  sidebar instancia │
  │  │  badge rol)  │   │  una nueva Page y  │
  │  │              │   │  navega el Frame   │
  │  │ ScrollViewer │   │                    │
  │  │ con botones  │   │                    │
  │  │ de menú      │   │                    │
  │  │              │   │                    │
  │  │ [Cerrar      │   │                    │
  │  │  sesión]     │   │                    │
  │  └──────────────┘   │                    │
  └──────────────────────────────────────────┘
```

### Cómo se construye cada módulo (patrón uniforme)

```
SP_Modulo.sql          → Stored procedures en SQL Server
      │
      ▼
ModeloEntidad.cs       → POCO en Entities/ (sin lógica)
      │
      ▼
ModeloDao.cs           → Lee/escribe la BD llamando SPs
      │                  Solo mapea SqlDataReader → Entity
      ▼
ModeloController.cs    → Lógica de negocio + validaciones
      │                  Llama al DAO, nunca a la BD directo
      ▼
ModuloPage.xaml        → UI (XAML puro, sin lógica)
ModuloPage.xaml.cs     → Code-behind: eventos, llamadas al Controller
```

### Cómo funcionan los roles

```
SesionManager (static singleton)
├── UsuarioId    → long (ID del usuario en BD)
├── NombreCompleto → "Juan Perez"
├── RolNombre    → "admin" | "empleado"
├── EsAdmin      → bool (true si RolNombre == "admin")
├── HaySesion    → bool (true si UsuarioId > 0)
├── Iniciar()    → llamado desde LoginWindow al loguear
└── Cerrar()     → llamado desde MainWindow al cerrar sesión

MainWindow construye el menú así:
  - Por cada item en la lista predefinida:
    - Si item.SoloAdmin == true && !SesionManager.EsAdmin → SALTAR
    - Si no → crear botón y agregarlo al sidebar
```

### Cómo se integran los módulos entre sí

```
MEMBRESIAS ──────► genera ──────► CAJA (movimiento ingreso)
VENTAS     ──────► genera ──────► CAJA (movimiento ingreso)
VENTAS     ──────► descuenta ───► PRODUCTOS (stock)
TURNOS     ──────► referencia ──► INSTRUCTOR_ASISTENCIAS
TURNOS     ──────► usa ────────► ACTIVIDADES
RUTINAS    ──────► asigna a ───► SOCIOS
WHATSAPP   ──────► avisa sobre ► MEMBRESIAS (vencimientos)
CUALQUIER MODULO ─► registra en ► AUDITORIA (via Auditor.Registrar)
```

### Cómo funciona la Auditoría

```csharp
// En CUALQUIER Controller, después de una operación exitosa:
Auditor.Registrar("crear", "socio", nuevoId, new Dictionary<string, object> {
    { "nombre", "Juan" },
    { "dni", "12345678" },
    { "rol", "empleado" }
});

// Internamente:
// 1. Lee SesionManager.UsuarioId como actor
// 2. Serializa el dict a JSON sin dependencias externas
// 3. Llama a sp_RegistrarAuditoria
// 4. Si falla → ignora (no rompe la operación principal)
```

---

## 5. TAREAS PENDIENTES — Para Claude Code

> **INSTRUCCIÓN PARA CLAUDE CODE**: Leer este archivo completo antes de escribir cualquier línea de código. Las secciones 1 y 3 son las más críticas. Si ves algo que contradice las reglas de la Sección 1, aplicar la regla siempre.

### TAREA 1 — Crítica: Fix `USUARIO_ACTUAL_ID` en 3 módulos
**Archivos a modificar**: `MembresiasPage.xaml.cs`, `CajaPage.xaml.cs`, `VentasPage.xaml.cs`

Buscar:
```csharp
private const long USUARIO_ACTUAL_ID = 1;
```
Reemplazar por:
```csharp
private long USUARIO_ACTUAL_ID => SesionManager.UsuarioId;
```

---

### TAREA 2 — Crítica: Fix columna `domicilio` en UsuarioDao
**Archivo a modificar**: `Models/DAO/UsuarioDao.cs`

En el método `MapearUsuario()`, cambiar la línea de `Domicilio` a:
```csharp
Domicilio = LeerColumnaSegura(r, "domicilio"),
```
Y agregar el método helper si no existe:
```csharp
private static string LeerColumnaSegura(SqlDataReader r, string columna)
{
    for (int i = 0; i < r.FieldCount; i++)
        if (r.GetName(i).Equals(columna, StringComparison.OrdinalIgnoreCase))
            return r[columna] as string;
    return null;
}
```

---

### TAREA 3 — Alta: Agregar llamadas a `Auditor.Registrar()` en todos los Controllers

**Archivos a modificar**: los 15 controllers existentes.

**Patrón a aplicar en cada método exitoso**:

```csharp
// En UsuarioController.Insertar():
if (id > 0)
    Auditor.Registrar("crear", "usuario", id, new Dictionary<string, object> {
        { "nombre", nombre }, { "apellido", apellido }, { "dni", dni }, { "rol_id", rolId }
    });

// En SocioController.Modificar():
if (ok)
    Auditor.Registrar("editar", "socio", id, new Dictionary<string, object> {
        { "nombre", nombre }, { "apellido", apellido }
    });

// En VentaController.Insertar():
if (id > 0)
    Auditor.Registrar("crear", "venta", id, new Dictionary<string, object> {
        { "total", total }, { "items", cantidadItems }
    });
```

**Tabla de acciones por módulo**:

| Controller | Método | Acción | Entidad |
|-----------|--------|--------|---------|
| UsuarioController | Insertar | "crear" | "usuario" |
| UsuarioController | Modificar | "editar" | "usuario" |
| UsuarioController | CambiarEstado | "activar" / "desactivar" | "usuario" |
| SocioController | Insertar | "crear" | "socio" |
| SocioController | Modificar | "editar" | "socio" |
| SocioController | Eliminar | "eliminar" | "socio" |
| ActividadController | Insertar | "crear" | "actividad" |
| ActividadController | Modificar | "editar" | "actividad" |
| MembresiaController | Insertar | "crear" | "membresia" |
| MembresiaController | Anular | "anular" | "membresia" |
| CajaController | InsertarMovimiento | "crear" | "caja" |
| ProductoController | Insertar | "crear" | "producto" |
| ProductoController | Modificar | "editar" | "producto" |
| ProductoController | AjustarStock | "editar" | "producto" |
| VentaController | Insertar | "crear" | "venta" |
| VentaController | Anular | "anular" | "venta" |
| CasilleroController | Asignar | "editar" | "casillero" |
| CasilleroController | Liberar | "editar" | "casillero" |
| TurnoController | Insertar | "crear" | "turno" |
| TurnoController | Modificar | "editar" | "turno" |
| TurnoController | Eliminar | "eliminar" | "turno" |
| InstructorAsistenciaController | RegistrarEntrada | "crear" | "asistencia" |
| InstructorAsistenciaController | RegistrarSalida | "editar" | "asistencia" |
| RutinaController | InsertarRutina | "crear" | "rutina" |
| RutinaController | ModificarRutina | "editar" | "rutina" |
| RutinaController | EliminarRutina | "eliminar" | "rutina" |
| RutinaController | AsignarRutina | "crear" | "rutina" |
| WhatsappController | MarcarComoEnviado | "editar" | "whatsapp" |
| UsuarioController | Login (exitoso) | "login" | "sesion" |

---

### TAREA 4 — Media: Verificar `MiDiccionario.xaml` tiene todos los estilos

Estilos requeridos por las páginas:

```xml
<!-- Estilos que DEBEN existir en MiDiccionario.xaml -->
BotonNaranjaEstilo      → Button naranja (#FF6B35), usado en headers de todas las páginas
BotonPrincipalEstilo    → Button cyan, usado en formularios (Guardar)
BotonCerrarEstilo       → Button gris/neutro, usado en formularios (Cancelar)
InputEstilo             → TextBox oscuro (#16162A), borde sutil
PasswordEstilo          → PasswordBox oscuro, igual a InputEstilo
BotonChipEstilo         → Button pequeño para filtros (WhatsApp, Auditoría)
```

Si alguno no existe, crearlo siguiendo este patrón base:
```xml
<Style x:Key="BotonNaranjaEstilo" TargetType="Button">
    <Setter Property="Background">
        <Setter.Value>
            <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                <GradientStop Color="#FF6B35" Offset="0"/>
                <GradientStop Color="#FF3D1F" Offset="1"/>
            </LinearGradientBrush>
        </Setter.Value>
    </Setter>
    <Setter Property="Foreground" Value="#FFFFFF"/>
    <Setter Property="FontWeight" Value="Bold"/>
    <Setter Property="FontSize" Value="12"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border Background="{TemplateBinding Background}" CornerRadius="10">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

---

### TAREA 5 — Media: Verificar que `SocioComboItem` existe

`RutinaController` y `WhatsappController` usan `SocioComboItem`. Crear si no existe:

**Archivo**: `Entities/SocioComboItem.cs`
```csharp
// Entities/SocioComboItem.cs — C# 7.3
namespace Entities
{
    public class SocioComboItem
    {
        public long   Id         { get; set; }
        public string TextoCombo { get; set; }  // "Juan Perez — #0042"

        public static SocioComboItem Desde(long id, string nombre, string apellido, int? nroSocio)
        {
            string nro = nroSocio.HasValue ? " — #" + nroSocio.Value.ToString("D4") : "";
            return new SocioComboItem
            {
                Id = id,
                TextoCombo = nombre + " " + apellido + nro
            };
        }
    }
}
```

---

### TAREA 6 — Media: Verificar `NotificacionWindow` tiene todos los métodos

Los módulos llaman a estos métodos estáticos:
```csharp
NotificacionWindow.MostrarExito(string mensaje)
NotificacionWindow.MostrarError(string mensaje)
NotificacionWindow.MostrarAdvertencia(string mensaje)    // ← puede faltar
NotificacionWindow.MostrarConfirmacion(string mensaje, string titulo) → bool
```

Si `MostrarAdvertencia` no existe, agregar:
```csharp
public static void MostrarAdvertencia(string mensaje)
{
    // Mismo patrón que MostrarError pero con color amarillo/naranja
    MessageBox.Show(mensaje, "Atención", MessageBoxButton.OK, MessageBoxImage.Warning);
}
```

---

### TAREA 7 — Baja: Dashboard (pantalla de bienvenida)

Crear `DashboardPage.xaml` y `.cs` con:
- KPI: Socios activos hoy / Ingresos del día / Membresías venciendo esta semana / Ventas del día
- Hacer que sea la primera página que se abre al entrar al sistema (index 0 del menú)
- Los datos vienen de SPs específicos de cada módulo

**SPs necesarios** (ya existen parcialmente):
- `sp_EstadisticasSocios` → socios activos
- `sp_EstadisticasCaja` → ingresos del día
- `sp_GenerarAvisosVencimiento` con `@DiasAntes = 7` → count de próximos vencimientos
- `sp_EstadisticasVentas` → ventas del día

---

### TAREA 8 — Opcional: Módulo Huellas Dactilares (placeholder)

La tabla `huellas_dactilares` existe en la BD. El módulo se puede crear como placeholder con mensaje "Requiere hardware biométrico" hasta tener el SDK (DigitalPersona o ZKTeco).

---

## Notas para retomar el trabajo

### Al abrir el proyecto por primera vez
1. Verificar que `DB_CAI_Optimus.mdf` está en la ruta correcta (App.config / connection string)
2. Ejecutar TODOS los scripts SQL en orden antes de correr la app
3. Verificar que existe al menos un usuario admin en la tabla `usuarios`

### Orden de ejecución de scripts SQL
```
1. Crear las tablas (script de creación de BD — ya existente)
2. SP_Login.sql
3. SP_Usuarios.sql
4. SP_Socios.sql
5. SP_Actividades.sql
6. SP_Membresias.sql
7. SP_Caja.sql
8. SP_Asistencias.sql
9. SP_Casilleros.sql
10. SP_Productos.sql
11. SP_Ventas.sql
12. SP_Turnos.sql
13. SP_InstructorAsistencias.sql
14. SP_Rutinas.sql
15. SP_Whatsapp.sql
16. SP_Auditoria.sql
```

### Usuario de prueba (insertar manualmente si no existe)
```sql
-- Contraseña: "admin123" hasheada con SHA-256
-- Hash: 240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a
INSERT INTO usuarios (rol_id, nombre, apellido, dni, password_hash, activo)
VALUES (1, 'Super', 'Administrador', '00000001',
        '240be518fabd2724ddb6f04eeb1da5967448d7e831c08c8fa822809f74c720a', 1);
```

---

*Documento generado en sesión de desarrollo Mayo 2026*  
*Sistema: OptimusCAI Gym — v1.0*
