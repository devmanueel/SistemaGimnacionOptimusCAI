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
