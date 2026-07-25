using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow : Window
    {
        // ============================================================
        // BUILD 2.9 / ETAPPE B: VISUELLE MERKMALE (Erinnerungsgedächtnis)
        // ============================================================

        private List<VisuellesMerkmal> LiesVisuelleMerkmale(string dateiname)
        {
            ErinnerungsGedaechtnisEintrag eintrag = erinnerungsGedaechtnis
                .FirstOrDefault(e => e.Dateiname == dateiname);

            if (eintrag == null)
            {
                return new List<VisuellesMerkmal>();
            }

            return eintrag.VisuelleMerkmale
                .Select(m => new VisuellesMerkmal
                {
                    Bezeichnung = m.Bezeichnung,
                    Kategorie = m.Kategorie,
                    Quelle = m.Quelle,
                    Sicherheit = m.Sicherheit,
                    Bestaetigt = m.Bestaetigt
                })
                .ToList();
        }

        private void SpeichereVisuelleMerkmale(string dateiname, List<VisuellesMerkmal> merkmale)
        {
            ErinnerungsGedaechtnisEintrag eintrag = erinnerungsGedaechtnis
                .FirstOrDefault(e => e.Dateiname == dateiname);

            if (eintrag == null)
            {
                eintrag = new ErinnerungsGedaechtnisEintrag { Dateiname = dateiname };
                erinnerungsGedaechtnis.Add(eintrag);
            }

            eintrag.VisuelleMerkmale = merkmale;

            SpeichereDaten();
        }

        private int ZaehleVorkommenVisuellesMerkmal(string bezeichnung, string kategorie, string aktuellerDateiname)
        {
            return MerkmalAuswertung.ZaehleVorkommen(erinnerungsGedaechtnis, bezeichnung, kategorie, aktuellerDateiname);
        }
    }
}
