using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DAS_LEBENSARCHIV
{
    // ============================================================
    // NEUE FUNKTION (Generaltest 2, Wunsch von Oma+Opa): FOTO DREHEN
    // ============================================================
    // Dreht eine Bilddatei tatsächlich dauerhaft und verlustfrei -
    // ausschließlich mit WPF-Bordmitteln (keine zusätzliche Bibliothek
    // nötig). Wird sowohl beim "Erinnerungen ansehen"-Fenster als auch
    // in der Arbeitsmappe verwendet, damit ein einmal gedrehtes Bild
    // überall im Programm richtig herum erscheint.
    // ============================================================
    public static class Bilddrehung
    {
        public static bool DreheUndSpeichere(string pfad, int gradImUhrzeigersinn)
        {
            try
            {
                byte[] originalBytes = File.ReadAllBytes(pfad);

                BitmapImage quelle = new BitmapImage();

                using (MemoryStream eingabeStream = new MemoryStream(originalBytes))
                {
                    quelle.BeginInit();
                    quelle.CacheOption = BitmapCacheOption.OnLoad;
                    quelle.StreamSource = eingabeStream;
                    quelle.EndInit();
                }

                TransformedBitmap gedreht = new TransformedBitmap(quelle, new RotateTransform(gradImUhrzeigersinn));

                BitmapEncoder encoder = ErmittleEncoder(pfad);

                if (encoder == null)
                {
                    return false;
                }

                encoder.Frames.Add(BitmapFrame.Create(gedreht));

                string tempPfad = pfad + ".drehung_temp";

                using (FileStream ausgabeStream = new FileStream(tempPfad, FileMode.Create))
                {
                    encoder.Save(ausgabeStream);
                }

                File.Delete(pfad);
                File.Move(tempPfad, pfad);

                return true;
            }
            catch
            {
                return false;
            }
        }

        // Nur Formate, für die WPF einen passenden Encoder mitbringt.
        // Andere Formate (z.B. HEIC, WEBP) werden bewusst nicht
        // gedreht - dafür wäre eine zusätzliche Bibliothek nötig.
        private static BitmapEncoder ErmittleEncoder(string pfad)
        {
            string endung = Path.GetExtension(pfad).ToLowerInvariant();

            switch (endung)
            {
                case ".png": return new PngBitmapEncoder();
                case ".bmp": return new BmpBitmapEncoder();
                case ".gif": return new GifBitmapEncoder();
                case ".tif":
                case ".tiff": return new TiffBitmapEncoder();
                case ".jpg":
                case ".jpeg": return new JpegBitmapEncoder();
                default: return null;
            }
        }
    }
}
