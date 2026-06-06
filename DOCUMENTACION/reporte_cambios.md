# 📋 Reporte de Correcciones — Sistema OptimusCAI Gym

**Fecha:** 20/05/2026  
**Módulos afectados:** Socios, Membresías  
**Archivos modificados:** 3 (`.sql`, `.cs`, `.cs`)

---

## 🔧 1. Error al crear socio nuevo (`spSocios.sql`)

### Problema
Al crear un socio, el sistema mostraba:  
`"Error al insertar. numero_socio"`

### Causa raíz
El SP `sp_InsertarSocio` en la base de datos estaba desactualizado:
- Solo retornaba `SCOPE_IDENTITY() AS id`
- El DAO en C# esperaba `id` **y** `numero_socio`
- Al leer `reader["numero_socio"]`, explotaba por columna inexistente

Además, el cálculo del siguiente número no excluía socios eliminados (soft-delete), lo que podía causar duplicados en la constraint UNIQUE `uq_socios_numero`.

### Solución aplicada
**Archivo:** `DataBase/spSocios.sql`

| Cambio | Detalle |
|--------|---------|
| ✅ Verificación de columna `numero_socio` | Se agrega al inicio del script por si no existe |
| ✅ Fix en `sp_InsertarSocio` | Ahora retorna `id`, `numero_socio` y `nombre_completo` |
| ✅ Fix en `sp_ObtenerSiguienteNumeroSocio` | Agrega `WHERE eliminado_en IS NULL` para no contar socios borrados |
| ✅ Fix en cálculo de MAX dentro de `sp_InsertarSocio` | Igualmente filtra por `eliminado_en IS NULL` |

### Resultado
- Los socios se crean correctamente
- El número de socio se calcula sin contar registros eliminados
- La redirección a membresías funciona sin errores

---

## 🔧 2. Fechas no visibles al crear membresía desde socio nuevo (`MembresiasPage.xaml.cs`)

### Problema
Al crear un socio nuevo y redirigir automáticamente a la sección de membresías, los campos de fecha (inicio y vencimiento) aparecían vacíos.

### Causa raíz
El método `AbrirPanelNuevaMembresia(Socio socio)` no inicializaba los DatePicker `dpInicio` y `dpVencimiento`, mientras que `AbrirFormulario()` sí lo hacía.

### Solución aplicada
**Archivo:** `Paginas/MembresiasPage.xaml.cs` (línea ~135)

Se agregaron 4 líneas para setear las fechas por defecto:
```cs
dpInicio.SelectedDate = DateTime.Today;
dpInicio.IsEnabled = false;
dpVencimiento.SelectedDate = DateTime.Today.AddDays(31);
dpVencimiento.IsEnabled = false;
```

### Resultado
- Las fechas se muestran correctamente al redirigir desde socios
- Inicio = fecha de hoy
- Vencimiento = hoy + 31 días (no editables, como debe ser)

---

## 🔧 3. Validaciones de cambio de plan en membresías (`spMembresias.sql` + `MembresiasPage.xaml.cs`)

### Problema
Se necesitaba implementar reglas de negocio para evitar:
1. Crear una membresía con la **misma actividad exacta** si el socio ya tiene una activa
2. Crear una membresía en la **misma categoría** si ya tiene una activa
3. Permitir **solo upgrades** (nivel superior) al cambiar de plan, no downgrades
4. Cambiar a **otra categoría** al editar una membresía

### Reglas de negocio implementadas

#### A) En `sp_InsertarMembresia` (Base de datos)

| Validación | Descripción | Mensaje de error |
|------------|-------------|------------------|
| **1. Misma actividad** | No permite crear si el socio ya tiene membresía activa con el **mismo `actividad_id`** | `"El socio ya tiene una membresía activa con la actividad 'X'. No se puede crear una nueva membresía con la misma actividad."` |
| **2. Misma categoría** | No permite crear si el socio ya tiene membresía activa en la **misma categoría** (aunque sea otra actividad) | `"El socio ya tiene una membresía activa en la categoría 'X'. No se puede crear otra membresía en la misma categoría."` |

**Comportamiento adicional:** Si las validaciones pasan, el SP **cancela automáticamente** las membresías activas anteriores del mismo socio + actividad antes de insertar la nueva.

#### B) En `sp_ModificarMembresia` (Base de datos)

| Validación | Descripción | Mensaje de error |
|------------|-------------|------------------|
| **1. Misma categoría** | Al editar, si se cambia la actividad, la nueva debe ser de la **misma categoría** | `"No se puede cambiar a otra categoría. El cambio de plan solo está permitido dentro de la misma categoría."` |
| **2. Solo upgrade** | El nuevo nivel debe ser **mayor** al actual (no se permite downgrade) | `"Solo se permite cambiar a un plan superior (upgrade). El downgrade no está permitido."` |

#### C) En `MembresiasPage.xaml.cs` (UI - `btnGuardar_Click`)

Las mismas validaciones se replican en el code-behind (líneas 479-499) para mostrar mensajes amigables **antes** de llamar al SP:

```cs
// Validación de cambio de plan (solo si se cambia la actividad)
if (actividad != null && actividad.Id != _actividadActualId)
{
    // Validar misma categoría
    if (_actividadActualCategoria != actividad.Categoria)
    {
        NotificacionWindow.MostrarError("No se puede cambiar a otra categoría...");
        return;
    }

    // Validar solo upgrade (nivel mayor)
    if (actividad.Nivel <= _actividadActualNivel)
    {
        NotificacionWindow.MostrarError("Solo se permite cambiar a un plan superior...");
        return;
    }
}
```

