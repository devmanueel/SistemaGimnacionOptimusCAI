# SDD — Fix Errores de Compilación
> Errores detectados al compilar — Mayo 2026  
> Resolver en este orden exacto antes de continuar

---

## ERRORES DETECTADOS

### Error 1 — CS0104 (Error de compilación — BLOQUEA el build)
```
'Validador' es una referencia ambigua entre
'SistemaGimnacionOptimusCAI.Helpers.Validador' y 'Controllers.Validador'

Archivo: SociosPage.xaml.cs — Línea 487
```

### Errores 2, 3, 4 — NU1902 (Advertencias de seguridad)
```
El paquete "BouncyCastle" 1.8.9 tiene una vulnerabilidad de gravedad
moderate conocida:
  - https://github.com/advisories/GHSA-8xfc-gm6g-vgpv
  - https://github.com/advisories/GHSA-m44j-cfrm-g8qc
  - https://github.com/advisories/GHSA-v435-xc8x-wvr9
```

### Error 5 — Referencia rota (Advertencia — impide linkear la DLL)
```
No se puede agregar el archivo
'packages\BouncyCastle.1.8.9\lib\BouncyCastle.Crypto.dll' al proyecto.
No se puede agregar un vínculo al archivo
C:\Users\USUARIO\Desktop\lpoo2\Proyectos\SistemaGimnacionOptimusCAI\
packages\BouncyCastle.1.8.9\lib\BouncyCastle.Crypto.dll
Este archivo se encuentra dentro del árbol de directorios del proyecto.
```

### Error 6 — Componente no encontrado
```
No se pudo encontrar el componente 'BouncyCastle.Crypto'
al que se hace referencia.
```

---

## CAUSA RAÍZ DE CADA ERROR

### CS0104 — Validador ambiguo
**Causa:** Existen DOS clases llamadas `Validador` accesibles desde `SociosPage.xaml.cs`:
- `Controllers.Validador` — la original donde vive la lógica de validación
- `SistemaGimnacionOptimusCAI.Helpers.Validador` — se creó una segunda por error o un `using` importa ambos namespaces

El compilador no sabe cuál usar en la línea 487.

### NU1902 + Errores de BouncyCastle
**Causa:** Se instaló `BouncyCastle 1.8.9` que tiene vulnerabilidades conocidas Y la DLL quedó dentro del árbol del proyecto en vez de en la carpeta `packages` externa. Esto rompe la referencia.

**BouncyCastle probablemente se instaló** para generar el QR del carnet PDF. No es la librería correcta para eso — el QR debe hacerse con `ZXing.Net` que ya estaba planificado en el SDD del carnet.

---

## PLAN DE SOLUCIÓN

```
PASO 1 → Eliminar BouncyCastle del proyecto
PASO 2 → Instalar ZXing.Net (la correcta para QR)
PASO 3 → Resolver el Validador ambiguo en SociosPage.xaml.cs
PASO 4 → Verificar que no existe Helpers/Validador.cs duplicado
PASO 5 → Compilar y confirmar 0 errores
```

---

## PASO 1 — Eliminar BouncyCastle

### En Visual Studio:

1. Click derecho sobre el proyecto `SistemaGimnacionOptimusCAI` en el Explorador de soluciones
2. Seleccionar **Administrar paquetes NuGet**
3. Ir a la pestaña **Instalados**
4. Buscar `BouncyCastle` → click en **Desinstalar**
5. Aceptar todos los prompts

### Si la referencia a la DLL quedó suelta, eliminarla manualmente:

1. En el Explorador de soluciones, expandir **Referencias** del proyecto UI
2. Si aparece `BouncyCastle.Crypto` con ícono de advertencia (⚠️) → click derecho → **Quitar**
3. Guardar el proyecto

### Verificar en el `.csproj` que no quedó rastro:

