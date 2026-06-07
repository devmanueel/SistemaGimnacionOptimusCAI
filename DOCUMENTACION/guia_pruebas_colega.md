# Guia para probar OptimusCAI en otra PC

Fecha: 07/06/2026

Este documento explica como levantar el proyecto en Debug, como funciona la base de datos local y que revisar si la aplicacion no inicia.

## 1. Requisitos

Instalar en la PC:

- Visual Studio 2022.
- .NET Framework 4.7.2 Developer Pack.
- SQL Server LocalDB.
- SQL Server Management Studio, recomendado para revisar la base.
- Driver DigitalPersona si se va a probar el lector de huellas.
- Inno Setup solo si se va a generar el instalador.

El proyecto debe compilarse como:

```powershell
Configuration: Debug
Platform: x86
```

Tambien para Release/instalador se debe usar:

```powershell
Configuration: Release
Platform: x86
```

## 2. Restaurar paquetes y compilar

Desde la raiz del proyecto:

```powershell
nuget restore SistemaGimnacionOptimusCAI.sln
```

Luego abrir la solucion en Visual Studio 2022:

```text
SistemaGimnacionOptimusCAI.sln
```

Seleccionar:

```text
Debug | x86
```

Compilar:

```text
Build > Build Solution
```

## 3. Como funciona la base de datos en Debug

La aplicacion usa `|DataDirectory|`.

En Debug, `DataDirectory` apunta a la carpeta donde se ejecuta el `.exe`:

```text
bin\x86\Debug\
```

Por eso, al ejecutar en Debug, la base debe quedar en:

```text
bin\x86\Debug\DataBase\DB_CAI_Optimus.mdf
bin\x86\Debug\DataBase\DB_CAI_Optimus_log.ldf
```

No deberia copiarse a mano si el proyecto esta bien compilado. Visual Studio copia estos archivos desde:

```text
DataBase\DB_CAI_Optimus.mdf
DataBase\DB_CAI_Optimus_log.ldf
```

hacia:

```text
bin\x86\Debug\DataBase\
```

Importante: si se agregan, editan o borran datos mientras se prueba en Debug, esos cambios quedan en la copia de `bin\x86\Debug\DataBase`, no necesariamente en la base que esta en la raiz del proyecto.

## 4. Datos de prueba

La base actual es de prueba. Se usa para validar que la aplicacion funcione correctamente antes de cargar los datos reales del gimnasio.

Antes de entregar al cliente se debe limpiar la base y dejar solo:

- Roles necesarios.
- Usuario administrador.
- Configuraciones base.
- Datos reales del gimnasio.

Usuario administrador de prueba:

```text
DNI: 00000001
Clave: admin123
```

## 5. Scripts de base de datos

Si se necesita recrear la base desde scripts, ejecutar en este orden:

```text
1. DataBase\script tablas.sql
2. DataBase\sp*.sql
```

Los stored procedures pueden ejecutarse en cualquier orden despues de crear las tablas, salvo que algun script indique lo contrario.

Archivos importantes:

```text
DataBase\script tablas.sql
DataBase\spSocios.sql
DataBase\spMembresias.sql
DataBase\spAsistencias.sql
DataBase\spVentas.sql
DataBase\spMovimientos.sql
DataBase\spConfiguracion.sql
DataBase\spInstructorAsistencias.sql
DataBase\spWhatsapp.sql
```

## 6. Si la aplicacion no inicia

Revisar si se genero este archivo junto al `.exe`:

```text
bin\x86\Debug\error_inicio.txt
```

En instalacion final puede aparecer en:

```text
C:\OptimusCAI\error_inicio.txt
```

Errores comunes:

```text
No se encuentra DB_CAI_Optimus.mdf
```

Solucion:

- Verificar que exista `bin\x86\Debug\DataBase\DB_CAI_Optimus.mdf`.
- Verificar que exista `bin\x86\Debug\DataBase\DB_CAI_Optimus_log.ldf`.
- Recompilar en `Debug | x86`.