### Resultado
| Escenario | Comportamiento |
|-----------|---------------|
| Socio con membresía activa en "Musculación Básico" | ❌ No permite crear otra en "Musculación Básico" (misma actividad) |
| Socio con membresía activa en "Musculación Básico" | ❌ No permite crear en "Musculación Intermedio" (misma categoría) |
| Socio con membresía activa en "Musculación" | ✅ Permite crear en "CrossTraining" (categoría diferente) |
| Editar membresía: "Básico" (nivel 1) → "Intermedio" (nivel 2) | ✅ Permitido (upgrade) |
| Editar membresía: "Intermedio" (nivel 2) → "Básico" (nivel 1) | ❌ No permitido (downgrade) |
| Editar membresía: "Musculación" → "CrossTraining" | ❌ No permitido (categoría diferente) |

---

## 📌 Resumen de archivos modificados

| Archivo | Líneas afectadas | Tipo de cambio |
|---------|------------------|----------------|
| `DataBase/spSocios.sql` | 1-18, 135, 171 | Fix SPs + verificación de columnas |
| `Paginas/MembresiasPage.xaml.cs` | ~135, 479-499 | Inicialización de fechas + validaciones cambio de plan |
| `DataBase/spMembresias.sql` | (existente) | SPs ya incluyen validaciones de actividad/categoría/nivel |

---

## ✅ Validación realizada

1. **SPs ejecutados correctamente** en `(LocalDB)\MSSQLLocalDB` → Base `DB_CAI_Optimus.mdf`
2. **Insert de socio probado** → OK (retorna `id` y `numero_socio`)
3. **Fechas en membresías** → Se muestran correctamente al redirigir desde socios
4. **Validaciones de cambio de plan** → Mensajes de error correctos al intentar violar reglas

---

## ⚠️ Notas para el equipo

### Base de datos
**No borrar registros directamente desde la tabla `socios`** en Server Explorer. Siempre usar el botón **Eliminar** del sistema, que hace soft-delete (`eliminado_en = GETDATE()`). Borrar físicamente rompe referencias en tablas relacionadas (membresías, asistencias, pagos).

### Cambio de plan en membresías
| Regla | Comportamiento |
|-------|---------------|
| Misma actividad | ❌ No permitido (un socio no puede tener 2 membresías activas con la misma actividad) |
| Misma categoría | ❌ No permitido (un socio no puede tener 2 membresías activas en la misma categoría) |
| Upgrade (nivel mayor) | ✅ Permitido dentro de la misma categoría |
| Downgrade (nivel menor) | ❌ No permitido |
| Cambio de categoría | ❌ No permitido |

---

## 🔧 4. Paginación Infinita (Infinite Scroll) en Socios (`31/05/2026`)

### Problema
`sp_ListarSociosConMembresias` devolvía **todos** los registros de una vez. Con muchos socios, la carga era lenta y el DataGrid renderizaba miles de filas.

### Solución aplicada
Se implementó carga paginada de **8 en 8**, activada al llegar al final del scroll.

### Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `DataBase\sp_ListarSociosConMembresias.sql` | SP modificado: 2 result sets (total + datos paginados), parámetros `@Pagina` y `@TamPagina` |
| `Entities\ResultadoPaginado.cs` | **NUEVO** — Clase genérica `ResultadoPaginado<T>` |
| `Entities\Entities.csproj` | Agregada referencia al nuevo `.cs` |
| `Models\DAO\SocioDao.cs` | `ListarSociosConMembresias` ahora lee 2 result sets, devuelve `ResultadoPaginado<SocioConMembresia>` |
| `Controllers\SocioController.cs` | Firma actualizada con `pagina` y `tamPagina` |
| `Paginas\SociosPage.xaml` | Agregado `panelCargando` con fila extra en el Grid |
| `Paginas\SociosPage.xaml.cs` | Infinite scroll: `CargarSociosPagina`, `OnScrollChanged`, `ObtenerScrollViewer`, contadores con totales reales |

### SP a ejecutar

**1 solo archivo:** `DataBase\sp_ListarSociosConMembresias.sql`

```
sqlcmd -S "(LocalDB)\MSSQLLocalDB" -d "DB_CAI_Optimus" -i "DataBase\sp_ListarSociosConMembresias.sql"
```

O ejecutarlo directamente desde SQL Server Object Explorer sobre `DB_CAI_Optimus.mdf`.

### Comportamiento
| Escenario | Resultado |
|-----------|-----------|
| Al abrir sección | Carga los primeros **8** socios |
| Scroll hasta el último | Muestra "Cargando más socios..." (1.2s de delay) y agrega los siguientes 8 |
| Sin más socios | No hace más requests |
| Filtros / búsqueda | Resetea a página 1 y recarga |

---

## 🔧 5. Dashboard — Asistencia Unificada (Socio + Instructor) (`01/06/2026`)

### Problema
El Dashboard (tablero principal) mostraba 4 cards KPI estáticas y un calendario grande sin funcionalidad práctica. El usuario necesitaba poder registrar asistencias directamente desde el tablero.

### Solución aplicada
Se rediseño completamente el Dashboard para funcionar como punto de entrada unificado de asistencia:

1. **Se eliminaron las 4 cards KPI** (Socios Activos, Membresias Activas, Ingresos del Mes, Asistencias Hoy)
2. **Se reemplazo por un input de DNI** con botón "VALIDAR"
3. **Se elimino el calendario grande** del panel izquierdo
4. **Se agrego un panel de resultado** que muestra la asistencia procesada (socio o instructor) con auto-ocultamiento a los 5 segundos

