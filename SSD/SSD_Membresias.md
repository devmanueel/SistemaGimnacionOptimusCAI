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