using System;
using System.Collections.Generic;
using System.IO;

namespace DAS_LEBENSARCHIV
{
    // ============================================================
    // SANIERUNG DES ARBEITSMODELLS (08.08.), BAUPHASE 1:
    // NEUES DATENMODELL - ERINNERUNG / FUNDORT / ZUORDNUNG
    // ============================================================
    // Baut auf Claudes Gesamtanalyse + Soll-Bauplan auf (mit A/Opa
    // abgestimmt, 08.08.). WICHTIG: dieses Modell existiert bewusst
    // NUR PARALLEL zum bestehenden Datenmodell (Person/Ereignis/
    // Sammlung mit ihren Foto-Dateiname-Feldern in Modelle.cs) - es
    // ersetzt oder verändert bislang NICHTS Bestehendes. Keine
    // Migration, keine Löschung alter Strukturen, keine
    // Deduplizierung - das sind spätere, eigens freizugebende
    // Bauphasen. ArchivObjekt (Id/CreatedAt/ModifiedAt) wird aus
    // Modelle.cs wiederverwendet, sonst nichts von dort berührt.
    //
    // Grundprinzip (A's Regel, 08.08.): "Zuordnung bedeutet künftig
    // Erinnerung <-> Ziel, nicht Datei kopieren <-> Zielordner."
    // Eine Zuordnung erzeugt deshalb NIEMALS eine physische Kopie -
    // sie ist ein reiner Datensatz-Verweis. Das setzt sich konsequent
    // im Testfenster (SanierungTestFenster) fort: an keiner Stelle
    // im gesamten Sanierungscode wird File.Copy aufgerufen.

    // ARBEITSMOTOR (09.08.): Erinnerungen sind nicht zwingend Bilder.
    // Bewusst einfach gehalten - nur zur Unterscheidung in Suche/
    // Anzeige, keine typspezifische Sonderlogik (OCR, Videoanalyse
    // usw.) in dieser Bauphase.
    public enum MedienTyp
    {
        Bild,
        Pdf,
        Dokument,
        Video
    }

    public enum ZuordnungsZielTyp
    {
        Person,
        Ereignis,
        Sammlung
    }

    // A/Opa-BAUAUFTRAG "JAMES-EINZUG" (12.08.), Punkt 9: eine zentrale
    // Sortierlogik statt der bisherigen einfachen Ja/Nein-Sortierung
    // (chronologisch/alphabetisch) - deckt jetzt alle 4 von A geforderten
    // Modi ab, wird von ZentraleErinnerungsSuche (MainWindow.
    // ErinnerungsmodellZustand.cs) verwendet, dort auch die einzige Stelle,
    // die tatsaechlich sortiert (AM/Arbeitsmotor/James-Suche geben nur noch
    // an, WELCHER Modus gewuenscht ist).
    public enum SortierModus
    {
        DatumNeuesteZuerst,
        DatumAeltesteZuerst,
        AlphabetischAufsteigend,
        AlphabetischAbsteigend
    }

    // Die Erinnerung selbst - ihre Identität ist ausschließlich die
    // Id (geerbt von ArchivObjekt). Der Hashwert ist bewusst NUR ein
    // technisches Duplikat-Erkennungsmerkmal, keine Identität (A's
    // ausdrücklicher Einwand vom 08.08.) - zwei Dateien mit demselben
    // Hash werden dadurch NICHT automatisch zu derselben Erinnerung
    // zusammengeführt; das bliebe eine spätere, eigens zu
    // entscheidende Deduplizierung. Ausnahme (09.08.): beim
    // TESTIMPORT wird der Hash genutzt, um zu erkennen, ob genau
    // dieselbe Datei bereits importiert wurde - das ist ein reiner
    // Import-Schutz, keine Zusammenführung bestehender Erinnerungen.
    public class Erinnerung : ArchivObjekt
    {
        public string Hashwert { get; set; }
        public List<Fundort> Fundorte { get; set; } = new List<Fundort>();

        // ARBEITSMOTOR (09.08.), additiv, bricht nichts Bestehendes:
        // Medientyp Default = Bild (alle bisher migrierten 288
        // Erinnerungen sind Bilder). Erstellungsdatum wird beim
        // Testimport aus dem Dateidatum übernommen - ermöglicht die
        // von A verlangte Sortierung nach Datum, ohne Originaldaten
        // zu verändern.
        public MedienTyp MedienTyp { get; set; } = MedienTyp.Bild;
        public DateTime? Erstellungsdatum { get; set; }