### Flujo de funcionamiento

| Paso | Descripcion |
|------|-------------|
| 1 | El admin ingresa un DNI en el TextBox y presiona Enter o click en VALIDAR |
| 2 | El sistema busca el DNI en `socios` y `usuarios` (rol instructor) |
| 3a | Si es **socio** → valida acceso con `sp_ValidarAccesoPorDni` (membresia, dia permitido, limites) |
| 3b | Si es **instructor** → auto-toggle entrada/salida con `sp_FicharInstructorDashboard` |
| 4 | El resultado se muestra en el panel principal con foto, nombre, y detalles |
| 5 | A los 5 segundos el resultado se oculta con fade-out y el foco vuelve al TextBox |

### Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `DataBase\spAsistencias.sql` | **NUEVO SP** `sp_BuscarPersonaPorDni` — busca DNI en socios y usuarios |
| `DataBase\spInstructorAsistencias.sql` | **NUEVO SP** `sp_FicharInstructorDashboard` — registra entrada/salida automatica para instructores |
| `Models\DAO\AsistenciaDao.cs` | Nuevo metodo `BuscarPersonaPorDni()` |
| `Models\DAO\InstructorAsistenciaDao.cs` | Nuevo metodo `FicharInstructorDashboard()` |
| `Controllers\AsistenciaController.cs` | Nuevo metodo `BuscarPersonaPorDni()` con validacion de formato |
| `Controllers\InstructorAsistenciaController.cs` | Nuevo metodo `FicharInstructorDashboard()` con `Auditor.Registrar()` |
| `Entities\InstructorAsistencia.cs` | Agregados campos `Foto`, `Operacion`, `Mensaje` a `FichajeResultado` |
| `Paginas\DashboardPage.xaml` | Rediseño completo: input DNI + panel resultado |
| `Paginas\DashboardPage.xaml.cs` | Logica nueva: deteccion socio/instructor, procesamiento, auto-ocultamiento |

### SPs a ejecutar

**Ejecutar en este orden:**

```
1. DataBase\spAsistencias.sql
   → Agrega sp_BuscarPersonaPorDni

2. DataBase\spInstructorAsistencias.sql
   → Agrega sp_FicharInstructorDashboard
```

O ejecutar directamente desde SQL Server Object Explorer sobre `DB_CAI_Optimus.mdf`.

```
sqlcmd -S "(LocalDB)\MSSQLLocalDB" -d "DB_CAI_Optimus" -i "DataBase\spAsistencias.sql"
sqlcmd -S "(LocalDB)\MSSQLLocalDB" -d "DB_CAI_Optimus" -i "DataBase\spInstructorAsistencias.sql"
```

### Comportamiento

| Escenario | Resultado |
|-----------|-----------|
| DNI de socio con membresia activa | ✅ Acceso permitido, muestra foto, plan, vencimiento, asistencias restantes |
| DNI de socio con membresia vencida | ❌ Acceso denegado, muestra motivo |
| DNI de socio sin membresia activa | ❌ Acceso denegado |
| DNI de instructor sin entrada hoy | ✅ Registra entrada, muestra hora |
| DNI de instructor con entrada abierta | ✅ Registra salida, muestra hora + horas trabajadas |
| DNI inexistente | ❌ Muestra error "No se encontro ninguna persona" |
| DNI vacio | No procesa |

### Notas
- El historial de asistencias de socios se mantiene en la seccion **Asistencias**
- El historial de fichajes de instructores se mantiene en **Asistencias de Instructores**
- El dashboard solo muestra el resultado inmediato, no historial
- El panel derecho (Vencimientos + Accesos Rapidos) se mantiene sin cambios

---

## 🔧 6. Solo guardar registros de acceso cuando validacion es AFIRMATIVA (`01/06/2026`)

### Problema
El SP `sp_ValidarAccesoPorDni` insertaba un registro en `registros_acceso` para CADA intento de validacion, tanto permitidos como denegados. Esto inflaba la tabla con registros de denegaciones que no aportan valor y contaminan las estadisticas.

### Solucion aplicada
Se eliminaron los 7 INSERT de acceso denegado. Ahora solo se inserta un registro cuando el acceso es **permitido**.

### Cambios en `sp_ValidarAccesoPorDni`

| Caso | Lineas originales | Cambio |
|------|-------------------|--------|
| Socio inactivo | 78-79 | ❌ Eliminado INSERT |
| Membresia no valida | 130-131 | ❌ Eliminado INSERT |
| Domingo | 155-156 | ❌ Eliminado INSERT |
| Dia no permitido | 178-179 | ❌ Eliminado INSERT |
| Ya asistio hoy | 201-202 | ❌ Eliminado INSERT |
| Limite total agotado | 220-221 | ❌ Eliminado INSERT |
| Limite semanal agotado | 244-245 | ❌ Eliminado INSERT |
| **Acceso permitido** | 259-260 | ✅ Se mantiene |

### Ajuste en SELECTs de retorno
Todos los SELECT de casos denegados cambiaron `CAST(SCOPE_IDENTITY() AS BIGINT) AS registro_id` por `CAST(NULL AS BIGINT) AS registro_id` ya que no hay INSERT que generar un ID.

### SP a ejecutar

```
DataBase\spAsistencias.sql
```

---

## 🔧 7. Fix TimeSpan conversion en fichaje instructor Dashboard (`01/06/2026`)

