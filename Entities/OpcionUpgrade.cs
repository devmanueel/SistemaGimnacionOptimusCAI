using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Entities
{
    public class OpcionUpgrade
    {
        public long ActividadId { get; set; }
        public string ActividadNombre { get; set; }
        public decimal PrecioNuevo { get; set; }
        public decimal PrecioActual { get; set; }
        public decimal DiferenciaAPagar { get; set; }
        public int NivelNuevo { get; set; }
        public int NivelActual { get; set; }

        // Para mostrar en el ComboBox
        public string Display => ActividadNombre + "  (+$" + DiferenciaAPagar.ToString("N0") + ")";
    }
}
