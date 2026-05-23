using System;

namespace Entities
{
    public class FichaMedica
    {
        public long Id { get; set; }
        public long SocioId { get; set; }
        public decimal? PesoKg { get; set; }
        public short? AlturaCm { get; set; }
        public string GrupoSanguineo { get; set; }
        public string Enfermedades { get; set; }
        public string Medicamentos { get; set; }
        public string RestriccionesFisicas { get; set; }
        public string ContactoEmergencia { get; set; }
        public string TelefonoEmergencia { get; set; }
        public bool AptoFisico { get; set; }
        public DateTime? FechaApto { get; set; }
        public string Observaciones { get; set; }
        public DateTime ActualizadoEn { get; set; }
        public long? ActualizadoPor { get; set; }
        public string ActualizadoPorNombre { get; set; }

        public string PesoTexto => PesoKg.HasValue ? PesoKg.Value.ToString("F1") + " kg" : "-";
        public string AlturaTexto => AlturaCm.HasValue ? AlturaCm.Value + " cm" : "-";
        public string AptoTexto => AptoFisico ? "Apto" : "No apto";
        public string FechaAptoTexto => FechaApto.HasValue ? FechaApto.Value.ToString("dd/MM/yyyy") : "-";
    }
}