### Problema
Al ingresar el DNI de un instructor en el Dashboard, el sistema crasheaba con error de conversion: *"No se puede convertir un objeto TimeSpan"*.

### Causa raiz
En `InstructorAsistenciaDao.cs`, el metodo `FicharInstructorDashboard` intentaba convertir la columna `hora` (tipo TIME en SQL Server) usando `Convert.ToDateTime()`. SQL Server mapea TIME a `TimeSpan` en C#, no a `DateTime`.

### Solucion aplicada
**Archivo:** `Models\DAO\InstructorAsistenciaDao.cs`

```cs
// ANTES (incorrecto):
var hora = Convert.ToDateTime(r["hora"]);
res.HoraEntrada = hora.TimeOfDay;

// AHORA (correcto):
var hora = (TimeSpan)r["hora"];
res.HoraEntrada = hora;
```

### Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `Models\DAO\InstructorAsistenciaDao.cs` | Fix conversion de TIME → TimeSpan en `FicharInstructorDashboard()` |

### Nota sobre registros de instructor cerrados
Si todos los registros de `instructor_asistencias` figuran como cerrados (con `hora_salida`), esto es comportamiento correcto de los datos existentes. El nuevo SP `sp_FicharInstructorDashboard` funciona correctamente:
- Si no hay entrada abierta hoy → registra entrada
- Si hay entrada abierta hoy → registra salida

---

## 🔧 8. Fix UriFormatException y segundo intento sin resultado en Dashboard (`01/06/2026`)

### Problema 1: UriFormatException al mostrar foto de instructor
Al ingresar el DNI de un instructor, el sistema crasheaba con:
```
System.UriFormatException: URI no válido: la cadena URI es demasiado larga.
```

### Causa raiz
`DashboardPage.xaml.cs` intentaba cargar la foto usando `new Uri("data:image/png;base64," + Convert.ToBase64String(foto))`. Para fotos grandes (>50KB), la cadena base64 supera el limite de longitud de URI de WPF.

### Solucion
Se reemplazo el enfoque de URI por `MemoryStream` + `BitmapImage`, igual que hace `ResultadoAccesoWindow`:

```cs
// ANTES (crashea con fotos grandes):
imgFotoResultado.ImageSource = new BitmapImage(
    new Uri("data:image/png;base64," + Convert.ToBase64String(foto)));

// AHORA (funciona con cualquier tamaño):
imgFotoResultado.ImageSource = BytesAImagen(foto);
```

Se agrego el metodo helper `BytesAImagen(byte[] bytes)` que usa `MemoryStream` + `BitmapImage` con `Freeze()` para thread-safety.

### Problema 2: Segundo intento de validacion no muestra resultado
Despues de un intento fallido, al ingresar el mismo DNI nuevamente no se mostraba ningun resultado.

### Causa raiz
El fade-out de `OcultarResultado()` dejaba `panelResultado.Opacity = 0` y el siguiente intento no restablecia la opacidad a 1 antes de mostrar el nuevo resultado.

### Solucion
Se agrego `panelResultado.Opacity = 1;` al inicio de `MostrarResultadoSocio()`, `MostrarResultadoInstructor()` y `MostrarError()`.

### Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `Paginas\DashboardPage.xaml.cs` | Metodo `BytesAImagen()`, `Opacity = 1` en 3 metodos de resultado |

---

## 🔧 9. Acciones de membresia desde tabla de Socios (`01/06/2026`)

### Problema
Desde la seccion Socios no habia forma de editar ni cancelar la membresia de un socio directamente. Los botones de accion solo permitian editar datos personales del socio o cambiar su estado activo/inactivo.

### Solucion aplicada
Se reemplazaron los botones de accion de la tabla de Socios por dos botones especificos para gestion de membresias:
1. **Modificar Membresia** — abre `MembresiaWindow` como popup con los datos precargados
2. **Cancelar Membresia** — cancela la membresia con confirmacion

### Cambios realizados

#### A. `SociosPage.xaml` — Botones en columna ACCIONES

Se reemplazaron los 3 botones anteriores (Editar socio, Editar membresia, Toggle estado) por 2 nuevos:

```xml
<!-- Modificar Membresia -->
<Button Style="{StaticResource ButtonStyleEditar}"
        Margin="0,0,4,0"
        ToolTip="Modificar membresía"
        Click="btnEditarMembresia_Click">
    <fa:ImageAwesome Icon="Calendar"
                     Foreground="{StaticResource GreenMain}"
                     Height="14"/>
</Button>

<!-- Cancelar Membresia -->
<Button Style="{StaticResource ButtonStyleCancelar}"
        Click="btnCancelarMembresia_Click"
        ToolTip="Cancelar membresía">
    <fa:ImageAwesome Icon="Times"
                     Foreground="#FF5555"
                     Height="14"/>
</Button>
```

#### B. `SociosPage.xaml.cs` — Handlers actualizados

**`btnEditarMembresia_Click`**: Abre `MembresiaWindow` como popup emergente con los datos de la membresia del socio precargados via `win.Configurar(socio.MembresiaId)`.

**`btnCancelarMembresia_Click`**: Cancela la membresia usando `_membresiaController.Cancelar()` con confirmacion previa.

Se agrego el campo `private readonly MembresiaController _membresiaController`.

### Flujo de uso

