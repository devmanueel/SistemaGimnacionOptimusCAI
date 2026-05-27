// ============================================================
//  Helpers/WinBioNative.cs
//
//  P/Invoke para la API Windows Biometric Framework (winbio.dll).
//  Compatible con C# 7.3 / .NET 4.7.2.
// ============================================================

using System;
using System.Runtime.InteropServices;

namespace SistemaGimnacionOptimusCAI.Helpers
{
    internal static class WinBioNative
    {
        // ── Constantes ─────────────────────────────────────────

        // Factor biométrico
        public const uint WINBIO_TYPE_FINGERPRINT = 0x00000008;

        // Tipos de pool
        public const uint WINBIO_POOL_SYSTEM  = 1;
        public const uint WINBIO_POOL_PRIVATE = 2;

        // Flags de sesión
        public const uint WINBIO_FLAG_DEFAULT  = 0x00000000;
        public const uint WINBIO_FLAG_BASIC    = 0x00000001;
        public const uint WINBIO_FLAG_ADVANCED = 0x00000002;

        // Dedo / subfactor
        public const byte WINBIO_SUBTYPE_ANY = 0xFF;

        // Tipos de identidad
        public const uint WINBIO_ID_TYPE_NULL     = 0;
        public const uint WINBIO_ID_TYPE_WILDCARD = 1;
        public const uint WINBIO_ID_TYPE_GUID     = 2;
        public const uint WINBIO_ID_TYPE_SID      = 3;

        // HRESULT de éxito
        public const int S_OK = 0;

        // Códigos de error WBF
        public const int WINBIO_E_DATABASE_ALREADY_EXISTS = unchecked((int)0x80098001);
        public const int WINBIO_E_UNKNOWN_ID              = unchecked((int)0x80098005);
        public const int WINBIO_E_BAD_CAPTURE             = unchecked((int)0x80098009);
        public const int WINBIO_E_CANCELED                = unchecked((int)0x80098013);
        public const int WINBIO_E_NO_MATCH                = unchecked((int)0x80098017);
        public const int WINBIO_E_ENROLLMENT_INCOMPLETE   = unchecked((int)0x8009800E);

        // Razones de rechazo de captura
        public const uint WINBIO_FP_TOO_HIGH    = 1;
        public const uint WINBIO_FP_TOO_LOW     = 2;
        public const uint WINBIO_FP_TOO_LEFT    = 3;
        public const uint WINBIO_FP_TOO_RIGHT   = 4;
        public const uint WINBIO_FP_TOO_FAST    = 5;
        public const uint WINBIO_FP_TOO_SLOW    = 6;
        public const uint WINBIO_FP_POOR_QUALITY = 7;
        public const uint WINBIO_FP_TOO_SKEWED  = 8;
        public const uint WINBIO_FP_TOO_SHORT   = 9;
        public const uint WINBIO_FP_MERGE_FAILURE = 10;

        // ── Estructuras ────────────────────────────────────────

        // Información de una unidad biométrica (2584 bytes)
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WINBIO_UNIT_SCHEMA
        {
            public uint UnitId;
            public uint PoolType;
            public uint BiometricFactor;
            public uint SensorSubType;
            public uint Capabilities;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string DeviceInstanceId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string Description;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string Manufacturer;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string Model;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string SerialNumber;
            public ushort FirmwareMajor;
            public ushort FirmwareMinor;
        }

        // Información de una base de datos biométrica (1064 bytes)
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WINBIO_STORAGE_SCHEMA
        {
            public uint BiometricFactor;
            public Guid DatabaseId;
            public Guid DataFormat;
            public uint Attributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string FilePath;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string ConnectString;
        }

        // Identidad biométrica (76 bytes: 4 Type + 72 union)
        // Con layout explícito para poder leer el GUID del campo union
        [StructLayout(LayoutKind.Explicit, Size = 76)]
        public struct WINBIO_IDENTITY
        {
            [FieldOffset(0)]
            public uint Type;   // WINBIO_ID_TYPE_*

            // Guid comienza en el byte 4 (offset en la union = 0 relativo a la union)
            [FieldOffset(4)]
            public Guid Guid;   // válido cuando Type == WINBIO_ID_TYPE_GUID
        }

