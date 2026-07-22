using System;
using System.Collections.Generic;
using System.Linq;

namespace DAS_LEBENSARCHIV
{
    // ============================================================
    // BUILD 3.1: JAMES' WISSENSBIBLIOTHEK (Etappe B.3)
    // ============================================================
    // Diese Klasse bildet die Grundlage für alle späteren
    // Wissensauswertungen von James.
    //
    // Aktuell:
    // - Gruppieren bestätigter Merkmale nach Kategorie
    // - Häufigkeiten zählen (Vorkommen desselben Merkmals in anderen
    //   Erinnerungen)
    //
    // Später zusätzlich (noch NICHT Teil dieses Builds):
    // - Beziehungen
    // - Ähnlichkeitssuche
    // - automatische Vorschläge
    // - Bilderkennung
    //
    // Wichtig: Es fließen ausschließlich bestätigte Merkmale
    // (Bestaetigt == true) in irgendeine Auswertung ein - James'
    // Wissen besteht nur aus dem, was der Benutzer selbst bestätigt
    // hat. Keine Vermutungen, keine neuen Begriffe.
    // ============================================================
    public static class MerkmalAuswertung
    {
        // Gruppiert die bestätigten Merkmale einer Erinnerung nach
        // Kategorie. Reihenfolge innerhalb einer Kategorie entspricht
        // der Reihenfolge, in der die Merkmale eingegeben wurden.
        public static List<MerkmalGruppe> GruppiereNachKategorie(List<VisuellesMerkmal> merkmale)
        {
            List<MerkmalGruppe> ergebnis = new List<MerkmalGruppe>();

            if (merkmale == null)
            {
                return ergebnis;
            }

            foreach (VisuellesMerkmal merkmal in merkmale)
            {
                if (!merkmal.Bestaetigt)
                {
                    continue;
                }

                MerkmalGruppe gruppe = ergebnis.FirstOrDefault(g => g.Kategorie == merkmal.Kategorie);

                if (gruppe == null)
                {
                    gruppe = new MerkmalGruppe { Kategorie = merkmal.Kategorie };
                    ergebnis.Add(gruppe);
                }

                gruppe.Bezeichnungen.Add(merkmal.Bezeichnung);
            }

            return ergebnis;
        }

        // Zählt, in wie vielen ANDEREN Erinnerungen (nicht die aktuelle,
        // identifiziert über aktuellerDateiname) dasselbe bestätigte
        // Merkmal (Bezeichnung + Kategorie, ohne Berücksichtigung von
        // Groß-/Kleinschreibung) ebenfalls vorkommt.
        public static int ZaehleVorkommen(
            List<ErinnerungsGedaechtnisEintrag> erinnerungsGedaechtnis,
            string bezeichnung,
            string kategorie,
            string aktuellerDateiname)
        {
            if (erinnerungsGedaechtnis == null)
            {
                return 0;
            }

            int anzahl = 0;

            foreach (ErinnerungsGedaechtnisEintrag eintrag in erinnerungsGedaechtnis)
            {
                if (eintrag == null || eintrag.Dateiname == aktuellerDateiname || eintrag.VisuelleMerkmale == null)
                {
                    continue;
                }

                bool vorhanden = eintrag.VisuelleMerkmale.Any(m =>
                    m.Bestaetigt
                    && string.Equals(m.Bezeichnung, bezeichnung, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(m.Kategorie, kategorie, StringComparison.OrdinalIgnoreCase));

                if (vorhanden)
                {
                    anzahl++;
                }
            }

            return anzahl;
        }
    }

    // Eine Gruppe bestätigter Merkmale derselben Kategorie.
    public class MerkmalGruppe
    {
        public string Kategorie { get; set; }
        public List<string> Bezeichnungen { get; set; } = new List<string>();
    }
}