| Accion | Comportamiento |
|--------|---------------|
| Click en 📅 Modificar | Abre popup `MembresiaWindow` con datos precargados. Permite cambiar actividad, fechas, monto, instructor. Soporta upgrades. |
| Click en ✖ Cancelar | Pide confirmacion. Si acepta, cambia el estado de la membresia a "cancelada". |
| Sin membresia | Muestra advertencia "Este socio no tiene una membresia asignada." |
| Membresia ya cancelada | Muestra advertencia "Esta membresia ya esta cancelada." |

### Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `Paginas\SociosPage.xaml` | Botones Modificar/Cancelar membresia, columna ACCIONES width=100 |
| `Paginas\SociosPage.xaml.cs` | `btnEditarMembresia_Click`, `btnCancelarMembresia_Click`, campo `_membresiaController` |

### Notas
- Se eliminaron los botones "Editar socio" y "Toggle estado" de la tabla
- `MembresiaWindow` ya existia y soporta edicion via `Configurar(long membresiaId)`
- El metodo `Cancelar` del `MembresiaController` ya existia

---

## 🔧 10. Fix StaticResource "Bg2" not found en MembresiaWindow (`01/06/2026`)

### Problema
Al hacer click en "Modificar Membresia" desde la tabla de Socios, el sistema crasheaba con:
```
System.Windows.Markup.XamlParseException: No se encontro el recurso con el nombre 'Bg2'.
```

### Causa raiz
`MembresiaWindow.xaml` es una `Window` independiente que usa recursos globales (`Bg1`, `Bg2`, `Border1`, `ComboBoxEstilo`, `InputEstilo`, etc.) definidos en `MiDiccionario.xaml`. A diferencia de otras ventanas como `EditarSocioWindow.xaml`, no tenia el diccionario mergado en sus recursos, por lo que los `StaticResource` no se resolvia.

### Solucion aplicada
**Archivo:** `Ventanas\MembresiaWindow.xaml`

Se agrego el bloque de recursos al inicio del Window, antes del Border principal:

```xml
<Window.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceDictionary Source="/MiDiccionario.xaml"/>
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Window.Resources>
```

Esto es consistente con como otras ventanas del proyecto (`EditarSocioWindow.xaml`, etc.) resuelven los recursos globales.

### Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `Ventanas\MembresiaWindow.xaml` | Agregado `Window.Resources` con merge de `MiDiccionario.xaml` |

### Nota
`MembresiaPage.xaml` eventualmente se eliminara porque toda la gestion de membresias se hara desde la seccion de Socios. `MembresiaWindow.xaml` (el popup) seguira siendo necesario y funcional.

---

## 🔧 11. Mejoras en Socios y simplificacion de Asistencias (`01/06/2026`)

### Cambios realizados

| Cambio | Descripcion |
|--------|-------------|
| Avatar con fondo verde | El circulo del avatar del socio ahora usa `GreenMid` de `MiDiccionario.xaml` |
| Columna NOMBRE COMPLETO | Se muestra nombre completo y email debajo, con mejor jerarquia visual (negrita + tamanos diferenciados) |
| Asistencias simplificada | Se elimino el panel de "Registrar Ingreso" (DNI, validacion, modo huella). Solo queda la lista de "Accesos del dia" con filtros y auto-refresh |

### Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `Paginas\SociosPage.xaml` | Avatar `GreenMid`, columna nombre/email ajustada |
| `Paginas\AsistenciasPage.xaml` | Eliminado panel de validacion (solo queda lista) |
| `Paginas\AsistenciasPage.xaml.cs` | Eliminados ~200 lineas (validacion, DNI, huella) |

---

## 🔧 12. Rediseno del LoginWindow — layout horizontal (`01/06/2026`)

### Cambio
Se reemplazo el layout vertical (logo arriba, formulario abajo) por un layout horizontal con dos paneles:

| Elemento | Antes | Despues |
|----------|-------|---------|
| Tamano ventana | 420x620 | 780x440 |
| Logo | Centrado arriba, 250px | Izquierda centrado vertical, 200px |
| Titulo | "Sistema de Gestion" debajo del logo | "Sistema de Gestion" + "OptimusCAI Gym" en verde |
| Formulario | Abajo del logo, con margen 40px | Derecha, panel separado con margen propio |
| Footer | Fila centrada con copyright + Salir | Una sola fila: copyright a la izquierda, Salir a la derecha |
| Barra verde | Franja horizontal de 4px arriba | Franja vertical de 4px a la izquierda |

### Estructura del layout

```
┌─────────────────────────────────────────────────────┐
│ ██  [LOGO]          │  DNI                         │
│ ██  Sistema de      │  ┌────────────────────────┐  │
│ ██  Gestion         │  │ 👤 12345678            │  │
│ ██                   │  └────────────────────────┘  │
│ ██  OptimusCAI Gym  │  CONTRASENA                   │
│ ██                   │  ┌────────────────────────┐  │
│ ██                   │  │ 🔒 ●●●●●●       👁   │  │
│ ██                   │  └────────────────────────┘  │
│ ██                   │  [  ERROR  ]                 │
│ ██                   │  ┌────────────────────────┐  │
│ ██                   │  │       INGRESAR         │  │
│ ██                   │  └────────────────────────┘  │
├─────────────────────────────────────────────────────┤
│ OptimusCAI — Gestion de Gimnasio          ✕ Salir   │
└─────────────────────────────────────────────────────┘
```

### Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `LoginWindow.xaml` | Layout completo redisenado a split horizontal, agregado `txtPasswordVisible` (TextBox overlay) para toggle |
| `LoginWindow.xaml.cs` | Implementada funcionalidad mostrar/ocultar contrasena con swap PasswordBox ↔ TextBox, iconos FontAwesome |

