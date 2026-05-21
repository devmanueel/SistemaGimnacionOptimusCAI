# SDD — Módulo Membresías
Versión: 1.0  
Proyecto: OptimusCAI Gym  

---

## Objetivo

Mejorar el comportamiento del módulo de Membresías para evitar errores de usuario y mejorar la experiencia visual.

---

## Reglas del negocio

- Todas las membresías duran exactamente **31 días**
- La duración NO se puede modificar
- No depende de la actividad
- No depende del usuario

---

## Fechas

- La fecha de inicio es siempre la fecha actual
- La fecha de vencimiento se calcula automáticamente sumando 31 días

Ejemplo:
- Inicio: 20/05/2026  
- Vencimiento: 20/06/2026  

---

## Comportamiento esperado

### Fechas

- El usuario NO puede modificar:
  - Fecha de inicio
  - Fecha de vencimiento

- Ambos campos deben mostrarse en pantalla, pero deshabilitados

---

### Flujo al crear una membresía

1. El usuario selecciona el socio
2. El sistema asigna automáticamente:
   - FechaInicio = hoy
   - FechaVencimiento = hoy + 31 días
3. Las fechas se muestran en pantalla
4. El usuario solo completa los datos necesarios (ej: monto)

---

### Preview de monto

- Cuando el usuario ingresa un monto:
  - Se muestra un preview del valor
  - Este preview debe aparecer con una animación suave (fade)
  - No debe aparecer de forma brusca

---

## Reglas técnicas

- No mover lógica al code-behind
- Mantener arquitectura actual (Controller, DAO, SP)
- No permitir edición manual de fechas
- Mantener estilos del sistema (dark, colores actuales)

---

## Resultado esperado

- El usuario no puede cometer errores con fechas
- La lógica es consistente en todo el sistema
- La interfaz es más clara y agradable

-------------- REGLAS DE NOGOCIO PARA MEMBRESIAS -----------------

---

## Regla de negocio — Cambio de plan dentro de la misma categoría

### Contexto

Dentro de una misma categoría (ej: gimnasio), existen múltiples variantes de membresía con distinto nivel:

Ejemplo:
- Gimnasio 1 vez por semana   → nivel bajo
- Gimnasio 2 veces por semana → nivel medio
- Gimnasio libre              → nivel alto

---

### Definición

Se introduce el concepto de **nivel de plan**:

- Cada actividad debe tener una propiedad `nivel`
- El nivel representa la jerarquía del plan dentro de la categoría
- A mayor valor, mayor nivel

Ejemplo:
- 1 vez por semana   → nivel = 1
- 2 veces por semana → nivel = 2
- libre              → nivel = 3

---

### Regla principal

Un socio puede cambiar su membresía actual SOLO si:

- La nueva actividad pertenece a la **MISMA categoría**
- El nuevo plan tiene un **nivel superior** al actual

---

### Restricciones

❌ NO se permite:
- Cambiar a otra categoría (ej: gimnasio → boxeo)
- Cambiar a un nivel inferior (downgrade)
- Cambiar al mismo nivel

---

### Forma de uso

- Este cambio SOLO puede realizarse desde la acción **Modificar membresía**
- NO se debe permitir creando una nueva membresía

---

### Comportamiento esperado

#### Caso 1 — Upgrade válido
- Actual: Gimnasio 2x semana (nivel 2)
- Nuevo: Gimnasio libre (nivel 3)

✅ Resultado:
- Permitir modificación

---

#### Caso 2 — Downgrade
- Actual: Gimnasio libre (nivel 3)
- Nuevo: Gimnasio 2x semana (nivel 2)

❌ Resultado:
- Bloquear

---

#### Caso 3 — Cambio de categoría
- Actual: Gimnasio 2x
- Nuevo: Boxeo 2x

❌ Resultado:
- Bloquear

---

### Implementación técnica

Validación en Controller o SP de modificación:

```sql
-- Obtener categoría y nivel actual
SELECT 
    @CategoriaActual = a.categoria,
    @NivelActual = a.nivel
FROM membresias m
JOIN actividades a ON a.id = m.actividad_id
WHERE m.id = @MembresiaId;

-- Obtener nueva categoría y nivel
SELECT 
    @CategoriaNueva = categoria,
    @NivelNuevo = nivel
FROM actividades
WHERE id = @NuevaActividadId;

-- Validaciones
IF @CategoriaActual <> @CategoriaNueva
BEGIN
    RAISERROR('No se puede cambiar a otra categoría.', 16, 1);
    RETURN;
END

IF @NivelNuevo <= @NivelActual
BEGIN
    RAISERROR('Solo se permite cambiar a un plan superior.', 16, 1);
    RETURN;
END
