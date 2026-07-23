using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DAS_LEBENSARCHIV
{
    // ============================================================
    // ARCHITEKTUR-BASISKLASSE
    // ============================================================
    // Gemeinsame technische Grundlage für alle künftigen Archivobjekte
    // (Person, Ereignis, später auch Dokument, Erinnerung, ...).
    //
    // - Id:        unveränderliche technische Identität (wird einmalig
    //              beim Anlegen erzeugt, dem Benutzer niemals angezeigt,
    //              ändert sich niemals - auch nicht bei Namensänderungen).
    // - CreatedAt: Zeitpunkt der Erstellung.
    // - ModifiedAt: Zeitpunkt der letzten Änderung (für spätere
    //              Auswertungen, z.B. "seit Ihrem letzten Besuch neu").
    // ============================================================
    public abstract class ArchivObjekt
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime ModifiedAt { get; set; } = DateTime.Now;
    }

    // ============================================================
    // ARCHITEKTURENTSCHEIDUNG 003 (Build 0.5)
    // ============================================================
    // Ereignisse sind die Bausteine der Biografie.
    // Erinnerungen gehören (künftig) zu Ereignissen.
    // Medien gehören (künftig) zu Erinnerungen.
    // Personen erleben Ereignisse.
    //
    // Architekturregel (lose Kopplung):
    // Keine Klasse darf jemals wissen, wo sie später angezeigt wird.
    // Ereignis weiß nichts von der Bedienoberfläche, nichts vom
    // Schreibtisch, nichts vom Lebensbuch. Ereignis kennt nur sich
    // selbst. Nur MainWindow (stellvertretend für "James") kennt alle
    // Objekte und verbindet sie mit der Anzeige.
    //
    // Build 0.5 legte das Fundament: eine Person kann beliebig viele
    // Ereignisse besitzen. Build 0.6 ergaenzt: ein Ereignis kann nun
    // zusaetzlich ein eigenes Foto besitzen (das bestehende Titelbild
    // der Person bleibt davon unberuehrt bestehen). Build 0.7 ergaenzt:
    // ein Ereignis bekommt beschreibende Eigenschaften (Jahreszeit,
    // Stichwoerter, Bemerkungen), die spaeter eine natuerliche Suche
    // ermoeglichen sollen (z.B. "Zeige mir alle Gartenbilder aus dem
    // Fruehjahr 1998."). Architekturkorrektur des Architekten: eine
    // eigenstaendige Erinnerung-Klasse zwischen Ereignis und Medium
    // wird erst eingefuehrt, wenn ein Ereignis tatsaechlich mehrere
    // unterschiedliche Medien (Fotos, Dokumente, Audio, Video) enthalten
    // soll - bis dahin bleiben die beschreibenden Felder direkt am
    // Ereignis, das haelt die Architektur so schlank wie noetig.
    // ============================================================
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

    // ============================================================
    // BUILD 1.2: BEZIEHUNGEN VERSTEHEN
    // ============================================================
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

    public class ArchivDaten
    {
        public List<Person> Personen { get; set; }
        public List<Person> Papierkorb { get; set; }
        public List<Person> Archiv { get; set; }

        public List<Ereignis> FreieEreignisse { get; set; }
        public List<Ereignis> FreieEreignisseArchiv { get; set; }

        public List<Ereignis> FreieEreignissePapierkorb { get; set; }

        public List<ErinnerungsGedaechtnisEintrag> ErinnerungsGedaechtnis { get; set; }

        public List<WissensBeziehung> WissensBeziehungen { get; set; }
    }

    // ============================================================
    // ETAPPE A: DIE EINHEITLICHE EREIGNISLISTE (Vorbereitung)
    // ============================================================
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

    // ============================================================
    // ETAPPE B (Build 2.9): JAMES' VISUELLES GEDÄCHTNIS
    // ============================================================
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

    // ============================================================
    // BUILD 0.8: JAMES FINDET ERINNERUNGEN
    // ============================================================
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

    // ============================================================
    // BUILD 1.7: JAMES MACHT VORSCHLÄGE
    // ============================================================
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

    // ============================================================
    // BUILD 1.8: ERINNERUNGEN VERKNÜPFEN
    // ============================================================
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

    // ============================================================
    // BUILD 0.4: ERINNERUNGSVERZEICHNIS
    // ============================================================
    public class GefundeneDatei : ArchivObjekt
    {
        public string Dateiname { get; set; }
        public string VollstaendigerPfad { get; set; }
        public long GroesseInBytes { get; set; }
        public DateTime Geaendert { get; set; }
        public string Dateityp { get; set; }
        public string Hashwert { get; set; }
    }

    public class ErinnerungsVerzeichnis
    {
        public DateTime ErstelltAm { get; set; }
        public List<GefundeneDatei> Dateien { get; set; }
    }

    // ============================================================
    // BUILD 1.0: ERINNERUNGEN AUFRÄUMEN
    // ============================================================
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

    // ============================================================
    // BUILD 1.1: BAUMFÖRMIGE ORDNERAUSWAHL
    // ============================================================
    public class OrdnerKnoten
    {
        public string Name { get; set; }
        public string VollstaendigerPfad { get; set; }
        public bool IsChecked { get; set; }
        public bool KinderGeladen { get; set; } = false;
        public ObservableCollection<OrdnerKnoten> Kinder { get; set; } = new ObservableCollection<OrdnerKnoten>();
    }

    // ============================================================
    // BUILD 1.1: ORDNERGEDÄCHTNIS
    // ============================================================
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

    // ============================================================
    // BUILD 1.9: JAMES MERKT SICH DIE ARBEIT
    // ============================================================
    public class Arbeitsstand
    {
        public Guid? PersonId { get; set; }
        public Guid? EreignisId { get; set; }
        public string Arbeitsbereich { get; set; }
        public DateTime Zeitpunkt { get; set; }
    }

    // ============================================================
    // BUILD 2.0: JAMES LERNT SEINEN BESITZER KENNEN
    // ============================================================
    public class Einstellungen
    {
        public string Anrede { get; set; }
        public string Arbeitsweise { get; set; }
        public string Schwerpunkt { get; set; }
        public string HinweisHaeufigkeit { get; set; }
        public string Schrittgroesse { get; set; }
    }
}