---

## 🔧 13. Fix mostrar/ocultar contrasena en LoginWindow (`01/06/2026`)

### Problema
El boton "👁" para mostrar/ocultar la contrasena en el login no tenia funcionalidad real: solo cambiaba el icono pero no mostraba el texto de la contrasena.

### Causa raiz
En WPF, `PasswordBox` no permite mostrar el texto en claro. El codigo anterior solo alternaba el icono visual (`"👁" ↔ "🔒"`) sin cambiar el control subyacente.

### Solucion aplicada

Se implemento un swap real entre `PasswordBox` y `TextBox`:

**XAML:** Se agrego un `TextBox` (`txtPasswordVisible`) superpuesto en la misma celda del Grid que el `PasswordBox`, inicialmente oculto (`Visibility="Collapsed"`). Se reemplazo el icono emoji por `fa:ImageAwesome` con iconos FontAwesome (`Eye` / `EyeSlash`).

**Code-behind:** Se implemento la logica de sincronizacion:

```cs
if (_mostrandoPassword)
{
    // PasswordBox → TextBox visible
    txtPasswordVisible.Text = txtPassword.Password;
    txtPassword.Visibility = Visibility.Collapsed;
    txtPasswordVisible.Visibility = Visibility.Visible;
    iconTogglePass.Icon = "EyeSlash";
}
else
{
    // TextBox → PasswordBox oculto
    txtPassword.Password = txtPasswordVisible.Text;
    txtPasswordVisible.Visibility = Visibility.Collapsed;
    txtPassword.Visibility = Visibility.Visible;
    iconTogglePass.Icon = "Eye";
}
```

`IntentarLogin()` lee del control activo segun `_mostrandoPassword`.

### Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `LoginWindow.xaml` | Agregado `txtPasswordVisible`, iconos FontAwesome `Eye`/`EyeSlash` |
| `LoginWindow.xaml.cs` | Swap real PasswordBox/TextBox + sincronizacion |

---

## 🔧 14. Validacion de 10 minutos para salida de instructores en Dashboard (`01/06/2026`)

### Problema
Un instructor podia marcar entrada y salida casi al mismo tiempo desde el Dashboard, registrando asistencias con duracion minima o nula.

### Solucion aplicada
Se agrego una validacion en `sp_FicharInstructorDashboard` que impide registrar la salida si no pasaron al menos 10 minutos desde la entrada.

**SP:** Si se intenta registrar salida antes de los 10 minutos, retorna `operacion = "espera_minima"` con un mensaje indicando cuantos minutos faltan.

**Controller:** `FicharInstructorDashboard()` ahora trata `espera_minima` como error (`ok = false`), mostrando el mensaje en el Dashboard.

### Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `DataBase\spInstructorAsistencias.sql` | Validacion de 10 min en `sp_FicharInstructorDashboard` |
| `Controllers\InstructorAsistenciaController.cs` | `espera_minima` tratado como error |

### SP a ejecutar
```
DataBase\spInstructorAsistencias.sql
```

---

## 🔧 15. Dashboard: mostrar datos del instructor en error de espera mínima + simplificación de Asistencias de Instructores (`01/06/2026`)

### Cambios realizados

| Cambio | Descripción |
|--------|-------------|
| Dashboard muestra datos en error | Cuando un instructor intenta registrar salida antes de 10 min, el Dashboard ahora muestra foto, nombre y mensaje de error (antes solo mostraba error genérico) |
| Asistencias de Instructores simplificada | Se eliminó el panel "Fichaje Rápido" (DNI + botones entrada/salida). Solo queda la lista de asistencias con historial y reportes |

### Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `Controllers\InstructorAsistenciaController.cs` | Retornar `resultado` cuando sea `espera_minima` (para mostrar datos del instructor) |
| `Paginas\DashboardPage.xaml.cs` | Manejar `espera_minima` mostrando foto/nombre + mensaje de error |
| `Paginas\InstructorAsistenciasPage.xaml` | Eliminado panel "Fichaje Rápido", solo lista de asistencias |
| `Paginas\InstructorAsistenciasPage.xaml.cs` | Eliminados handlers de fichaje y lógica de panel admin/no-admin |

### SPs a ejecutar
```
DataBase\spInstructorAsistencias.sql
```

---

## 🔧 16. Envio Masivo de WhatsApp — Integracion Individual/Masivo (`03/06/2026`)

### Problema
La ventana de "Nuevo Mensaje" solo permitia crear mensajes individuales (un socio a la vez). No existia forma de enviar un mismo mensaje a multiples socios de forma masiva.

### Solucion aplicada
Se transformo la ventana `NuevoMensajeWindow` en un formulario unificado con selector de modo (Individual / Masivo):

| Modo | Comportamiento |
|------|---------------|
| **Individual** | Muestra selector de socio, campo de telefono, plantillas rapidas. Comportamiento original intacto. |
| **Masivo** | Muestra lista de socios con checkboxes, buscador interno, botones "Seleccionar Activos" y "Desmarcar Todos". |

### Caracteristicas del modo Masivo

| Feature | Detalle |
|---------|---------|
| Lista con header fijo | Columnas alineadas: Check, Socio, Telefono, Estado |
| Scroll con rueda del mouse | `PreviewMouseWheel` handler en el ScrollViewer |
| Buscador en tiempo real | Filtra por nombre o telefono |
| Seleccionar Activos | Marca solo socios con membresia activa |
| Desmarcar Todos | Limpia todas las selecciones sin re-renderizar |
| Plantillas compartidas | 👋 Bienvenida y 🎂 Cumpleaños visibles en ambos modos (genericas en masivo) |
| Solo socios activos | El SP filtra socios con `activo = 1` y `membresia.estado = 'activa'` |

