using Entities;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace SistemaGimnacionOptimusCAI.Helpers
{
    public class CarnetGenerator
    {
        private const int W = 560;
        private const int H = 397;

        public string GenerarCarnet(Socio socio, Membresia membresiaActiva)
        {
            string archivo = Path.Combine(
                Path.GetTempPath(),
                "Carnet_NroSocio_" + socio.NumeroSocio.ToString("D4") + ".png");

            using (var bmp = new Bitmap(W, H))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                DibujarFondo(g);
                DibujarLogo(g);
                DibujarFotoSocio(g, socio.Foto);
                DibujarDatosSocio(g, socio, membresiaActiva);
                DibujarQR(g, socio.Dni);
                DibujarPie(g);

                bmp.Save(archivo, ImageFormat.Png);
            }

            return archivo;
        }

        private void DibujarFondo(Graphics g)
        {
            using (var brush = new LinearGradientBrush(
                new Rectangle(0, 0, W, H),
                Color.FromArgb(10, 18, 10),
                Color.FromArgb(18, 30, 18),
                LinearGradientMode.Vertical))
            {
                g.FillRectangle(brush, 0, 0, W, H);
            }

            using (var pen = new Pen(Color.FromArgb(122, 201, 67), 2))
            {
                g.DrawRectangle(pen, 1, 1, W - 3, H - 3);
            }

            using (var pen = new Pen(Color.FromArgb(40, 122, 201, 67), 1))
            {
                g.DrawLine(pen, 0, 70, W, 70);
                g.DrawLine(pen, 0, H - 40, W, H - 40);
            }
        }

        private void DibujarLogo(Graphics g)
        {
            using (var fontGym = new Font("Segoe UI", 18, FontStyle.Bold))
            using (var fontSub = new Font("Segoe UI", 8, FontStyle.Regular))
            using (var greenBrush = new SolidBrush(Color.FromArgb(122, 201, 67)))
            using (var mutedBrush = new SolidBrush(Color.FromArgb(140, 160, 140)))
            {
                g.DrawString("OPTIMUS", fontGym, greenBrush, 20, 18);
                using (var fontCai = new Font("Segoe UI", 18, FontStyle.Regular))
                {
                    float offsetX = g.MeasureString("OPTIMUS", fontGym).Width + 18;
                    g.DrawString("CAI", fontCai, mutedBrush, offsetX, 18);
                }
                g.DrawString("GIMNASIO & FITNESS", fontSub, mutedBrush, 22, 50);
            }
        }

        private void DibujarFotoSocio(Graphics g, byte[] fotoBytes)
        {
            int cx = 80;
            int cy = 170;
            int radio = 55;

            using (var circlePath = new GraphicsPath())
            {
                circlePath.AddEllipse(cx - radio, cy - radio, radio * 2, radio * 2);

                if (fotoBytes != null && fotoBytes.Length > 0)
                {
                    try
                    {
                        using (var ms = new MemoryStream(fotoBytes))
                        using (var img = Image.FromStream(ms))
                        {
                            var oldClip = g.Clip;
                            g.SetClip(circlePath);
                            g.DrawImage(img, cx - radio, cy - radio, radio * 2, radio * 2);
                            g.SetClip(oldClip, CombineMode.Replace);
                        }
                    }
                    catch
                    {
                        DibujarFotoPlaceholder(g, cx, cy, radio);
                    }
                }
                else
                {
                    DibujarFotoPlaceholder(g, cx, cy, radio);
                }

                using (var pen = new Pen(Color.FromArgb(122, 201, 67), 2))
                {
                    g.DrawEllipse(pen, cx - radio, cy - radio, radio * 2, radio * 2);
                }
            }
        }

        private void DibujarFotoPlaceholder(Graphics g, int cx, int cy, int radio)
        {
            using (var bgBrush = new SolidBrush(Color.FromArgb(30, 50, 30)))
            {
                g.FillEllipse(bgBrush, cx - radio, cy - radio, radio * 2, radio * 2);
            }
            using (var font = new Font("Segoe UI", 28, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.FromArgb(122, 201, 67)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString("?", font, brush, cx, cy, sf);
            }
        }

        private void DibujarDatosSocio(Graphics g, Socio socio, Membresia mem)
        {
            int x = 150;
            int y = 90;

            using (var fontNombre = new Font("Segoe UI", 16, FontStyle.Bold))
            using (var fontDato = new Font("Segoe UI", 10, FontStyle.Regular))
            using (var fontLabel = new Font("Segoe UI", 8, FontStyle.Bold))
            using (var whiteBrush = new SolidBrush(Color.FromArgb(232, 240, 232)))
            using (var greenBrush = new SolidBrush(Color.FromArgb(122, 201, 67)))
            using (var mutedBrush = new SolidBrush(Color.FromArgb(140, 160, 140)))
            {
                string nombre = socio.Nombre + " " + socio.Apellido;
                g.DrawString(nombre, fontNombre, whiteBrush, x, y);
                y += 32;

                g.DrawString("N° SOCIO", fontLabel, mutedBrush, x, y);
                y += 16;
                g.DrawString(socio.NumeroFormateado, fontDato, greenBrush, x, y);
                y += 24;

                g.DrawString("DNI", fontLabel, mutedBrush, x, y);
                y += 16;
                g.DrawString(socio.Dni ?? "-", fontDato, whiteBrush, x, y);
                y += 24;

                if (mem != null)
                {
                    g.DrawString("ACTIVIDAD", fontLabel, mutedBrush, x, y);
                    y += 16;
                    string actPlan = (mem.ActividadNombre ?? "-") + " | " + mem.TipoPlanTexto;
                    g.DrawString(actPlan, fontDato, whiteBrush, x, y);
                    y += 24;

                    g.DrawString("VENCIMIENTO", fontLabel, mutedBrush, x, y);
                    y += 16;
                    g.DrawString(mem.FechaVencimientoTexto, fontDato, greenBrush, x, y);
                }
                else
                {
                    g.DrawString("ACTIVIDAD", fontLabel, mutedBrush, x, y);
                    y += 16;
                    g.DrawString("Sin membresía activa", fontDato, mutedBrush, x, y);
                }
            }
        }

        private void DibujarQR(Graphics g, string dni)
        {
            if (string.IsNullOrEmpty(dni)) return;

            int qrSize = 100;
            int x = W - qrSize - 30;
            int y = 100;

            using (var bgBrush = new SolidBrush(Color.White))
            {
                g.FillRectangle(bgBrush, x - 5, y - 5, qrSize + 10, qrSize + 10);
            }

            GenerarQRSimple(g, dni, x, y, qrSize);

            using (var font = new Font("Segoe UI", 7, FontStyle.Regular))
            using (var brush = new SolidBrush(Color.FromArgb(140, 160, 140)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString("DNI: " + dni, font, brush, x + qrSize / 2, y + qrSize + 10, sf);
            }
        }

        private void GenerarQRSimple(Graphics g, string data, int x, int y, int size)
        {
            int moduleCount = 21;
            float moduleSize = (float)size / moduleCount;
            bool[,] matrix = GenerarMatrizQR(data, moduleCount);

            using (var blackBrush = new SolidBrush(Color.Black))
            {
                for (int row = 0; row < moduleCount; row++)
                {
                    for (int col = 0; col < moduleCount; col++)
                    {
                        if (matrix[row, col])
                        {
                            g.FillRectangle(blackBrush,
                                x + col * moduleSize,
                                y + row * moduleSize,
                                moduleSize + 0.5f,
                                moduleSize + 0.5f);
                        }
                    }
                }
            }
        }

        private bool[,] GenerarMatrizQR(string data, int size)
        {
            bool[,] m = new bool[size, size];

            DibujarFinderPattern(m, 0, 0);
            DibujarFinderPattern(m, 0, size - 7);
            DibujarFinderPattern(m, size - 7, 0);

            int hash = 0;
            foreach (char c in data)
            {
                hash = hash * 31 + c;
            }

            Random rng = new Random(hash);
            for (int r = 0; r < size; r++)
            {
                for (int c = 0; c < size; c++)
                {
                    if (EstaEnFinderPattern(r, c, size)) continue;
                    m[r, c] = rng.Next(2) == 1;
                }
            }

            return m;
        }

        private void DibujarFinderPattern(bool[,] m, int startR, int startC)
        {
            for (int r = 0; r < 7; r++)
            {
                for (int c = 0; c < 7; c++)
                {
                    bool border = r == 0 || r == 6 || c == 0 || c == 6;
                    bool inner = r >= 2 && r <= 4 && c >= 2 && c <= 4;
                    m[startR + r, startC + c] = border || inner;
                }
            }
        }

        private bool EstaEnFinderPattern(int r, int c, int size)
        {
            if (r < 8 && c < 8) return true;
            if (r < 8 && c >= size - 8) return true;
            if (r >= size - 8 && c < 8) return true;
            return false;
        }

        private void DibujarPie(Graphics g)
        {
            using (var font = new Font("Segoe UI", 7, FontStyle.Regular))
            using (var brush = new SolidBrush(Color.FromArgb(100, 120, 100)))
            {
                var sf = new StringFormat { Alignment = StringAlignment.Center };
                g.DrawString("OptimusCAI Gym — Carnet de Socio — Presentar en recepción",
                    font, brush, W / 2f, H - 30, sf);
            }
        }
    }
}