```text
No se puede cargar DPFPApi.dll
```

Solucion:

- Instalar el driver DigitalPersona correspondiente.
- Reiniciar la aplicacion.
- Si no se va a usar huella en esa PC, la app deberia iniciar igual y mostrar que no se detecto lector.

```text
La conexion con el servidor se ha establecido correctamente, pero se produjo un error durante el inicio de sesion
```

Solucion:

- Verificar que SQL Server LocalDB este instalado.
- Verificar que la base no este bloqueada por otra instancia.
- Cerrar la app y Visual Studio si estan usando el mismo `.mdf`.
- Recompilar y ejecutar otra vez.

## 7. Probar en Debug

Pasos recomendados:

1. Hacer `pull` del repo.
2. Restaurar NuGet.
3. Seleccionar `Debug | x86`.
4. Compilar.
5. Verificar que exista:

```text
bin\x86\Debug\DataBase\DB_CAI_Optimus.mdf
bin\x86\Debug\DataBase\DB_CAI_Optimus_log.ldf
bin\x86\Debug\Assets\Sounds\acceso_ok.wav
bin\x86\Debug\Assets\Sounds\acceso_error.wav
```

6. Ejecutar desde Visual Studio.
7. Iniciar sesion con el usuario administrador de prueba.
8. Probar las secciones principales:

```text
Inicio
Socios
Membresias
Asistencias
Caja
Ventas
WhatsApp
Fichaje Instructores
Reportes
```

## 8. Probar sonidos de asistencia

Los sonidos estan en:

```text
Assets\Sounds\acceso_ok.wav
Assets\Sounds\acceso_error.wav
```

Al compilar deben copiarse a:

```text
bin\x86\Debug\Assets\Sounds\
```

Comportamiento esperado:

- Asistencia valida: suena `acceso_ok.wav`.
- Asistencia rechazada o error: suena `acceso_error.wav`.

## 9. Generar Release para instalador

Antes de generar el instalador:

1. Cerrar la aplicacion si esta abierta.
2. Seleccionar:

```text
Release | x86
```

3. Compilar la solucion.
4. Verificar que exista:

```text
bin\x86\Release\SistemaGimnacionOptimusCAI.exe
bin\x86\Release\DataBase\DB_CAI_Optimus.mdf
bin\x86\Release\DataBase\DB_CAI_Optimus_log.ldf
bin\x86\Release\Assets\Sounds\acceso_ok.wav
bin\x86\Release\Assets\Sounds\acceso_error.wav
```

## 10. Generar instalador con Inno Setup

Abrir este archivo con Inno Setup:

```text
Instalador\OptimusCAI.iss
```

Compilar el script.

El instalador generado queda normalmente en:

```text
Instalador\Output\
```

La instalacion copia la aplicacion en:

```text
C:\OptimusCAI\
```

La base instalada queda en:

```text
C:\OptimusCAI\DataBase\DB_CAI_Optimus.mdf
C:\OptimusCAI\DataBase\DB_CAI_Optimus_log.ldf
```

## 11. Importante para trabajar en equipo

Mientras la base sea de prueba, se puede subir al repo para que ambos tengan los mismos datos.

Pero hay que tener cuidado:

- Si uno modifica la base `.mdf` y la sube, puede pisar datos de prueba del otro.
- Los cambios reales de estructura o stored procedures deben quedar siempre en scripts SQL.
- Antes de entregar al cliente se debe generar una base limpia, sin datos ficticios.

Para revisar antes de subir:

```powershell
git status
git diff
```

Subir solo lo necesario:

- Codigo fuente.
- XAML.
- Scripts SQL.
- Assets necesarios.
- Instalador `.iss`.
- Base `.mdf` y `.ldf` solo mientras se use como base compartida de prueba.