### Archivos modificados/creados

| Archivo | Cambio |
|---------|--------|
| `DataBase\spWhatsapp.sql` | **2 SPs nuevos**: `sp_ListarSociosParaWhatsappMasivo`, `sp_InsertarWhatsappMensajeMasivo` |
| `Entities\WhatsappMensaje.cs` | **Nueva clase**: `SocioMasivoItem` (con `INotifyPropertyChanged` para binding de checkboxes) |
| `Models\DAO\WhatsappDao.cs` | Nuevos metodos: `ListarSociosParaMasivo()`, `InsertarMasivo()` |
| `Controllers\WhatsappController.cs` | Nuevos metodos: `ListarSociosParaMasivo()`, `InsertarMasivo()` (con validaciones) |
| `Ventanas\NuevoMensajeWindow.xaml` | Selector de modo (RadioButtons), paneles GridIndividual/GridMasivo, lista con SharedSizeGroup |
| `Ventanas\NuevoMensajeWindow.xaml.cs` | Logica de intercambio de modos, `GuardarMasivo()`, busqueda, scroll fix |
| `MiDiccionario.xaml` | **3 estilos nuevos**: `ToggleButtonStyle`, `BotonVerdeStyle`, `BotonRojoStyle` |
| `packages.config` | Agregado `BouncyCastle` 1.8.9 (dependencia de iTextSharp faltante) |

### SPs a ejecutar

```
DataBase\spWhatsapp.sql
```

Este archivo agrega los 2 nuevos SPs sin afectar los existentes. Ejecutar desde SQL Server Object Explorer sobre `DB_CAI_Optimus.mdf`.

### Nota sobre BouncyCastle

