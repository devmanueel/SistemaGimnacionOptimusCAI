using System.Collections.Generic;

namespace Entities
{
    public class ResultadoPaginado<T>
    {
        public List<T> Items     { get; set; } = new List<T>();
        public int     Total     { get; set; }
        public bool    HayMas    { get; set; }
        public int     Pagina    { get; set; }
        public int     TamPagina { get; set; }
    }
}
