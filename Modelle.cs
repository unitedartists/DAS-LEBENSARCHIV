using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DAS_LEBENSARCHIV
{
    // ============================================================
    // ARCHITEKTUR-BASISKLASSE
    // ============================================================
    public abstract class ArchivObjekt
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ModifiedAt { get; set; } = DateTime.Now;
    }

    public class Ereignis : ArchivObjekt
    {
        public string Titel { get; set; }
        public string Beschreibung { get; set; }
        public string Datum { get; set; }
        public string Ort { get; set; }

        public string Jahreszeit { get; set; }
        public List<string> Stichwoerter { get; set; } = new List<string>();
        public string Bemerkungen { get; set; }

        public string EreignisFotoDateiname { get; set; }

        public List<string> WeitereFotoDateinamen { get; set; } = new List<string>();

        public List<string> Beteiligte { get; set; } = new List<string>();

        public List<string> Bewertungen { get; set; } = new List<string>();

        public override string ToString()
        {
            if (!string.IsNullOrWhiteSpace(Datum))
            {
                return Titel + " (" + Datum + ")";
            }

            int anzahlErinnerungen = (string.IsNullOrEmpty(EreignisFotoDateiname) ? 0 : 1)
                + (WeitereFotoDateinamen != null ? WeitereFotoDateinamen.Count : 0);

            if (anzahlErinnerungen > 0)
            {
                return Titel + " (" + anzahlErinnerungen + (anzahlErinnerungen == 1 ? " Erinnerung)" : " Erinnerungen)");
            }

            return Titel + " (angelegt " + CreatedAt.ToString("dd.MM.yyyy HH:mm") + ")";
        }
    }

    public class Sammlung : ArchivObjekt
    {
        public string Titel { get; set; }

        public string SammlungFotoDateiname { get; set; }

        public List<string> WeitereFotoDateinamen { get; set; } = new List<string>();

        public override string ToString()
        {
            int anzahlErinnerungen = (string.IsNullOrEmpty(SammlungFotoDateiname) ? 0 : 1)
                + (WeitereFotoDateinamen != null ? WeitereFotoDateinamen.Count : 0);

            if (anzahlErinnerungen > 0)
            {
                return Titel + " (" + anzahlErinnerungen + (anzahlErinnerungen == 1 ? " Erinnerung)" : " Erinnerungen)");
            }

            return Titel + " (angelegt " + CreatedAt.ToString("dd.MM.yyyy HH:mm") + ")";
        }
    }

    public class Beziehung
    {
        public string Rolle { get; set; }
        public string EigeneBezeichnung { get; set; }

        public override string ToString()
        {
            if (Rolle == "Sonstige" && !string.IsNullOrWhiteSpace(EigeneBezeichnung))
            {
                return EigeneBezeichnung;
            }

            return Rolle;
        }
    }

    public class Person : ArchivObjekt
    {
        public string Vorname { get; set; }
        public string Nachname { get; set; }
        public string Geburt { get; set; }
        public string Ort { get; set; }

        public Beziehung Beziehung { get; set; }

        public List<string> ErinnerungsDateinamen { get; set; } = new List<string>();

        public string TitelbildDateiname { get; set; }

        public List<Ereignis> Ereignisse { get; set; } = new List<Ereignis>();

        public override string ToString()
        {
            return (Vorname + " " + Nachname).Trim();
        }
    }

    public class AsservatenEintrag : ArchivObjekt
    {
        public string Dateiname { get; set; }
        public string UrspruenglicherPfad { get; set; }
        public string AsservatenPfad { get; set; }
        public string Dateityp { get; set; }
        public string Hashwert { get; set; }
        public string Grund { get; set; }

        public override string ToString()
        {
            return Dateiname + " (" + Grund + ")";
        }
    }

    public class ArchivDaten
    {
        public List<Person> Personen { get; set; }
        public List<Person> Papierkorb { get; set; }
        public List<Person> Archiv { get; set; }

        public List<Ereignis> FreieEreignisse { get; set; }
        public List<Ereignis> FreieEreignisseArchiv { get; set; }

        public List<Ereignis> FreieEreignissePapierkorb { get; set; }

        public List<Sammlung> Sammlungen { get; set; }
        public List<Sammlung> SammlungenArchiv { get; set; }
        public List<Sammlung> SammlungenPapierkorb { get; set; }

        public List<AsservatenEintrag> Asservatenkammer { get; set; }

        public List<ErinnerungsGedaechtnisEintrag> ErinnerungsGedaechtnis { get; set; }

        public List<WissensBeziehung> WissensBeziehungen { get; set; }
    }

    public class EreignisEintrag
    {
        public Ereignis Ereignis { get; set; }
        public Person Person { get; set; }
        public bool IstArchiviert { get; set; }

        public override string ToString()
        {
            if (Person != null)
            {
                return Ereignis.Titel + " (" + Person.ToString() + ")";
            }

            return Ereignis.Titel;
        }
    }

    public class VisuellesMerkmal
    {
        public string Bezeichnung { get; set; }
        public string Kategorie { get; set; }
        public string Quelle { get; set; } = "Benutzer";
        public int Sicherheit { get; set; } = 100;
        public bool Bestaetigt { get; set; } = true;
    }

    public class WissensBeziehung
    {
        public string Von { get; set; }
        public string Beziehungsart { get; set; }
        public string Zu { get; set; }
    }

    public class ErinnerungsGedaechtnisEintrag
    {
        public string Dateiname { get; set; }
        public List<VisuellesMerkmal> VisuelleMerkmale { get; set; } = new List<VisuellesMerkmal>();
    }

    public class Suchtreffer
    {
        public Ereignis Ereignis { get; set; }
        public Person Person { get; set; }
        public bool IstArchiviert { get; set; }

        public override string ToString()
        {
            return Ereignis.Titel + " (" + Person.ToString() + ")";
        }
    }

    public class Vorschlag
    {
        public string Text { get; set; }
        public Person Person { get; set; }
        public Ereignis Ereignis { get; set; }
        public int? ZielTabIndex { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }

    public class Verknuepfung
    {
        public string Grund { get; set; }
        public int Prioritaet { get; set; }
        public Person Person { get; set; }
        public Ereignis Ereignis { get; set; }
        public bool IstArchiviert { get; set; }

        public override string ToString()
        {
            return Grund + ": " + Ereignis.Titel + " (" + Person.ToString() + ")";
        }
    }

    public class GefundeneDatei : ArchivObjekt
    {
        public string Dateiname { get; set; }
        public string VollstaendigerPfad { get; set; }
        public long GroesseInBytes { get; set; }
        public DateTime Geaendert { get; set; }
        public string Dateityp { get; set; }
        public string Hashwert { get; set; }
        public string FundortRolle { get; set; }
    }

    public class ErinnerungsVerzeichnis
    {
        public DateTime ErstelltAm { get; set; }
        public List<GefundeneDatei> Dateien { get; set; }
    }

    public class DoppelgaengerGruppe
    {
        public string Hashwert { get; set; }
        public List<GefundeneDatei> Dateien { get; set; } = new List<GefundeneDatei>();

        public override string ToString()
        {
            string ersterName = Dateien.Count > 0 ? Dateien[0].Dateiname : "";
            return Dateien.Count + " Dateien - " + ersterName;
        }
    }

    public class OrdnerKnoten
    {
        public string Name { get; set; }
        public string VollstaendigerPfad { get; set; }
        public bool IsChecked { get; set; }
        public bool KinderGeladen { get; set; } = false;
        public ObservableCollection<OrdnerKnoten> Kinder { get; set; } = new ObservableCollection<OrdnerKnoten>();
    }

    public class OrdnerErinnerung
    {
        public string Pfad { get; set; }
        public DateTime LetzterScan { get; set; }
        public int AnzahlDateien { get; set; }
        public string Pruefinfo { get; set; }
    }

    public class Ordnergedaechtnis
    {
        public List<OrdnerErinnerung> Ordner { get; set; } = new List<OrdnerErinnerung>();
    }

    public class Arbeitsstand
    {
        public Guid? PersonId { get; set; }
        public Guid? EreignisId { get; set; }
        public string Arbeitsbereich { get; set; }
        public DateTime Zeitpunkt { get; set; }
    }

    public class Einstellungen
    {
        public string Anrede { get; set; }
        public string Arbeitsweise { get; set; }
        public string Schwerpunkt { get; set; }
        public string HinweisHaeufigkeit { get; set; }
        public string Schrittgroesse { get; set; }
    }

    public class ArchivStandortKonfiguration
    {
        public string ArchivPfad { get; set; }
        public string AlterPfadZumLoeschen { get; set; }
        public string PasswortTresorPfad { get; set; }
    }

    // ============================================================
    // SPRINT C, ETAPPE 1 (04.08.) / ETAPPE 1b-BAUKASTEN (05.08.):
    // SEHGEDÄCHTNIS
    // ============================================================
    // Architekturentscheidung A (05.08.): "1 Bild = viele kleine
    // beschreibende Bausteine" statt "1 Bild = 1 Schublade". Ein Bild
    // kann 0, 1 oder beliebig viele bestätigte Stichwörter besitzen.
    // James' Vermutungen (CLIP-Ähnlichkeit) und Opas bestätigtes Wissen
    // bleiben zwei getrennte Ebenen - das bestätigte Wissen hat Vorrang.
    public class SehgedaechtnisEintrag
    {
        public string Hashwert { get; set; }
        public float[] BildEinbettung { get; set; }
        public DateTime AnalysiertAm { get; set; }
        public string Modellversion { get; set; }

        // Baukasten-Modell (05.08.): James' aktuelle Vermutungen, je
        // Stichwort eine eigene Sicherheit - ersetzt die frühere
        // Einzelvermutung.
        public List<VermuteterBegriff> JamesVermutungen { get; set; } = new List<VermuteterBegriff>();

        // Baukasten-Modell (05.08.): vom Benutzer bestätigte Stichwörter -
        // beliebig viele, unabhängig voneinander. Hat Vorrang vor James'
        // Vermutungen.
        public List<string> BestaetigteStichwoerter { get; set; } = new List<string>();

        // Vorbereitet für spätere Erweiterung (A's Punkt 7): ausdrücklich
        // vom Benutzer verneinte Stichwörter ("keine Katze"). Datenstruktur
        // bereits vorhanden, Bedienoberfläche dafür bewusst noch nicht
        // gebaut.
        public List<string> BestaetigtNichtVorhanden { get; set; } = new List<string>();

        // --- Alte Felder aus Etappe 1b vor dem Baukasten-Umbau ---
        // Bleiben bewusst hier stehen, damit ein bereits vorhandenes
        // sehgedaechtnis.json beim Laden automatisch (MigriereSehgedaechtnis
        // in MainWindow.Sehzentrum.cs) ins neue Listenmodell übernommen
        // werden kann. Werden danach geleert und nicht mehr neu befüllt.
        public string JamesVermutungKategorie { get; set; }
        public int JamesVermutungSicherheit { get; set; }
        public string BestaetigteKategorie { get; set; }
    }

    // Eine einzelne Vermutung von James zu einem Stichwort, mit Sicherheit
    // in Prozent (Kosinus-Ähnlichkeit gegen den Referenz-Durchschnitt).
    public class VermuteterBegriff
    {
        public string Begriff { get; set; }
        public int SicherheitProzent { get; set; }
    }

    // ============================================================
    // SPRINT C, ETAPPE 1b (05.08.): STICHWORT-REFERENZEN
    // ============================================================
    // Ein Eintrag pro Stichwort (z.B. "Hund"), enthält die Einbettungen
    // aller bisher vom Benutzer bestätigten Beispielbilder für dieses
    // Stichwort. James vergleicht neue Bilder mit dem Durchschnitt dieser
    // Beispiele. Je mehr bestätigte Beispiele, desto treffsicherer wird
    // die Vermutung mit der Zeit. (Klassenname bewusst unverändert
    // gelassen, damit ein bereits vorhandenes kategorien.json ohne
    // Migration weiterverwendet werden kann - "Kategorie" entspricht
    // inhaltlich jetzt einem "Stichwort" im Baukastensinn.)
    public class KategorieReferenz
    {
        public string Kategorie { get; set; }
        public List<float[]> BestaetigteEinbettungen { get; set; } = new List<float[]>();
    }
}