Se agrego `BouncyCastle` 1.8.9 al `packages.config` para que `nuget restore` descargue la dependencia de iTextSharp que faltaba. Sin esto, la exportacion a PDF crashea en maquinas que no tienen la carpeta `packages\BouncyCastle.1.8.9\` preexistente.

```xml
<package id="BouncyCastle" version="1.8.9" targetFramework="net472" />
```

Despues de agregarlo, ejecutar:
```
nuget restore SistemaGimnacionOptimusCAI.sln
```

---

## 17. Notificaciones de membresias por vencer en Dashboard (`05/06/2026`)

### Objetivo
Agregar un aviso operativo al iniciar sesion para detectar socios con membresias proximas a vencer y permitir contacto rapido por WhatsApp Web.

### Solucion aplicada
Se agrego un modulo liviano de notificaciones de membresias por vencer, respetando la arquitectura del sistema:

| Capa | Implementacion |
|------|----------------|
| SP | `sp_ObtenerNotificacionesMembresiasPorVencer` en `DataBase\spMembresias.sql` |
| Entity | `Entities\NotificacionMembresia.cs` |
| DAO | `Models\DAO\NotificacionMembresiaDao.cs` |
| Controller | `Controllers\NotificacionMembresiaController.cs` |
| UI | `Paginas\DashboardPage.xaml` y `Paginas\DashboardPage.xaml.cs` |

### Comportamiento
- Al cargar el Dashboard despues del login, se consultan membresias activas que vencen entre hoy y los proximos 7 dias.
- Las alertas se muestran en el panel derecho `PROXIMOS VENCIMIENTOS`.
- Cada alerta muestra socio, numero de socio, actividad, fecha de vencimiento y estado del vencimiento.
- El boton `Abrir en WhatsApp` abre WhatsApp Web con un mensaje personalizado usando:
  - nombre del socio
  - numero de socio
  - actividad
  - fecha de vencimiento
- El panel de vencimientos quedo con alto fijo y scroll interno para no achicar la seccion `ACCESOS RAPIDOS`.
- El boton usa el estilo existente `BotonVerdeStyle` definido en `MiDiccionario.xaml`.

### Archivos modificados/creados

| Archivo | Cambio |
|---------|--------|
| `DataBase\spMembresias.sql` | Nuevo SP `sp_ObtenerNotificacionesMembresiasPorVencer` |
| `Entities\NotificacionMembresia.cs` | Nueva entidad para representar la alerta |
| `Entities\Entities.csproj` | Inclusion de la nueva entidad |
| `Models\DAO\NotificacionMembresiaDao.cs` | Nuevo DAO que consume el SP |
| `Models\Models.csproj` | Inclusion del nuevo DAO |
| `Controllers\NotificacionMembresiaController.cs` | Nuevo controller con logica de consulta y armado de URL WhatsApp |
| `Controllers\Controllers.csproj` | Inclusion del nuevo controller |
| `Paginas\DashboardPage.xaml` | Ajuste del panel derecho: vencimientos con alto fijo y scroll interno |
| `Paginas\DashboardPage.xaml.cs` | Carga de alertas, render de cards y apertura de WhatsApp Web |

### SPs a ejecutar

Para evitar errores de base de datos, ejecutar:

```
DataBase\spMembresias.sql
```

Ese script crea/actualiza el SP:

```
sp_ObtenerNotificacionesMembresiasPorVencer
```

### Importante
Si la base ya tiene los scripts anteriores aplicados, no hace falta recrear tablas. Solo ejecutar `DataBase\spMembresias.sql` sobre la base `DB_CAI_Optimus.mdf` en `(LocalDB)\MSSQLLocalDB`.

Si tambien se va a usar el modulo completo de WhatsApp, verificar que ya este ejecutado:

```
DataBase\spWhatsapp.sql
```

---

## 18. Resumen final de cambios recientes

### Mini resumen
- Se agregaron notificaciones de membresias por vencer en el Dashboard.
- Se incorporo el boton para abrir WhatsApp Web con mensaje personalizado.
- Se dejo la asistencia usando `dias_sesiones` como base del limite semanal.
- Se adapto el upgrade para que use `categoria` + `dias_sesiones` y ya no dependa de `nivel`.

### SPs que se deben ejecutar
Ejecutar estos scripts sobre la base `DB_CAI_Optimus.mdf` para dejar el sistema alineado con los cambios actuales:

```
DataBase\script tablas.sql
DataBase\spSocios.sql
DataBase\spActividades.sql
DataBase\spAsistencias.sql
DataBase\spMembresias.sql
DataBase\sp_ListarSociosConMembresias.sql
DataBase\spInstructorAsistencias.sql
DataBase\spWhatsapp.sql
DataBase\spActualizarActividadesCategoriaNivel.sql
```

Si ya tenes la base creada y solo queres aplicar las ultimas correcciones, al menos ejecuta:

```
DataBase\spActividades.sql
DataBase\spAsistencias.sql
DataBase\spMembresias.sql
DataBase\sp_ListarSociosConMembresias.sql
DataBase\spInstructorAsistencias.sql
DataBase\spWhatsapp.sql
DataBase\spActualizarActividadesCategoriaNivel.sql
```

---

## 19. Renovacion de membresias desde Socios

### Mini resumen
- Se agrego la renovacion de membresia directamente desde la tabla de Socios.
- La renovacion permite usar una membresia activa, vencida o cancelada y volver a cobrarla.
- Desde la ventana emergente se puede cambiar la actividad, instructor, monto y observaciones.
- Se incorporo un preview de cobro similar al de Nuevo Socio para ver siempre cuanto se va a cobrar.
- La renovacion actualiza fecha de inicio, fecha de vencimiento, estado, historial y caja.

### Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `DataBase\spMembresias.sql` | `sp_RenovarMembresia` acepta actividad, instructor y observaciones |
| `Models\DAO\MembresiaDao.cs` | Renovacion parametrizada con actividad/instructor opcionales |
| `Controllers\MembresiaController.cs` | Logica de renovacion y auditoria |
| `Ventanas\MembresiaWindow.xaml` | Agregado preview de cobro |
| `Ventanas\MembresiaWindow.xaml.cs` | Modo renovacion con calculo y confirmacion |
| `Paginas\SociosPage.xaml` | Boton de renovacion en la tabla |
| `Paginas\SociosPage.xaml.cs` | Handler para abrir la renovacion del socio |

### SP a ejecutar
Ejecutar nuevamente:

```
DataBase\spMembresias.sql
```

---

## 20. Correcciones recientes en Socios y Membresias

### Mini resumen
- Se agrego la validacion de edad minima para crear socios: por ahora solo se permiten socios mayores de 6 anios.
- La validacion se aplica al presionar `Siguiente` en la ventana emergente `Nuevo Socio`.
- Tambien se dejo la misma regla en el formulario embebido de `SociosPage` por si ese flujo se vuelve a usar.
- Al cambiar filtros/chips en Socios, el scroll de la tabla vuelve al inicio para evitar cargas automaticas no deseadas.
- Al dar de baja o restaurar un socio desde la ficha abierta por el buscador global, se actualizan dinamicamente los stats y listados de Socios.
- Al dar de baja un socio, sus membresias activas se cancelan automaticamente.
- La ficha del socio ahora recarga sus membresias al darlo de baja/restaurarlo, para mostrar el estado actualizado sin cerrar y volver a buscar.
- Tambien se agrego refresco del listado de Membresias si esa pagina esta abierta.

### Archivos modificados

| Archivo | Cambio |
|---------|--------|
| `Ventanas\NuevoSocioWindow.xaml.cs` | Validacion de edad minima de 6 anios al avanzar desde el paso de datos personales |
| `Paginas\SociosPage.xaml.cs` | Validacion de edad, reset de scroll al cambiar filtros y refresco de stats/listado |
| `Paginas\FichaSocioWindow.xaml.cs` | Marca de cambios y recarga dinamica de membresias al dar de baja/restaurar |
| `Paginas\MembresiasPage.xaml.cs` | Metodo publico para refrescar el listado cuando cambia el socio desde otra ventana |
| `MainWindow.xaml.cs` | Refresco de paginas afectadas cuando la ficha de socio modifica estado |
| `Models\DAO\SocioDao.cs` | Lectura de membresias canceladas devueltas por los SPs |
| `Controllers\SocioController.cs` | Auditoria de membresias canceladas por baja de socio |
| `DataBase\spSocios.sql` | Cancelacion automatica de membresias activas al dar de baja socios |

### SPs a ejecutar

Como se modificaron Stored Procedures de Socios, ejecutar nuevamente:

```
DataBase\spSocios.sql
```

Ese script actualiza principalmente:

```
sp_CambiarEstadoSocio
sp_DarDeBajaSocios
```

### Importante
Para las validaciones de edad, el reset de scroll y el refresco dinamico de la ficha no hace falta ejecutar SPs adicionales.

Si no se ejecuta `DataBase\spSocios.sql`, la aplicacion puede compilar, pero la base no tendra aplicada la cancelacion automatica de membresias activas al dar de baja socios.