        // A/Opa-REPARATURAUFTRAG (11.08.), PROBLEM 1: echter Import-
        // Zeitstempel, getrennt vom Erstellungsdatum der Originaldatei
        // (das beim Import aus deren Aenderungsdatum uebernommen wird und
        // deshalb NICHT den tatsaechlichen Importzeitpunkt widerspiegelt -
        // ein altes Foto blieb dadurch bei "neueste zuerst" faelschlich
        // unten). Nullable, damit bereits bestehende Erinnerungen ohne
        // dieses Feld weiterhin klaglos funktionieren (Fallback auf
        // Erstellungsdatum/CreatedAt, siehe ZentraleErinnerungsSuche in
        // MainWindow.ErinnerungsmodellZustand.cs).
        public DateTime? ImportiertAm { get; set; }

        public override string ToString()
        {
            string ersterFundort = Fundorte.Count > 0 ? Path.GetFileName(Fundorte[0].Pfad) : "(kein Fundort)";
            return ersterFundort + " (" + Fundorte.Count + " Fundort(e))";
        }
    }

    // Ein physischer Ort, an dem dieselbe Erinnerung tatsächlich als
    // Datei existiert (z.B. Original auf D:, Sicherung auf dem
    // Seagate-Laufwerk). Mehrere Fundorte sind ausdrücklich erlaubt
    // und kein Fehler - genau das war im alten Modell nicht möglich.
    public class Fundort
    {
        public string Pfad { get; set; }
        public DateTime Gefunden { get; set; } = DateTime.Now;
        public string FundortRolle { get; set; }
    }

    // Eine Zuordnung verbindet eine Erinnerung mit GENAU einem Ziel
    // (Person/Ereignis/Sammlung). Mehrere Zuordnungen derselben
    // Erinnerung zu unterschiedlichen oder auch gleichartigen Zielen
    // sind das eigentliche Kernprinzip dieser Sanierung. Das Entfernen
    // EINER Zuordnung berührt weder die Erinnerung noch ihre anderen
    // Zuordnungen noch die physische Datei (Papierkorb-Kontext-Regel,
    // 07.08. mit Opa beschlossen).
    //
    // TÜV-Sanierung, Anschlussplan (08.08.), von A/Opa freigegebene
    // Empfehlung: ZielId (Guid) verweist auf die bereits bestehende Id
    // der Person/des Ereignisses/der Sammlung - Namen können sich
    // ändern, eine Id nicht. ZielBezeichnung bleibt als optionales,
    // menschenlesbares Label bestehen (z.B. für Berichte), ist aber
    // NICHT die maßgebliche Verknüpfung.
    public class Zuordnung : ArchivObjekt
    {
        public Guid ErinnerungId { get; set; }
        public ZuordnungsZielTyp ZielTyp { get; set; }
        public Guid ZielId { get; set; }
        public string ZielBezeichnung { get; set; }

        public override string ToString()
        {
            return ZielTyp + ": " + (string.IsNullOrEmpty(ZielBezeichnung) ? ZielId.ToString() : ZielBezeichnung);
        }
    }

    // ============================================================
    // SANIERUNG BAUPHASE 3 (08.08.) + INTEGRATION (10.08.): SPEICHER-CONTAINER
    // ============================================================
    // Reine Serialisierungshülle für eine EIGENE, NEUE Datei
    // (erinnerungsmodell.json) - getrennt von personen.json, die
    // dabei an keiner Stelle beschrieben wird.
    //
    // A/Opa-Auftrag "Integrations- und Sanierungsauftrag" (10.08.),
    // Punkt 12+13: ZuordnungenPapierkorb ergänzt den bisherigen
    // Container additiv - eine hierhin verschobene Zuordnung betrifft
    // NIE die Erinnerung selbst oder ihre Fundorte, nur den
    // Zuordnungs-Datensatz.
    public class ArchivErinnerungsDaten
    {
        public List<Erinnerung> Erinnerungen { get; set; } = new List<Erinnerung>();
        public List<Zuordnung> Zuordnungen { get; set; } = new List<Zuordnung>();
        public List<Zuordnung> ZuordnungenPapierkorb { get; set; } = new List<Zuordnung>();
    }
}
