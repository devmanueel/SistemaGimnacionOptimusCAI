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