        // ── Funciones P/Invoke ─────────────────────────────────

        [DllImport("winbio.dll")]
        public static extern int WinBioEnumBiometricUnits(
            uint factor,
            out IntPtr schemaArrayPtr,  // PWINBIO_UNIT_SCHEMA* (alocado por WBF)
            out IntPtr unitCount);      // SIZE_T*

        [DllImport("winbio.dll")]
        public static extern int WinBioEnumDatabases(
            uint factor,
            out IntPtr storageSchemaArrayPtr,  // PWINBIO_STORAGE_SCHEMA*
            out IntPtr storageCount);          // SIZE_T*

        [DllImport("winbio.dll", CharSet = CharSet.Unicode)]
        public static extern int WinBioCreateDatabase(
            ref WINBIO_UNIT_SCHEMA unitSchema,
            ref Guid databaseId,
            ref Guid dataFormat,
            string filePath,       // null = ruta por defecto
            string connectString); // null = sin cadena de conexión

        [DllImport("winbio.dll")]
        public static extern int WinBioOpenSession(
            uint factor,
            uint poolType,
            uint flags,
            uint[] unitArray,   // WINBIO_UNIT_ID[] (null para pool del sistema)
            IntPtr unitCount,   // SIZE_T
            IntPtr databaseId,  // WINBIO_UUID* (null para pool del sistema)
            out uint sessionHandle);

        [DllImport("winbio.dll")]
        public static extern int WinBioCloseSession(uint sessionHandle);

        [DllImport("winbio.dll")]
        public static extern int WinBioFree(IntPtr address);

        [DllImport("winbio.dll")]
        public static extern int WinBioAcquireFocus();

        [DllImport("winbio.dll")]
        public static extern int WinBioReleaseFocus();

        [DllImport("winbio.dll")]
        public static extern int WinBioCancel(uint sessionHandle);

        [DllImport("winbio.dll")]
        public static extern int WinBioEnrollBegin(
            uint sessionHandle,
            byte subFactor,
            uint unitId);

        [DllImport("winbio.dll")]
        public static extern int WinBioEnrollCaptureSample(
            uint sessionHandle,
            out uint rejectDetail);

        [DllImport("winbio.dll")]
        public static extern int WinBioEnrollCommit(
            uint sessionHandle,
            ref WINBIO_IDENTITY identity,
            [MarshalAs(UnmanagedType.U1)]
            out bool isNewTemplate);

        [DllImport("winbio.dll")]
        public static extern int WinBioEnrollDiscard(uint sessionHandle);

        [DllImport("winbio.dll")]
        public static extern int WinBioIdentify(
            uint sessionHandle,
            out uint unitId,
            out WINBIO_IDENTITY identity,
            out byte subFactor,
            out uint rejectDetail);

        [DllImport("winbio.dll")]
        public static extern int WinBioDeleteTemplate(
            uint sessionHandle,
            uint unitId,
            ref WINBIO_IDENTITY identity,
            byte subFactor);

        // ── Helpers ────────────────────────────────────────────

        public static bool Exitoso(int hr) => hr == S_OK;

        public static string DescribirRechazo(uint rejectDetail)
        {
            switch (rejectDetail)
            {
                case WINBIO_FP_TOO_HIGH:     return "Dedo demasiado arriba";
                case WINBIO_FP_TOO_LOW:      return "Dedo demasiado abajo";
                case WINBIO_FP_TOO_LEFT:     return "Dedo demasiado a la izquierda";
                case WINBIO_FP_TOO_RIGHT:    return "Dedo demasiado a la derecha";
                case WINBIO_FP_TOO_FAST:     return "Movimiento demasiado rápido";
                case WINBIO_FP_POOR_QUALITY: return "Calidad de imagen insuficiente";
                case WINBIO_FP_TOO_SKEWED:   return "Dedo inclinado";
                case WINBIO_FP_TOO_SHORT:    return "Área de contacto demasiado pequeña";
                case WINBIO_FP_MERGE_FAILURE: return "No coincide con capturas previas";
                default:                     return "Captura no válida, intentá de nuevo";
            }
        }
    }
}