Abrir `SistemaGimnacionOptimusCAI.csproj` con un editor de texto y verificar que NO existe ninguna línea como:
```xml
<Reference Include="BouncyCastle.Crypto">
  <HintPath>...\BouncyCastle.Crypto.dll</HintPath>
</Reference>
```
Si existe, eliminar ese bloque `<Reference>` completo.

---

## PASO 2 — Instalar ZXing.Net (para QR en carnets)

En **Package Manager Console** (`Tools → NuGet Package Manager → Package Manager Console`):

```powershell
Install-Package ZXing.Net -Version 0.16.9
```

Verificar que se instaló correctamente:
```powershell
Get-Package | Where-Object { $_.Id -like "*ZXing*" }
```
Debe mostrar: `ZXing.Net  0.16.9`

---

## PASO 3 — Resolver CS0104: Validador ambiguo

### Opción A — Verificar si existe un Validador duplicado en Helpers

Buscar en todo el proyecto si existe el archivo `Helpers/Validador.cs`:

En Visual Studio: `Ctrl+Shift+F` → buscar `class Validador` en todos los archivos.

**Si aparece más de una vez:**
- La clase `Validador` debe existir SOLO en `Controllers/Validador.cs`
- Si existe una copia en `Helpers/` → **eliminar ese archivo**
- Si existe una copia en cualquier otro lugar → **eliminar esa copia**

### Opción B — Calificar el uso en SociosPage.xaml.cs (fix directo)

Si por alguna razón ambas deben existir, calificar explícitamente el uso en `SociosPage.xaml.cs`:

**Buscar** en `SociosPage.xaml.cs` línea 487 (y cualquier otra línea que use `Validador`) y **reemplazar**:

```csharp
// ANTES (ambiguo):
Validador.ValidarTelefono(txtTelefono.Text)
Validador.EsCaracterTelefonoValido(e.Text)

// DESPUÉS (calificado con namespace completo):
Controllers.Validador.ValidarTelefono(txtTelefono.Text)
Controllers.Validador.EsCaracterTelefonoValido(e.Text)
```

### Opción C — Quitar el using ambiguo (más limpio)

En la parte superior de `SociosPage.xaml.cs`, buscar si existe:
```csharp
using SistemaGimnacionOptimusCAI.Helpers;
```

Si ese `using` es el que trae el `Validador` de Helpers al scope, y la clase `Helpers.Validador` no debería existir, simplemente **eliminar ese archivo** `Helpers/Validador.cs`.

**La regla es:** `Validador` vive ÚNICAMENTE en `Controllers/Validador.cs`. No duplicar en Helpers.

---

## PASO 4 — Verificar que no hay más duplicados

Hacer búsqueda global en Visual Studio (`Ctrl+Shift+F`):

```
Buscar: class Validador
En: Toda la solución
```

El resultado debe mostrar **exactamente UN archivo**: `Controllers/Validador.cs`.

Si aparece en más archivos → eliminar todos los duplicados y dejar solo el de Controllers.

---

## PASO 5 — Compilar y verificar

`Ctrl+Shift+B` → debe mostrar:

```
========== Compilación: 4 correctas, 0 incorrectas, 0 omitidas ==========
```

Si quedan advertencias NU1902 después de desinstalar BouncyCastle:
- Son de otros paquetes con vulnerabilidades menores
- No bloquean el build
- Se pueden ignorar por ahora o actualizar los paquetes afectados

---

## RESUMEN RÁPIDO PARA CLAUDE CODE

```
1. Desinstalar NuGet: BouncyCastle 1.8.9
2. Instalar NuGet: ZXing.Net 0.16.9
3. Eliminar referencia suelta BouncyCastle.Crypto de las Referencias del proyecto
4. Buscar "class Validador" en toda la solución
5. Si existe en más de un archivo → eliminar todos menos Controllers/Validador.cs
6. En SociosPage.xaml.cs línea 487: usar Controllers.Validador en lugar de solo Validador
7. Compilar → 0 errores
```

---

*Fix Errores Compilación — OptimusCAI Gym — Mayo 2026*
