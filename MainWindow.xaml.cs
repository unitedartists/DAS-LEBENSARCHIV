using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using System.Security.Cryptography;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow : Window
    {
        private Person aktuellBearbeitetePerson = null;

        // Build 5.1: analog zu aktuellBearbeitetePerson, für die neue
        // Ereignisverwaltung auf dem Schreibtisch (dieselbe Bedienlogik
        // wie bei der Personenverwaltung).
        private Ereignis aktuellBearbeitetesFreiesEreignis = null;

        // Neue Funktion (Generaltest 2, Wunsch von Oma+Opa): dritte
        // Schublade "Sammlung" - analog zu aktuellBearbeitetesFreiesEreignis.
        private Sammlung aktuellBearbeiteteSammlung = null;
        private DispatcherTimer statusTimer;

        // Hauptliste aller Personen (unabhängig davon, was gerade angezeigt/gefiltert wird)
        private List<Person> allePersonen = new List<Person>();

        // Build 5.0: freie, personenunabhängige Ereignisse - die
        // "persönliche Ereignisliste" aus dem Architekturauftrag.
        private List<Ereignis> freieEreignisse = new List<Ereignis>();
        private List<Ereignis> freieEreignisseArchiv = new List<Ereignis>();

        // Etappe A.5, Punkt 2: Papierkorb für freie Ereignisse.
        private List<Ereignis> freieEreignissePapierkorb = new List<Ereignis>();

        // Neue Funktion (Generaltest 2): Sammlungen - dritte Schublade
        // neben Personen und besonderen Ereignissen, analog aufgebaut.
        private List<Sammlung> sammlungen = new List<Sammlung>();
        private List<Sammlung> sammlungenArchiv = new List<Sammlung>();
        private List<Sammlung> sammlungenPapierkorb = new List<Sammlung>();

        // Neue Funktion (Generaltest 2): Asservatenkammer - hierhin
        // verschiebt James automatisch erkannte, exakte Duplikate.
        private List<AsservatenEintrag> asservatenkammer = new List<AsservatenEintrag>();

        // Etappe B (Build 2.9): visuelles Gedächtnis.
        private List<ErinnerungsGedaechtnisEintrag> erinnerungsGedaechtnis = new List<ErinnerungsGedaechtnisEintrag>();

        // Build 3.0: Vorbereitung für spätere Builds - noch ungenutzt.
        private List<WissensBeziehung> wissensBeziehungen = new List<WissensBeziehung>();

        // Etappe A: einheitliche Ereignisliste - baut sich ausschließlich aus
        // den bestehenden Strukturen oben auf (rein additiv, siehe
        // AktualisiereEinheitlicheEreignisliste). Wird aktuell von keiner
        // Oberfläche gelesen oder angezeigt - dient nur als vorbereitete,
        // geprüfte Grundlage für spätere Etappen.
        private List<EreignisEintrag> alleEreignisse = new List<EreignisEintrag>();

        // Wurzelknoten des Ordner-Auswahlbaums (Build 1.1) - wird an
        // OrdnerBaumTreeView.ItemsSource gebunden.
        private ObservableCollection<OrdnerKnoten> ordnerBaumWurzelKnoten = new ObservableCollection<OrdnerKnoten>();

        // ============================================================
        // ARCHITEKTUR: ZENTRALER ARCHIV-SPEICHERORT (31.07., gemeinsam mit
        // dem Architekten festgelegt). Der Benutzer wählt EINEN obersten
        // Archivordner (z.B. H:\Lebensarchiv). James verwaltet darunter
        // selbständig seine Struktur (Erinnerungen, Asservatenkammer usw.).
        // In %APPDATA% bleibt künftig nur noch ein winziger Zeiger auf
        // diesen Ordner - keine Bild-/Videodaten mehr dort.
        //
        // Rückwärtskompatibel: Solange noch kein eigener Speicherort
        // gewählt wurde (kein Zeiger vorhanden), bleibt alles wie bisher
        // unter %APPDATA%\LEBENSARCHIV - bestehende Installationen laufen
        // dadurch unverändert weiter, bis der Benutzer aktiv umzieht.
        //
        // WICHTIG: alle bisherigen Namen (OrdnerPfad, ErinnerungenOrdnerPfad,
        // AsservatenkammerOrdnerPfad, ArbeitsstandPfad, usw.) bleiben
        // unverändert bestehen, nur als Eigenschaft statt als feste
        // Konstante - jede bestehende Stelle im restlichen Code funktioniert
        // dadurch unverändert weiter.
        // ============================================================
        private static readonly string KonfigurationsOrdnerPfad = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LEBENSARCHIV");

        private static readonly string ArchivStandortZeigerPfad = Path.Combine(KonfigurationsOrdnerPfad, "archivstandort.json");

        private static string _archivWurzelPfad;

        private static string ArchivWurzelPfad
        {
            get
            {
                if (_archivWurzelPfad == null)
                {
                    _archivWurzelPfad = LadeArchivStandortKonfiguration().ArchivPfad ?? KonfigurationsOrdnerPfad;
                }

                return _archivWurzelPfad;
            }
        }

        private static ArchivStandortKonfiguration LadeArchivStandortKonfiguration()
        {
            try
            {
                if (File.Exists(ArchivStandortZeigerPfad))
                {
                    string json = File.ReadAllText(ArchivStandortZeigerPfad);
                    ArchivStandortKonfiguration konfiguration = JsonSerializer.Deserialize<ArchivStandortKonfiguration>(json);

                    if (konfiguration != null
                        && !string.IsNullOrWhiteSpace(konfiguration.ArchivPfad)
                        && Directory.Exists(konfiguration.ArchivPfad))
                    {
                        return konfiguration;
                    }
                }
            }
            catch
            {
                // Zeiger nicht lesbar - dann eben mit dem bisherigen
                // Speicherort weiterarbeiten, keine Fehlermeldung fuer
                // eine reine Komfortfunktion.
            }

            // Noch kein eigener Speicherort gewählt - bisheriger Ort.
            return new ArchivStandortKonfiguration { ArchivPfad = KonfigurationsOrdnerPfad };
        }

        private static void SpeichereArchivStandortKonfiguration(ArchivStandortKonfiguration konfiguration)
        {
            try
            {
                Directory.CreateDirectory(KonfigurationsOrdnerPfad);

                JsonSerializerOptions optionen = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(konfiguration, optionen);
                File.WriteAllText(ArchivStandortZeigerPfad, json);
            }
            catch
            {
                // Zeiger konnte nicht gespeichert werden - dann bleibt der
                // neue Pfad zumindest für die laufende Sitzung aktiv.
            }
        }

        private static string OrdnerPfad => ArchivWurzelPfad;

        private static string DateiPfad => Path.Combine(OrdnerPfad, "personen.json");

        // ============================================================
        // ARCHITEKTUR: ERINNERUNGEN (Fotos/Titelbilder der Personen)
        // ============================================================
        private static string ErinnerungenOrdnerPfad => Path.Combine(OrdnerPfad, "Erinnerungen");

        private static string PersonErinnerungsOrdner(Person person)
        {
            return Path.Combine(ErinnerungenOrdnerPfad, person.Id.ToString());
        }

        private static string ErinnerungsOrdnerFuer(Person person, Ereignis ereignis)
        {
            if (person != null)
            {
                return Path.Combine(ErinnerungenOrdnerPfad, person.Id.ToString(), ereignis.Id.ToString());
            }

            return Path.Combine(ErinnerungenOrdnerPfad, "FreieEreignisse", ereignis.Id.ToString());
        }

        // ============================================================
        // ARCHITEKTUR: ERINNERUNGSVERZEICHNIS (Build 0.4)
        // ============================================================
        private static string ErinnerungsVerzeichnisPfad => Path.Combine(OrdnerPfad, "erinnerungsverzeichnis.json");

        // ============================================================
        // ARCHITEKTUR: ORDNERGEDÄCHTNIS (Build 1.1)
        // ============================================================
        private static string OrdnergedaechtnisPfad => Path.Combine(OrdnerPfad, "ordnergedaechtnis.json");

        // ============================================================
        // ARCHITEKTUR: ARBEITSSTAND (Build 1.9)
        // ============================================================
        private static string ArbeitsstandPfad => Path.Combine(OrdnerPfad, "arbeitsstand.json");

        // Index des neuen "Arbeitsmappe"-Reiters (Build 2.1, nach Einstellungen).
        private const int ArbeitsmappeTabIndex = 7;

        private static string ArbeitsmappeZugeordnetPfad => Path.Combine(OrdnerPfad, "arbeitsmappe_zugeordnet.json");

        // ============================================================
        // NEUE FUNKTION (Generaltest 2): ASSERVATENKAMMER
        // ============================================================
        private static string AsservatenkammerOrdnerPfad => Path.Combine(OrdnerPfad, "Asservatenkammer");

        // ============================================================
        // ARCHITEKTUR: ARBEITSMAPPE (Build 2.1)
        // ============================================================
        private List<GefundeneDatei> arbeitsmappeAlleDateien = new List<GefundeneDatei>();
        private string arbeitsmappeFilter = "Alle";
        private int arbeitsmappeSeite = 1;
        // Punkt 2 (letzte Feinjustierung): 14 pro Seite, da im Fenster noch
        // Platz für 2 weitere Kacheln in der 2. Zeile war.
        private const int ArbeitsmappeProSeite = 14;
        private HashSet<string> arbeitsmappeAusgewaehlt = new HashSet<string>();
        private Person arbeitsmappeNeuesEreignisPerson = null;
        private Person arbeitsmappeLetztesEreignisPerson = null;
        private Ereignis arbeitsmappeLetztesEreignis = null;
        private HashSet<string> arbeitsmappeBereitsZugeordnet = new HashSet<string>();

        private static readonly Dictionary<string, string> DateitypZuordnung = new Dictionary<string, string>
        {
            { ".jpg", "Bilder" }, { ".jpeg", "Bilder" }, { ".png", "Bilder" }, { ".bmp", "Bilder" },
            { ".gif", "Bilder" }, { ".tif", "Bilder" }, { ".tiff", "Bilder" }, { ".heic", "Bilder" }, { ".webp", "Bilder" },

            { ".mp4", "Videos" }, { ".mov", "Videos" }, { ".avi", "Videos" }, { ".mkv", "Videos" },
            { ".wmv", "Videos" }, { ".m4v", "Videos" },

            { ".mp3", "Audio" }, { ".wav", "Audio" }, { ".m4a", "Audio" }, { ".aac", "Audio" },
            { ".ogg", "Audio" }, { ".flac", "Audio" },

            { ".pdf", "PDF" },

            { ".doc", "Dokumente" }, { ".docx", "Dokumente" }, { ".odt", "Dokumente" }, { ".rtf", "Dokumente" },
            { ".xls", "Dokumente" }, { ".xlsx", "Dokumente" }, { ".ppt", "Dokumente" }, { ".pptx", "Dokumente" },

            { ".txt", "Textdateien" }, { ".md", "Textdateien" }
        };

        private static string ErmittleDateityp(string dateiendung)
        {
            string endungKlein = dateiendung.ToLower();

            if (DateitypZuordnung.ContainsKey(endungKlein))
            {
                return DateitypZuordnung[endungKlein];
            }

            return "Sonstige";
        }

        public MainWindow()
        {
            InitializeComponent();
            statusTimer = new DispatcherTimer();
            statusTimer.Interval = TimeSpan.FromSeconds(3);
            statusTimer.Tick += StatusTimer_Tick;

            OrdnerBaumTreeView.ItemsSource = ordnerBaumWurzelKnoten;

            LadeDaten();
            ZeigeEinstellungenImFormular();
            ZeigeAktuellenArchivSpeicherort();
            ZeigeGespeicherteZusammenfassung();

            bool arbeitFortgesetzt = PruefeUndBieteArbeitsstandAn();

            if (!arbeitFortgesetzt)
            {
                ZeigeStartseiteVorschlag();
            }

            VornameTextBox.Focus();
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            statusTimer.Stop();
            StatusText.Visibility = Visibility.Collapsed;
        }

        private void ZeigeStatusMeldung(string text)
        {
            StatusText.Text = text;
            StatusText.Visibility = Visibility.Visible;
            statusTimer.Stop();
            statusTimer.Start();
        }


        // ============================================================
        // BUILD 1.9: JAMES MERKT SICH DIE ARBEIT
        // ============================================================

        private Arbeitsstand LadeArbeitsstand()
        {
            try
            {
                if (File.Exists(ArbeitsstandPfad))
                {
                    string json = File.ReadAllText(ArbeitsstandPfad);
                    return JsonSerializer.Deserialize<Arbeitsstand>(json);
                }
            }
            catch
            {
            }

            return null;
        }

        private void SpeichereArbeitsstand()
        {
            try
            {
                Person person = PersonenListe.SelectedItem as Person;

                if (person == null)
                {
                    return;
                }

                Ereignis ereignis = EreignisseListe.SelectedItem as Ereignis;

                Arbeitsstand stand = new Arbeitsstand
                {
                    PersonId = person.Id,
                    EreignisId = ereignis != null ? ereignis.Id : (Guid?)null,
                    Arbeitsbereich = "Schreibtisch",
                    Zeitpunkt = DateTime.Now
                };

                Directory.CreateDirectory(OrdnerPfad);

                JsonSerializerOptions optionen = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(stand, optionen);

                File.WriteAllText(ArbeitsstandPfad, json);
            }
            catch
            {
            }
        }

        private bool PruefeUndBieteArbeitsstandAn()
        {
            Arbeitsstand stand = LadeArbeitsstand();

            if (stand == null || stand.PersonId == null)
            {
                return false;
            }

            Person person = allePersonen.FirstOrDefault(p => p.Id == stand.PersonId.Value);

            if (person == null)
            {
                return false;
            }

            Ereignis ereignis = null;

            if (stand.EreignisId != null && person.Ereignisse != null)
            {
                ereignis = person.Ereignisse.FirstOrDefault(e => e.Id == stand.EreignisId.Value);
            }

            string beschreibung = ereignis != null
                ? ereignis.Titel + " (" + person.ToString() + ")"
                : person.ToString();

            bool ergebnis = James.FrageJaNein(James.ArbeitFortsetzenFrage(beschreibung));

            if (!ergebnis)
            {
                return false;
            }

            HauptTabControl.SelectedIndex = 0;

            StartseiteBereich.Visibility = Visibility.Collapsed;
            EreignisBereich.Visibility = Visibility.Collapsed;
            EreignismappeBereich.Visibility = Visibility.Collapsed;
            PersonenFormularBereich.Visibility = Visibility.Visible;
            PersonenListeBereich.Visibility = Visibility.Visible;

            PersonenListe.SelectedItem = person;

            if (ereignis != null)
            {
                EreignisseListe.SelectedItem = ereignis;
            }

            return true;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SpeichereArbeitsstand();
        }

        // ============================================================
        // BUILD 5.1: DIE STARTSEITE ALS ZENTRALER EINSTIEGSPUNKT
        // ============================================================

        private void StartseitePersonButton_Click(object sender, RoutedEventArgs e)
        {
            StartseiteBereich.Visibility = Visibility.Collapsed;
            EreignisBereich.Visibility = Visibility.Collapsed;
            EreignismappeBereich.Visibility = Visibility.Collapsed;
            SammlungBereich.Visibility = Visibility.Collapsed;

            PersonenFormularBereich.Visibility = Visibility.Visible;
            PersonenListeBereich.Visibility = Visibility.Visible;
        }

        private void StartseiteEreignisButton_Click(object sender, RoutedEventArgs e)
        {
            StartseiteBereich.Visibility = Visibility.Collapsed;
            PersonenFormularBereich.Visibility = Visibility.Collapsed;
            PersonenListeBereich.Visibility = Visibility.Collapsed;
            EreignismappeBereich.Visibility = Visibility.Collapsed;
            SammlungBereich.Visibility = Visibility.Collapsed;

            EreignisBereich.Visibility = Visibility.Visible;

            aktuellBearbeitetesFreiesEreignis = null;
            EreignisTitelSchreibtischTextBox.Clear();
            FreiesEreignisErinnerungenLinkText.Visibility = Visibility.Collapsed;
            EreignisSchreibtischArchivierenButton.Visibility = Visibility.Collapsed;

            if (EreignisListeSchreibtisch.SelectedItem != null)
            {
                EreignisListeSchreibtisch.SelectedItem = null;
            }

            AktualisiereFreieEreignisseAnzeige();
            EreignisTitelSchreibtischTextBox.Focus();
        }

        private void ZurStartseite_Click(object sender, MouseButtonEventArgs e)
        {
            PersonenFormularBereich.Visibility = Visibility.Collapsed;
            PersonenListeBereich.Visibility = Visibility.Collapsed;
            EreignisBereich.Visibility = Visibility.Collapsed;
            EreignismappeBereich.Visibility = Visibility.Collapsed;
            SammlungBereich.Visibility = Visibility.Collapsed;

            StartseiteBereich.Visibility = Visibility.Visible;

            ZeigeStartseiteVorschlag();
        }

        // Neue Funktion (Wunsch aus Generaltest 2): dritte Schublade
        // "Sammlung" neben Person und besonderem Ereignis - dieselbe
        // Bedienlogik wie StartseiteEreignisButton_Click.
        private void StartseiteSammlungButton_Click(object sender, RoutedEventArgs e)
        {
            StartseiteBereich.Visibility = Visibility.Collapsed;
            PersonenFormularBereich.Visibility = Visibility.Collapsed;
            PersonenListeBereich.Visibility = Visibility.Collapsed;
            EreignisBereich.Visibility = Visibility.Collapsed;
            EreignismappeBereich.Visibility = Visibility.Collapsed;

            SammlungBereich.Visibility = Visibility.Visible;

            aktuellBearbeiteteSammlung = null;
            SammlungTitelSchreibtischTextBox.Clear();
            SammlungErinnerungenLinkText.Visibility = Visibility.Collapsed;
            SammlungSchreibtischArchivierenButton.Visibility = Visibility.Collapsed;

            if (SammlungenListeSchreibtisch.SelectedItem != null)
            {
                SammlungenListeSchreibtisch.SelectedItem = null;
            }

            AktualisiereSammlungenAnzeige();
            SammlungTitelSchreibtischTextBox.Focus();
        }


        // ============================================================
        // ETAPPE A: DIE EINHEITLICHE EREIGNISLISTE (Vorbereitung)
        // ============================================================

        private void AktualisiereEinheitlicheEreignisliste()
        {
            List<EreignisEintrag> neu = new List<EreignisEintrag>();

            foreach (Person person in allePersonen)
            {
                if (person.Ereignisse == null)
                {
                    continue;
                }

                foreach (Ereignis ereignis in person.Ereignisse)
                {
                    neu.Add(new EreignisEintrag
                    {
                        Ereignis = ereignis,
                        Person = person,
                        IstArchiviert = false
                    });
                }
            }

            foreach (object element in ArchivListe.Items)
            {
                Person person = element as Person;

                if (person == null || person.Ereignisse == null)
                {
                    continue;
                }

                foreach (Ereignis ereignis in person.Ereignisse)
                {
                    neu.Add(new EreignisEintrag
                    {
                        Ereignis = ereignis,
                        Person = person,
                        IstArchiviert = true
                    });
                }
            }

            foreach (Ereignis ereignis in freieEreignisse)
            {
                neu.Add(new EreignisEintrag
                {
                    Ereignis = ereignis,
                    Person = null,
                    IstArchiviert = false
                });
            }

            foreach (Ereignis ereignis in freieEreignisseArchiv)
            {
                neu.Add(new EreignisEintrag
                {
                    Ereignis = ereignis,
                    Person = null,
                    IstArchiviert = true
                });
            }

            alleEreignisse = neu;
        }
    }
}
