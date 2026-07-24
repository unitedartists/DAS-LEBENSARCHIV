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
        private DispatcherTimer statusTimer;

        // Hauptliste aller Personen (unabhängig davon, was gerade angezeigt/gefiltert wird)
        private List<Person> allePersonen = new List<Person>();

        // Build 5.0: freie, personenunabhängige Ereignisse - die
        // "persönliche Ereignisliste" aus dem Architekturauftrag.
        private List<Ereignis> freieEreignisse = new List<Ereignis>();
        private List<Ereignis> freieEreignisseArchiv = new List<Ereignis>();

        // Etappe A.5, Punkt 2: Papierkorb für freie Ereignisse.
        private List<Ereignis> freieEreignissePapierkorb = new List<Ereignis>();

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

        private static readonly string OrdnerPfad = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LEBENSARCHIV");

        private static readonly string DateiPfad = Path.Combine(OrdnerPfad, "personen.json");

        // ============================================================
        // ARCHITEKTUR: ERINNERUNGEN (Fotos/Titelbilder der Personen)
        // ============================================================
        private static readonly string ErinnerungenOrdnerPfad = Path.Combine(OrdnerPfad, "Erinnerungen");

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
        private static readonly string ErinnerungsVerzeichnisPfad = Path.Combine(OrdnerPfad, "erinnerungsverzeichnis.json");

        // ============================================================
        // ARCHITEKTUR: ORDNERGEDÄCHTNIS (Build 1.1)
        // ============================================================
        private static readonly string OrdnergedaechtnisPfad = Path.Combine(OrdnerPfad, "ordnergedaechtnis.json");

        // ============================================================
        // ARCHITEKTUR: ARBEITSSTAND (Build 1.9)
        // ============================================================
        private static readonly string ArbeitsstandPfad = Path.Combine(OrdnerPfad, "arbeitsstand.json");

        // Index des neuen "Arbeitsmappe"-Reiters (Build 2.1, nach Einstellungen).
        private const int ArbeitsmappeTabIndex = 7;

        private static readonly string ArbeitsmappeZugeordnetPfad = Path.Combine(OrdnerPfad, "arbeitsmappe_zugeordnet.json");

        // ============================================================
        // ARCHITEKTUR: ARBEITSMAPPE (Build 2.1)
        // ============================================================
        private List<GefundeneDatei> arbeitsmappeAlleDateien = new List<GefundeneDatei>();
        private string arbeitsmappeFilter = "Alle";
        private int arbeitsmappeSeite = 1;
        private const int ArbeitsmappeProSeite = 25;
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
        // EREIGNISSE (Build 0.5) + EREIGNIS-FOTOS (Build 0.6)
        // ============================================================

        private void AktualisiereEreignisseAnzeige(Person person)
        {
            EreignisseListe.Items.Clear();
            EreignisAuswahlPanel.Visibility = Visibility.Collapsed;
            EreignisFotoBild.Source = null;

            if (person != null && person.Ereignisse != null)
            {
                foreach (Ereignis ereignis in person.Ereignisse)
                {
                    EreignisseListe.Items.Add(ereignis);
                }
            }
        }

        private void ZeigeEreignisFoto(Person person, Ereignis ereignis)
        {
            if (person != null && ereignis != null && ereignis.EreignisFotoDateiname != null)
            {
                string pfad = Path.Combine(ErinnerungsOrdnerFuer(person, ereignis), ereignis.EreignisFotoDateiname);

                if (File.Exists(pfad))
                {
                    BitmapImage bild = new BitmapImage();
                    bild.BeginInit();
                    bild.CacheOption = BitmapCacheOption.OnLoad;
                    bild.UriSource = new Uri(pfad);
                    bild.EndInit();

                    EreignisFotoBild.Source = bild;
                    return;
                }
            }

            EreignisFotoBild.Source = null;
        }

        private void EreignisseListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Ereignis ereignis = EreignisseListe.SelectedItem as Ereignis;
            Person person = PersonenListe.SelectedItem as Person;

            if (ereignis == null)
            {
                EreignisAuswahlPanel.Visibility = Visibility.Collapsed;
                EreignisFotoBild.Source = null;
                EreignisDetailsText.Text = "";
                BewertungFavoritCheckBox.IsChecked = false;
                BewertungWichtigCheckBox.IsChecked = false;
                BewertungFamiliengeschichteCheckBox.IsChecked = false;
                BewertungLebensbuchCheckBox.IsChecked = false;
                BewertungUeberpruefenCheckBox.IsChecked = false;
                BewertungPrivatCheckBox.IsChecked = false;
                BewertungUnbekanntCheckBox.IsChecked = false;
                ZeigeEreignisErinnerungenLink(null);
                SpeichereArbeitsstand();
                return;
            }

            EreignisAuswahlPanel.Visibility = Visibility.Visible;
            ZeigeEreignisFoto(person, ereignis);
            EreignisDetailsText.Text = ErstelleEreignisDetailsText(ereignis);
            ZeigeBewertungen(ereignis);
            ZeigeEreignisErinnerungenLink(ereignis);
            SpeichereArbeitsstand();
        }

        // ============================================================
        // BUILD 0.9: ERINNERUNGEN BEWERTEN
        // ============================================================

        private void ZeigeBewertungen(Ereignis ereignis)
        {
            List<string> bewertungen = ereignis.Bewertungen ?? new List<string>();

            BewertungFavoritCheckBox.IsChecked = bewertungen.Contains("Favorit");
            BewertungWichtigCheckBox.IsChecked = bewertungen.Contains("Besonders wichtig");
            BewertungFamiliengeschichteCheckBox.IsChecked = bewertungen.Contains("Familiengeschichte");
            BewertungLebensbuchCheckBox.IsChecked = bewertungen.Contains("Für das Lebensbuch geeignet");
            BewertungUeberpruefenCheckBox.IsChecked = bewertungen.Contains("Noch überprüfen");
            BewertungPrivatCheckBox.IsChecked = bewertungen.Contains("Privat");
            BewertungUnbekanntCheckBox.IsChecked = bewertungen.Contains("Unbekannt");
        }

        private void BewertungSpeichern_Click(object sender, RoutedEventArgs e)
        {
            Ereignis ereignis = EreignisseListe.SelectedItem as Ereignis;
            Person person = PersonenListe.SelectedItem as Person;

            if (ereignis == null)
            {
                James.Hinweis(James.BitteEreignisAuswaehlen);
                return;
            }

            List<string> neueBewertungen = new List<string>();

            if (BewertungFavoritCheckBox.IsChecked == true)
            {
                neueBewertungen.Add("Favorit");
            }

            if (BewertungWichtigCheckBox.IsChecked == true)
            {
                neueBewertungen.Add("Besonders wichtig");
            }

            if (BewertungFamiliengeschichteCheckBox.IsChecked == true)
            {
                neueBewertungen.Add("Familiengeschichte");
            }

            if (BewertungLebensbuchCheckBox.IsChecked == true)
            {
                neueBewertungen.Add("Für das Lebensbuch geeignet");
            }

            if (BewertungUeberpruefenCheckBox.IsChecked == true)
            {
                neueBewertungen.Add("Noch überprüfen");
            }

            if (BewertungPrivatCheckBox.IsChecked == true)
            {
                neueBewertungen.Add("Privat");
            }

            if (BewertungUnbekanntCheckBox.IsChecked == true)
            {
                neueBewertungen.Add("Unbekannt");
            }

            ereignis.Bewertungen = neueBewertungen;
            ereignis.ModifiedAt = DateTime.Now;

            if (person != null)
            {
                person.ModifiedAt = DateTime.Now;
            }

            SpeichereDaten();

            ZeigeStatusMeldung(James.BewertungGespeichert(ereignis.Titel));
        }

        private string ErstelleEreignisDetailsText(Ereignis ereignis)
        {
            List<string> zeilen = new List<string>();

            if (!string.IsNullOrWhiteSpace(ereignis.Beschreibung))
            {
                zeilen.Add(ereignis.Beschreibung);
            }

            if (!string.IsNullOrWhiteSpace(ereignis.Ort))
            {
                zeilen.Add("Ort: " + ereignis.Ort);
            }

            if (!string.IsNullOrWhiteSpace(ereignis.Jahreszeit))
            {
                zeilen.Add("Jahreszeit: " + ereignis.Jahreszeit);
            }

            if (ereignis.Stichwoerter != null && ereignis.Stichwoerter.Count > 0)
            {
                zeilen.Add("Stichwörter: " + string.Join(", ", ereignis.Stichwoerter));
            }

            if (!string.IsNullOrWhiteSpace(ereignis.Bemerkungen))
            {
                zeilen.Add("Bemerkungen: " + ereignis.Bemerkungen);
            }

            if (ereignis.Beteiligte != null && ereignis.Beteiligte.Count > 0)
            {
                List<string> beteiligteMitKennzeichen = ereignis.Beteiligte
                    .Select(name => allePersonen.Any(p => p.ToString().Equals(name, StringComparison.OrdinalIgnoreCase))
                        ? name + " ✓"
                        : name)
                    .ToList();

                zeilen.Add("Wer war noch dabei: " + string.Join(", ", beteiligteMitKennzeichen));
            }

            if (ereignis.WeitereFotoDateinamen != null && ereignis.WeitereFotoDateinamen.Count > 0)
            {
                zeilen.Add("+ " + ereignis.WeitereFotoDateinamen.Count + " weitere Erinnerungen zu diesem Ereignis");
            }

            return string.Join("\n", zeilen);
        }

        private void EreignisFotoHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            Person person = PersonenListe.SelectedItem as Person;
            Ereignis ereignis = EreignisseListe.SelectedItem as Ereignis;

            if (person == null || ereignis == null)
            {
                James.Hinweis(James.BittePersonUndEreignisAuswaehlen);
                return;
            }

            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;
            James.Hinweis(James.ArbeitsmappeUmleitungEreignis(ereignis.Titel));
        }

        private void EreignisHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            Person person = PersonenListe.SelectedItem as Person;

            if (person == null)
            {
                James.Hinweis(James.BittePersonAuswaehlen);
                return;
            }

            EreignisTitelTextBox.Clear();
            EreignisBeschreibungTextBox.Clear();
            EreignisDatumTextBox.Clear();
            EreignisOrtTextBox.Clear();
            EreignisJahreszeitComboBox.SelectedIndex = 0;
            EreignisStichwoerterTextBox.Clear();
            EreignisBemerkungenTextBox.Clear();
            EreignisBeteiligteTextBox.Clear();

            EreignisFormularPanel.Visibility = Visibility.Visible;
            EreignisTitelTextBox.Focus();
        }

        private void EreignisSpeichern_Click(object sender, RoutedEventArgs e)
        {
            Person person = PersonenListe.SelectedItem as Person;

            if (person == null)
            {
                James.Hinweis(James.BittePersonAuswaehlen);
                return;
            }

            string titel = EreignisTitelTextBox.Text.Trim();

            if (titel == "")
            {
                James.Hinweis(James.BitteEreignisTitelEingeben);
                return;
            }

            string jahreszeit = "";
            ComboBoxItem ausgewaehlteJahreszeit = EreignisJahreszeitComboBox.SelectedItem as ComboBoxItem;

            if (ausgewaehlteJahreszeit != null)
            {
                jahreszeit = ausgewaehlteJahreszeit.Content.ToString();
            }

            List<string> stichwoerter = EreignisStichwoerterTextBox.Text
                .Split(',')
                .Select(teil => teil.Trim())
                .Where(teil => teil != "")
                .ToList();

            List<string> beteiligte = EreignisBeteiligteTextBox.Text
                .Split(',')
                .Select(teil => teil.Trim())
                .Where(teil => teil != "")
                .ToList();

            Ereignis neuesEreignis = new Ereignis
            {
                Titel = titel,
                Beschreibung = EreignisBeschreibungTextBox.Text.Trim(),
                Datum = EreignisDatumTextBox.Text.Trim(),
                Ort = EreignisOrtTextBox.Text.Trim(),
                Jahreszeit = jahreszeit,
                Stichwoerter = stichwoerter,
                Bemerkungen = EreignisBemerkungenTextBox.Text.Trim(),
                Beteiligte = beteiligte
            };

            if (person.Ereignisse == null)
            {
                person.Ereignisse = new List<Ereignis>();
            }

            person.Ereignisse.Add(neuesEreignis);
            person.ModifiedAt = DateTime.Now;

            AktualisiereEreignisseAnzeige(person);
            ZeigePersonErinnerungenLink(person);

            SpeichereDaten();

            EreignisFormularPanel.Visibility = Visibility.Collapsed;

            EreignisseListe.SelectedItem = neuesEreignis;

            ZeigeStatusMeldung(James.EreignisZugeordnet(neuesEreignis.Titel, person.ToString()));
        }

        // ============================================================
        // WERKZEUGE: COMPUTER KENNENLERNEN (Build 0.4, Ordnerbaum ab Build 1.1)
        // ============================================================

        private void WerkzeugeMenuComputerKennenlernen_Click(object sender, RoutedEventArgs e)
        {
            HauptTabControl.SelectedIndex = 5;
        }

        private void WerkzeugeMenuErinnerungenAufraeumen_Click(object sender, RoutedEventArgs e)
        {
            HauptTabControl.SelectedIndex = 5;
        }

        private void MenuEinstellungen_Click(object sender, RoutedEventArgs e)
        {
            HauptTabControl.SelectedIndex = EinstellungenTabIndex;
        }

        // ============================================================
        // BUILD 1.0: ERINNERUNGEN AUFRÄUMEN
        // ============================================================

        private void ErinnerungenAufraeumen_Click(object sender, RoutedEventArgs e)
        {
            bool ergebnis = James.FrageJaNein(James.DoppelgaengerEinladung, "Erinnerungen aufräumen", MessageBoxImage.Information);

            if (!ergebnis)
            {
                return;
            }

            if (!File.Exists(ErinnerungsVerzeichnisPfad))
            {
                James.Hinweis(James.KeinErinnerungsverzeichnisGefunden);
                return;
            }

            List<GefundeneDatei> alleDateien;

            try
            {
                string json = File.ReadAllText(ErinnerungsVerzeichnisPfad);
                ErinnerungsVerzeichnis verzeichnis = JsonSerializer.Deserialize<ErinnerungsVerzeichnis>(json);
                alleDateien = (verzeichnis != null && verzeichnis.Dateien != null) ? verzeichnis.Dateien : new List<GefundeneDatei>();
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimLesenErinnerungsverzeichnis(ex.Message));
                return;
            }

            Dictionary<string, List<GefundeneDatei>> gruppenNachHash = new Dictionary<string, List<GefundeneDatei>>();

            foreach (GefundeneDatei datei in alleDateien)
            {
                if (string.IsNullOrEmpty(datei.Hashwert))
                {
                    continue;
                }

                if (!gruppenNachHash.ContainsKey(datei.Hashwert))
                {
                    gruppenNachHash[datei.Hashwert] = new List<GefundeneDatei>();
                }

                gruppenNachHash[datei.Hashwert].Add(datei);
            }

            DoppelgaengerListe.Items.Clear();
            DoppelgaengerDetailsText.Text = "";

            int anzahlGruppen = 0;
            int anzahlDateienGesamt = 0;

            foreach (KeyValuePair<string, List<GefundeneDatei>> eintrag in gruppenNachHash)
            {
                if (eintrag.Value.Count > 1)
                {
                    DoppelgaengerGruppe gruppe = new DoppelgaengerGruppe
                    {
                        Hashwert = eintrag.Key,
                        Dateien = eintrag.Value
                    };

                    DoppelgaengerListe.Items.Add(gruppe);

                    anzahlGruppen++;
                    anzahlDateienGesamt += eintrag.Value.Count;
                }
            }

            DoppelgaengerErgebnisPanel.Visibility = Visibility.Visible;

            if (anzahlGruppen > 0)
            {
                DoppelgaengerStatusText.Text = James.DoppelgaengerGefunden(anzahlDateienGesamt, anzahlGruppen);
            }
            else
            {
                DoppelgaengerStatusText.Text = James.KeineDoppelgaengerGefunden;
            }
        }

        private void DoppelgaengerListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DoppelgaengerGruppe gruppe = DoppelgaengerListe.SelectedItem as DoppelgaengerGruppe;

            if (gruppe == null)
            {
                DoppelgaengerDetailsText.Text = "";
                return;
            }

            List<string> zeilen = new List<string>();

            foreach (GefundeneDatei datei in gruppe.Dateien)
            {
                zeilen.Add(datei.VollstaendigerPfad);
            }

            DoppelgaengerDetailsText.Text = string.Join("\n", zeilen);
        }

        private void ComputerKennenlernen_Click(object sender, RoutedEventArgs e)
        {
            bool ergebnis = James.FrageJaNein(James.ComputerKennenlernenEinladung, "Computer kennenlernen", MessageBoxImage.Information);

            if (!ergebnis)
            {
                return;
            }

            ordnerBaumWurzelKnoten.Clear();

            DriveInfo[] laufwerke = DriveInfo.GetDrives();

            foreach (DriveInfo laufwerk in laufwerke)
            {
                if (!laufwerk.IsReady)
                {
                    continue;
                }

                string beschriftung = laufwerk.Name;

                try
                {
                    if (!string.IsNullOrWhiteSpace(laufwerk.VolumeLabel))
                    {
                        beschriftung = laufwerk.Name + " (" + laufwerk.VolumeLabel + ")";
                    }
                }
                catch
                {
                }

                OrdnerKnoten wurzelKnoten = new OrdnerKnoten
                {
                    Name = beschriftung,
                    VollstaendigerPfad = laufwerk.Name
                };

                wurzelKnoten.Kinder.Add(ErzeugePlatzhalterKnoten());

                ordnerBaumWurzelKnoten.Add(wurzelKnoten);
            }

            OrdnerAuswahlPanel.Visibility = Visibility.Visible;
            WerkzeugeStatusText.Text = ErstelleOrdnergedaechtnisBegruessung();
        }

        private static OrdnerKnoten ErzeugePlatzhalterKnoten()
        {
            return new OrdnerKnoten
            {
                Name = "Wird geladen ...",
                VollstaendigerPfad = null
            };
        }

        private void OrdnerKnoten_Expanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem element = e.OriginalSource as TreeViewItem;

            if (element == null)
            {
                return;
            }

            OrdnerKnoten knoten = element.DataContext as OrdnerKnoten;

            if (knoten == null || knoten.KinderGeladen || knoten.VollstaendigerPfad == null)
            {
                return;
            }

            knoten.Kinder.Clear();
            knoten.KinderGeladen = true;

            try
            {
                foreach (string unterordnerPfad in Directory.EnumerateDirectories(knoten.VollstaendigerPfad))
                {
                    try
                    {
                        DirectoryInfo info = new DirectoryInfo(unterordnerPfad);

                        OrdnerKnoten unterKnoten = new OrdnerKnoten
                        {
                            Name = info.Name,
                            VollstaendigerPfad = info.FullName
                        };

                        unterKnoten.Kinder.Add(ErzeugePlatzhalterKnoten());

                        knoten.Kinder.Add(unterKnoten);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            e.Handled = true;
        }

        private void SammleAusgewaehlteOrdner(IEnumerable<OrdnerKnoten> knoten, List<string> ergebnis)
        {
            foreach (OrdnerKnoten einzelnerKnoten in knoten)
            {
                if (einzelnerKnoten.VollstaendigerPfad == null)
                {
                    continue;
                }

                if (einzelnerKnoten.IsChecked)
                {
                    ergebnis.Add(einzelnerKnoten.VollstaendigerPfad);
                }
                else
                {
                    SammleAusgewaehlteOrdner(einzelnerKnoten.Kinder, ergebnis);
                }
            }
        }

        // ============================================================
        // BUILD 1.1: ORDNERGEDÄCHTNIS - LADEN, SPEICHERN, BEGRÜSSUNG
        // ============================================================

        private Ordnergedaechtnis LadeOrdnergedaechtnis()
        {
            try
            {
                if (File.Exists(OrdnergedaechtnisPfad))
                {
                    string json = File.ReadAllText(OrdnergedaechtnisPfad);
                    Ordnergedaechtnis geladen = JsonSerializer.Deserialize<Ordnergedaechtnis>(json);

                    if (geladen != null)
                    {
                        if (geladen.Ordner == null)
                        {
                            geladen.Ordner = new List<OrdnerErinnerung>();
                        }

                        return geladen;
                    }
                }
            }
            catch
            {
            }

            return new Ordnergedaechtnis();
        }

        private void SpeichereOrdnergedaechtnis(Ordnergedaechtnis daten)
        {
            try
            {
                Directory.CreateDirectory(OrdnerPfad);

                JsonSerializerOptions optionen = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(daten, optionen);

                File.WriteAllText(OrdnergedaechtnisPfad, json);
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimSpeichernOrdnergedaechtnis(ex.Message));
            }
        }

        private string ErstelleOrdnergedaechtnisBegruessung()
        {
            Ordnergedaechtnis daten = LadeOrdnergedaechtnis();

            return James.OrdnergedaechtnisBegruessung(daten.Ordner);
        }

        private CancellationTokenSource scanAbbrechenQuelle;

        private async void RundgangStarten_Click(object sender, RoutedEventArgs e)
        {
            List<string> ausgewaehlteOrdner = new List<string>();
            SammleAusgewaehlteOrdner(ordnerBaumWurzelKnoten, ausgewaehlteOrdner);

            if (ausgewaehlteOrdner.Count == 0)
            {
                James.Hinweis(James.BitteOrdnerAuswaehlen);
                return;
            }

            RundgangStartenButton.IsEnabled = false;
            ComputerKennenlernenButton.IsEnabled = false;
            AbbrechenRundgangButton.Visibility = Visibility.Visible;
            WerkzeugeStatusText.Text = James.RundgangLaeuft(0);

            List<GefundeneDatei> gefundeneDateien = new List<GefundeneDatei>();
            Dictionary<string, int> zaehlerProTyp = new Dictionary<string, int>();
            Dictionary<string, int> anzahlProAusgewaehltemOrdner = new Dictionary<string, int>();
            int[] gesamtZaehler = new int[1];

            scanAbbrechenQuelle = new CancellationTokenSource();
            CancellationToken abbrechenToken = scanAbbrechenQuelle.Token;

            IProgress<int> fortschritt = new Progress<int>(anzahlBisher =>
            {
                WerkzeugeStatusText.Text = James.RundgangLaeuft(anzahlBisher);
            });

            bool wurdeAbgebrochen = false;

            try
            {
                await Task.Run(() =>
                {
                    foreach (string ordnerPfad in ausgewaehlteOrdner)
                    {
                        int anzahlVorher = gefundeneDateien.Count;

                        ScanneOrdner(ordnerPfad, zaehlerProTyp, gefundeneDateien, gesamtZaehler, abbrechenToken, fortschritt);

                        anzahlProAusgewaehltemOrdner[ordnerPfad] = gefundeneDateien.Count - anzahlVorher;
                    }
                }, abbrechenToken);
            }
            catch (OperationCanceledException)
            {
                wurdeAbgebrochen = true;
            }

            try
            {
                Directory.CreateDirectory(OrdnerPfad);

                ErinnerungsVerzeichnis verzeichnis = new ErinnerungsVerzeichnis
                {
                    ErstelltAm = DateTime.Now,
                    Dateien = gefundeneDateien
                };

                JsonSerializerOptions optionen = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(verzeichnis, optionen);

                File.WriteAllText(ErinnerungsVerzeichnisPfad, json);
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimSpeichernErinnerungsverzeichnis(ex.Message));
            }

            Ordnergedaechtnis ordnergedaechtnis = LadeOrdnergedaechtnis();

            foreach (KeyValuePair<string, int> eintrag in anzahlProAusgewaehltemOrdner)
            {
                OrdnerErinnerung erinnerung = ordnergedaechtnis.Ordner.FirstOrDefault(o => o.Pfad == eintrag.Key);

                if (erinnerung == null)
                {
                    erinnerung = new OrdnerErinnerung { Pfad = eintrag.Key };
                    ordnergedaechtnis.Ordner.Add(erinnerung);
                }

                erinnerung.LetzterScan = DateTime.Now;
                erinnerung.AnzahlDateien = eintrag.Value;
            }

            SpeichereOrdnergedaechtnis(ordnergedaechtnis);

            WerkzeugeStatusText.Text = James.RundgangZusammenfassung(wurdeAbgebrochen, zaehlerProTyp);

            RundgangStartenButton.IsEnabled = true;
            ComputerKennenlernenButton.IsEnabled = true;
            AbbrechenRundgangButton.Visibility = Visibility.Collapsed;
        }

        private void AbbrechenRundgang_Click(object sender, RoutedEventArgs e)
        {
            if (scanAbbrechenQuelle != null)
            {
                scanAbbrechenQuelle.Cancel();
            }
        }

        private void ScanneOrdner(string pfad, Dictionary<string, int> zaehler, List<GefundeneDatei> gefundeneDateien, int[] gesamtZaehler, CancellationToken abbrechenToken, IProgress<int> fortschritt)
        {
            abbrechenToken.ThrowIfCancellationRequested();

            try
            {
                foreach (string datei in Directory.EnumerateFiles(pfad))
                {
                    abbrechenToken.ThrowIfCancellationRequested();

                    try
                    {
                        FileInfo info = new FileInfo(datei);
                        string dateityp = ErmittleDateityp(info.Extension);

                        string hashwert = null;

                        try
                        {
                            hashwert = BerechneHashwert(datei);
                        }
                        catch
                        {
                        }

                        if (!zaehler.ContainsKey(dateityp))
                        {
                            zaehler[dateityp] = 0;
                        }

                        zaehler[dateityp] = zaehler[dateityp] + 1;

                        gefundeneDateien.Add(new GefundeneDatei
                        {
                            Dateiname = info.Name,
                            VollstaendigerPfad = info.FullName,
                            GroesseInBytes = info.Length,
                            Geaendert = info.LastWriteTime,
                            Dateityp = dateityp,
                            Hashwert = hashwert
                        });

                        gesamtZaehler[0] = gesamtZaehler[0] + 1;

                        if (gesamtZaehler[0] % 50 == 0)
                        {
                            fortschritt.Report(gesamtZaehler[0]);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }

            try
            {
                foreach (string unterordner in Directory.EnumerateDirectories(pfad))
                {
                    ScanneOrdner(unterordner, zaehler, gefundeneDateien, gesamtZaehler, abbrechenToken, fortschritt);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
        }

        private static string BerechneHashwert(string dateipfad)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                using (FileStream stream = File.OpenRead(dateipfad))
                {
                    byte[] hashBytes = sha256.ComputeHash(stream);
                    return Convert.ToBase64String(hashBytes);
                }
            }
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
        // BUILD 2.1: DIE ARBEITSMAPPE
        // ============================================================

        private void HauptTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != HauptTabControl)
            {
                return;
            }

            if (HauptTabControl.SelectedIndex == ArbeitsmappeTabIndex)
            {
                OeffneArbeitsmappe();
            }
        }

        private void ArbeitsmappeOeffnenButton_Click(object sender, RoutedEventArgs e)
        {
            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;
        }

        private int arbeitsmappeOeffnenZaehler = 0;

        private void OeffneArbeitsmappe()
        {
            arbeitsmappeOeffnenZaehler++;
            ArbeitsmappeDebugText.Text = "🐞 OeffneArbeitsmappe() ausgeführt - Aufruf Nr. " + arbeitsmappeOeffnenZaehler + " um " + DateTime.Now.ToString("HH:mm:ss.fff");

            arbeitsmappeAlleDateien = LadeErinnerungsverzeichnisDateien();
            arbeitsmappeBereitsZugeordnet = LadeArbeitsmappeZugeordnet();
            arbeitsmappeFilter = "Alle";
            arbeitsmappeSeite = 1;
            arbeitsmappeAusgewaehlt.Clear();
            arbeitsmappeNeuesEreignisPerson = null;
            arbeitsmappeLetztesEreignisPerson = null;
            arbeitsmappeLetztesEreignis = null;
            ArbeitsmappeEreignisOeffnenButton.Visibility = Visibility.Collapsed;

            if (ArbeitsmappeSucheTextBox != null)
            {
                ArbeitsmappeSucheTextBox.Text = "";
            }

            AktualisiereArbeitsmappenFilterButtons();
            AktualisiereArbeitsmappe();
        }

        private HashSet<string> LadeArbeitsmappeZugeordnet()
        {
            try
            {
                if (File.Exists(ArbeitsmappeZugeordnetPfad))
                {
                    string json = File.ReadAllText(ArbeitsmappeZugeordnetPfad);
                    List<string> geladen = JsonSerializer.Deserialize<List<string>>(json);

                    if (geladen != null)
                    {
                        return new HashSet<string>(geladen);
                    }
                }
            }
            catch
            {
            }

            return new HashSet<string>();
        }

        private void SpeichereArbeitsmappeZugeordnet()
        {
            try
            {
                Directory.CreateDirectory(OrdnerPfad);

                JsonSerializerOptions optionen = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(arbeitsmappeBereitsZugeordnet.ToList(), optionen);

                File.WriteAllText(ArbeitsmappeZugeordnetPfad, json);
            }
            catch
            {
            }
        }

        private List<GefundeneDatei> LadeErinnerungsverzeichnisDateien()
        {
            try
            {
                if (File.Exists(ErinnerungsVerzeichnisPfad))
                {
                    string json = File.ReadAllText(ErinnerungsVerzeichnisPfad);
                    ErinnerungsVerzeichnis verzeichnis = JsonSerializer.Deserialize<ErinnerungsVerzeichnis>(json);

                    if (verzeichnis != null && verzeichnis.Dateien != null)
                    {
                        return verzeichnis.Dateien;
                    }
                }
            }
            catch
            {
            }

            return new List<GefundeneDatei>();
        }

        private List<GefundeneDatei> ArbeitsmappeGefilterteDateien()
        {
            IEnumerable<GefundeneDatei> ergebnis = arbeitsmappeAlleDateien;

            if (arbeitsmappeFilter != "Alle")
            {
                ergebnis = ergebnis.Where(d => d.Dateityp == arbeitsmappeFilter);
            }

            string suchtext = ArbeitsmappeSucheTextBox.Text.Trim().ToLower();

            if (suchtext != "")
            {
                ergebnis = ergebnis.Where(d =>
                    (d.Dateiname != null && d.Dateiname.ToLower().Contains(suchtext)) ||
                    (d.VollstaendigerPfad != null && d.VollstaendigerPfad.ToLower().Contains(suchtext)));
            }

            return ergebnis.OrderBy(d => d.Dateiname).ToList();
        }

        private void ArbeitsmappeFilter_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;

            if (button == null)
            {
                return;
            }

            arbeitsmappeFilter = button.Tag.ToString();
            arbeitsmappeSeite = 1;

            AktualisiereArbeitsmappenFilterButtons();
            AktualisiereArbeitsmappe();
        }

        private void ArbeitsmappeSucheTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            arbeitsmappeSeite = 1;
            AktualisiereArbeitsmappe();
        }

        private void AktualisiereArbeitsmappenFilterButtons()
        {
            Button[] alleButtons =
            {
                ArbeitsmappeFilterAlleButton,
                ArbeitsmappeFilterBilderButton,
                ArbeitsmappeFilterDokumenteButton,
                ArbeitsmappeFilterPdfButton,
                ArbeitsmappeFilterVideosButton,
                ArbeitsmappeFilterAudioButton
            };

            foreach (Button button in alleButtons)
            {
                bool istAktiv = button.Tag.ToString() == arbeitsmappeFilter;
                button.FontWeight = istAktiv ? FontWeights.Bold : FontWeights.Normal;
                button.Background = istAktiv ? new SolidColorBrush(Color.FromRgb(0xE3, 0xF0, 0xDF)) : Brushes.White;
            }
        }

        private void AktualisiereArbeitsmappe()
        {
            List<GefundeneDatei> gefiltert = ArbeitsmappeGefilterteDateien();

            int gesamtSeiten = Math.Max(1, (int)Math.Ceiling(gefiltert.Count / (double)ArbeitsmappeProSeite));

            if (arbeitsmappeSeite > gesamtSeiten)
            {
                arbeitsmappeSeite = gesamtSeiten;
            }

            if (arbeitsmappeSeite < 1)
            {
                arbeitsmappeSeite = 1;
            }

            List<GefundeneDatei> seite = gefiltert
                .Skip((arbeitsmappeSeite - 1) * ArbeitsmappeProSeite)
                .Take(ArbeitsmappeProSeite)
                .ToList();

            ArbeitsmappeKachelnPanel.Children.Clear();

            foreach (GefundeneDatei datei in seite)
            {
                ArbeitsmappeKachelnPanel.Children.Add(ErstelleArbeitsmappenKachel(datei));
            }

            ArbeitsmappeUeberschriftText.Text = James.ArbeitsmappeUeberschrift(arbeitsmappeAlleDateien.Count);
            ArbeitsmappeSeiteText.Text = "Seite " + arbeitsmappeSeite + " von " + gesamtSeiten;
            ArbeitsmappeVorherigeSeiteButton.IsEnabled = arbeitsmappeSeite > 1;
            ArbeitsmappeNaechsteSeiteButton.IsEnabled = arbeitsmappeSeite < gesamtSeiten;

            AktualisiereArbeitsmappenWerkzeuge();
        }

        private void ArbeitsmappeVorherigeSeite_Click(object sender, RoutedEventArgs e)
        {
            arbeitsmappeSeite--;
            AktualisiereArbeitsmappe();
        }

        private void ArbeitsmappeNaechsteSeite_Click(object sender, RoutedEventArgs e)
        {
            arbeitsmappeSeite++;
            AktualisiereArbeitsmappe();
        }

        private static string ArbeitsmappeSymbolFuerDateityp(string dateityp)
        {
            switch (dateityp)
            {
                case "Bilder": return "🖼️";
                case "Videos": return "🎬";
                case "Audio": return "🎵";
                case "PDF": return "📄";
                case "Dokumente": return "📝";
                case "Textdateien": return "📃";
                default: return "📦";
            }
        }

        private Border ErstelleArbeitsmappenKachel(GefundeneDatei datei)
        {
            Border rahmen = new Border
            {
                Width = 150,
                Height = 200,
                Margin = new Thickness(6),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                CornerRadius = new CornerRadius(4)
            };

            StackPanel inhalt = new StackPanel
            {
                Margin = new Thickness(8)
            };

            CheckBox auswahlBox = new CheckBox
            {
                IsChecked = arbeitsmappeAusgewaehlt.Contains(datei.VollstaendigerPfad),
                Margin = new Thickness(0, 0, 0, 6)
            };

            auswahlBox.Checked += (sender, e) =>
            {
                arbeitsmappeAusgewaehlt.Add(datei.VollstaendigerPfad);
                AktualisiereArbeitsmappenWerkzeuge();
            };

            auswahlBox.Unchecked += (sender, e) =>
            {
                arbeitsmappeAusgewaehlt.Remove(datei.VollstaendigerPfad);
                AktualisiereArbeitsmappenWerkzeuge();
            };

            inhalt.Children.Add(auswahlBox);

            Border bildRahmen = new Border
            {
                Width = 130,
                Height = 100,
                Background = Brushes.WhiteSmoke,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            bool dateiVorhanden = File.Exists(datei.VollstaendigerPfad);

            if (dateiVorhanden && datei.Dateityp == "Bilder")
            {
                try
                {
                    BitmapImage bild = new BitmapImage();
                    bild.BeginInit();
                    bild.CacheOption = BitmapCacheOption.OnLoad;
                    bild.DecodePixelWidth = 220;
                    bild.UriSource = new Uri(datei.VollstaendigerPfad);
                    bild.EndInit();

                    bildRahmen.Child = new Image
                    {
                        Source = bild,
                        Stretch = Stretch.Uniform
                    };
                }
                catch
                {
                    bildRahmen.Child = ErstelleArbeitsmappenSymbol("🖼️");
                }
            }
            else
            {
                bildRahmen.Child = ErstelleArbeitsmappenSymbol(ArbeitsmappeSymbolFuerDateityp(datei.Dateityp));
            }

            inhalt.Children.Add(bildRahmen);

            string statusTextInhalt;

            if (!dateiVorhanden)
            {
                statusTextInhalt = James.ArbeitsmappeDateiNichtGefunden;
            }
            else if (arbeitsmappeBereitsZugeordnet.Contains(datei.VollstaendigerPfad))
            {
                statusTextInhalt = James.ArbeitsmappeBereitsZugeordnet;
            }
            else
            {
                statusTextInhalt = James.ArbeitsmappeKeinemZugeordnet;
            }

            TextBlock statusText = new TextBlock
            {
                Text = statusTextInhalt,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                FontSize = 11,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 8, 0, 0)
            };

            inhalt.Children.Add(statusText);

            rahmen.Child = inhalt;

            rahmen.MouseLeftButtonDown += (sender, e) =>
            {
                if (e.ClickCount == 2)
                {
                    OeffneErinnerungGross(datei);
                }
            };

            return rahmen;
        }

        private static TextBlock ErstelleArbeitsmappenSymbol(string symbol)
        {
            return new TextBlock
            {
                Text = symbol,
                FontSize = 40,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private void OeffneErinnerungGross(GefundeneDatei datei)
        {
            if (!File.Exists(datei.VollstaendigerPfad))
            {
                James.Hinweis(James.ArbeitsmappeDateiNichtGefunden);
                return;
            }

            if (datei.Dateityp == "Bilder")
            {
                try
                {
                    BitmapImage bild = new BitmapImage();
                    bild.BeginInit();
                    bild.CacheOption = BitmapCacheOption.OnLoad;
                    bild.UriSource = new Uri(datei.VollstaendigerPfad);
                    bild.EndInit();

                    ArbeitsmappeGrossBild.Source = bild;
                    ArbeitsmappeGrossansichtPanel.Visibility = Visibility.Visible;
                }
                catch (Exception ex)
                {
                    James.Problem(James.FehlerBeimOeffnenDerErinnerung(ex.Message));
                }

                return;
            }

            try
            {
                ProcessStartInfo start = new ProcessStartInfo
                {
                    FileName = datei.VollstaendigerPfad,
                    UseShellExecute = true
                };

                Process.Start(start);
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimOeffnenDerErinnerung(ex.Message));
            }
        }

        private void ArbeitsmappeGrossansichtSchliessen_Click(object sender, RoutedEventArgs e)
        {
            ArbeitsmappeGrossansichtPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeGrossBild.Source = null;
        }

        private void AktualisiereArbeitsmappenWerkzeuge()
        {
            int anzahl = arbeitsmappeAusgewaehlt.Count;

            ArbeitsmappeAuswahlText.Text = James.ArbeitsmappeAuswahlText(anzahl);

            ArbeitsmappeNeuesEreignisAnlegenButton.IsEnabled = anzahl > 0;
            ArbeitsmappeMitEreignisVerbindenButton.IsEnabled = anzahl > 0;
            ArbeitsmappeNeuePersonAnlegenButton.IsEnabled = anzahl > 0;
            ArbeitsmappePersonZuordnenButton.IsEnabled = anzahl > 0;
            ArbeitsmappeMarkierungAufhebenButton.IsEnabled = anzahl > 0;

            ArbeitsmappeNeuesFreiesEreignisButton.IsEnabled = anzahl > 0;
            ArbeitsmappeFreiesEreignisZuordnenButton.IsEnabled = anzahl > 0;

            if (anzahl == 0)
            {
                VersteckeAlleArbeitsmappenPanels();
            }
        }

        private void ArbeitsmappeMarkierungAufheben_Click(object sender, RoutedEventArgs e)
        {
            arbeitsmappeAusgewaehlt.Clear();
            AktualisiereArbeitsmappe();
        }

        private GefundeneDatei ArbeitsmappeEinzigAusgewaehlteDatei()
        {
            string pfad = arbeitsmappeAusgewaehlt.FirstOrDefault();

            if (pfad == null)
            {
                return null;
            }

            return arbeitsmappeAlleDateien.FirstOrDefault(d => d.VollstaendigerPfad == pfad);
        }

        private void ArbeitsmappeMitEreignisVerbinden_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            List<Person> alleAuswaehlbarenPersonen = allePersonen
                .Concat(ArchivListe.Items.Cast<Person>())
                .ToList();

            ArbeitsmappePersonComboBox.ItemsSource = alleAuswaehlbarenPersonen;
            ArbeitsmappePersonComboBox.SelectedIndex = -1;
            ArbeitsmappeEreignisComboBox.ItemsSource = null;

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeEreignisAuswahlPanel.Visibility = Visibility.Visible;
        }

        private void ArbeitsmappePersonComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Person person = ArbeitsmappePersonComboBox.SelectedItem as Person;
            ArbeitsmappeEreignisComboBox.ItemsSource = person != null ? person.Ereignisse : null;

            if (person != null)
            {
                int anzahlEreignisse = person.Ereignisse != null ? person.Ereignisse.Count : 0;
                ArbeitsmappeStatusText.Text = James.DiagnosePersonAusgewaehltFuerEreignis(person.ToString(), anzahlEreignisse);
            }
        }

        private void ArbeitsmappeEreignisBestaetigen_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArbeitsmappePersonComboBox.SelectedItem as Person;
            Ereignis ereignis = ArbeitsmappeEreignisComboBox.SelectedItem as Ereignis;

            if (person == null || ereignis == null)
            {
                James.Hinweis(James.BittePersonUndEreignisAuswaehlen);
                return;
            }

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();

            VerknuepfeArbeitsmappenDateienMitEreignis(person, ereignis, pfade);

            ArbeitsmappeEreignisAuswahlPanel.Visibility = Visibility.Collapsed;
            AktualisiereArbeitsmappe();
        }

        private int VerbindeDateienMitEreignis(Ereignis ereignis, List<string> pfade, string zielOrdner)
        {
            int verbunden = 0;

            Directory.CreateDirectory(zielOrdner);

            foreach (string pfad in pfade)
            {
                GefundeneDatei datei = arbeitsmappeAlleDateien.FirstOrDefault(d => d.VollstaendigerPfad == pfad);

                if (datei == null || datei.Dateityp != "Bilder" || !File.Exists(datei.VollstaendigerPfad))
                {
                    continue;
                }

                string dateiendung = Path.GetExtension(datei.VollstaendigerPfad);
                string neuerDateiname = Guid.NewGuid().ToString() + dateiendung;
                string zielPfad = Path.Combine(zielOrdner, neuerDateiname);

                File.Copy(datei.VollstaendigerPfad, zielPfad, true);

                if (string.IsNullOrEmpty(ereignis.EreignisFotoDateiname))
                {
                    ereignis.EreignisFotoDateiname = neuerDateiname;
                }
                else
                {
                    if (ereignis.WeitereFotoDateinamen == null)
                    {
                        ereignis.WeitereFotoDateinamen = new List<string>();
                    }

                    ereignis.WeitereFotoDateinamen.Add(neuerDateiname);
                }

                arbeitsmappeAusgewaehlt.Remove(pfad);
                arbeitsmappeBereitsZugeordnet.Add(pfad);
                verbunden++;
            }

            if (verbunden > 0)
            {
                ereignis.ModifiedAt = DateTime.Now;
            }

            return verbunden;
        }

        private void VerknuepfeArbeitsmappenDateienMitEreignis(Person person, Ereignis ereignis, List<string> pfade)
        {
            if (person == null || ereignis == null || pfade == null || pfade.Count == 0)
            {
                return;
            }

            try
            {
                int verbunden = VerbindeDateienMitEreignis(ereignis, pfade, ErinnerungsOrdnerFuer(person, ereignis));

                if (verbunden > 0)
                {
                    person.ModifiedAt = DateTime.Now;
                    SpeichereDaten();
                    SpeichereArbeitsmappeZugeordnet();
                }

                if (verbunden == 0)
                {
                    James.Hinweis(James.ArbeitsmappeNurBilder);
                }
                else
                {
                    int gesamtErinnerungen = ZaehleErinnerungenDerPerson(person);

                    ArbeitsmappeStatusText.Text = verbunden == 1
                        ? James.ArbeitsmappeVerbunden(ereignis.Titel, person.ToString(), gesamtErinnerungen)
                        : James.ArbeitsmappeVerbundenMehrere(verbunden, ereignis.Titel, person.ToString(), gesamtErinnerungen);

                    ZeigeArbeitsmappeEreignisOeffnenButton(person, ereignis);
                }

                if (verbunden > 0)
                {
                    AktualisiereErinnerungskarteFallsBetroffen(person, ereignis);
                    AktualisiereSchreibtischFallsBetroffen(person);
                }
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimSpeichernFoto(ex.Message));
            }
        }

        private void ArbeitsmappeNeuesEreignisAnlegen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            if (allePersonen.Count == 0)
            {
                James.Hinweis(James.ArbeitsmappeBittePersonZuerst);
                return;
            }

            ArbeitsmappeNeuesEreignisPersonComboBox.ItemsSource = allePersonen;
            ArbeitsmappeNeuesEreignisPersonComboBox.SelectedIndex = -1;

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeNeuesEreignisPersonPanel.Visibility = Visibility.Visible;
        }

        private void ArbeitsmappeNeuesEreignisWeiter_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArbeitsmappeNeuesEreignisPersonComboBox.SelectedItem as Person;

            if (person == null)
            {
                James.Hinweis(James.ArbeitsmappeBitteEreignisPersonAuswaehlen);
                return;
            }

            arbeitsmappeNeuesEreignisPerson = person;

            ArbeitsmappeEreignisTitelTextBox.Clear();
            ArbeitsmappeEreignisBeschreibungTextBox.Clear();
            ArbeitsmappeEreignisDatumTextBox.Clear();
            ArbeitsmappeEreignisOrtTextBox.Clear();
            ArbeitsmappeEreignisJahreszeitComboBox.SelectedIndex = 0;
            ArbeitsmappeEreignisStichwoerterTextBox.Clear();
            ArbeitsmappeEreignisBemerkungenTextBox.Clear();
            ArbeitsmappeEreignisBeteiligteTextBox.Clear();

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeNeuesEreignisFormularPanel.Visibility = Visibility.Visible;
            ArbeitsmappeEreignisTitelTextBox.Focus();
        }

        private void ArbeitsmappeEreignisSpeichern_Click(object sender, RoutedEventArgs e)
        {
            Person person = arbeitsmappeNeuesEreignisPerson;

            if (person == null)
            {
                James.Hinweis(James.ArbeitsmappeBitteEreignisPersonAuswaehlen);
                return;
            }

            string titel = ArbeitsmappeEreignisTitelTextBox.Text.Trim();

            if (titel == "")
            {
                James.Hinweis(James.BitteEreignisTitelEingeben);
                return;
            }

            string jahreszeit = "";
            ComboBoxItem ausgewaehlteJahreszeit = ArbeitsmappeEreignisJahreszeitComboBox.SelectedItem as ComboBoxItem;

            if (ausgewaehlteJahreszeit != null)
            {
                jahreszeit = ausgewaehlteJahreszeit.Content.ToString();
            }

            List<string> stichwoerter = ArbeitsmappeEreignisStichwoerterTextBox.Text
                .Split(',')
                .Select(teil => teil.Trim())
                .Where(teil => teil != "")
                .ToList();

            List<string> beteiligte = ArbeitsmappeEreignisBeteiligteTextBox.Text
                .Split(',')
                .Select(teil => teil.Trim())
                .Where(teil => teil != "")
                .ToList();

            Ereignis neuesEreignis = new Ereignis
            {
                Titel = titel,
                Beschreibung = ArbeitsmappeEreignisBeschreibungTextBox.Text.Trim(),
                Datum = ArbeitsmappeEreignisDatumTextBox.Text.Trim(),
                Ort = ArbeitsmappeEreignisOrtTextBox.Text.Trim(),
                Jahreszeit = jahreszeit,
                Stichwoerter = stichwoerter,
                Bemerkungen = ArbeitsmappeEreignisBemerkungenTextBox.Text.Trim(),
                Beteiligte = beteiligte
            };

            if (person.Ereignisse == null)
            {
                person.Ereignisse = new List<Ereignis>();
            }

            person.Ereignisse.Add(neuesEreignis);
            person.ModifiedAt = DateTime.Now;

            SpeichereDaten();

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();
            int vorherAusgewaehlt = arbeitsmappeAusgewaehlt.Count;

            VerknuepfeArbeitsmappenDateienMitEreignis(person, neuesEreignis, pfade);

            bool wurdeFotoVerbunden = arbeitsmappeAusgewaehlt.Count < vorherAusgewaehlt;

            if (!wurdeFotoVerbunden)
            {
                int gesamtErinnerungen = ZaehleErinnerungenDerPerson(person);
                ArbeitsmappeStatusText.Text = James.ArbeitsmappeEreignisAngelegtUndVerbunden(neuesEreignis.Titel, person.ToString(), gesamtErinnerungen);
            }

            ZeigeArbeitsmappeEreignisOeffnenButton(person, neuesEreignis);

            arbeitsmappeNeuesEreignisPerson = null;

            ArbeitsmappeNeuesEreignisFormularPanel.Visibility = Visibility.Collapsed;

            AktualisiereArbeitsmappe();
        }

        private void ArbeitsmappeNeuePersonAnlegen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            ArbeitsmappeNeuePersonVornameTextBox.Clear();
            ArbeitsmappeNeuePersonNachnameTextBox.Clear();
            ArbeitsmappeNeuePersonGeburtTextBox.Clear();
            ArbeitsmappeNeuePersonOrtTextBox.Clear();
            ArbeitsmappeNeuePersonBeziehungComboBox.Text = "";

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeNeuePersonPanel.Visibility = Visibility.Visible;
            ArbeitsmappeNeuePersonVornameTextBox.Focus();
        }

        private void ArbeitsmappeNeuePersonSpeichern_Click(object sender, RoutedEventArgs e)
        {
            string vorname = ArbeitsmappeNeuePersonVornameTextBox.Text.Trim();
            string nachname = ArbeitsmappeNeuePersonNachnameTextBox.Text.Trim();

            if (vorname == "" && nachname == "")
            {
                James.Hinweis(James.BitteErstNamenEingeben);
                return;
            }

            string beziehungstext = ArbeitsmappeNeuePersonBeziehungComboBox.Text != null
                ? ArbeitsmappeNeuePersonBeziehungComboBox.Text.Trim()
                : "";

            Person neuePerson = new Person
            {
                Vorname = vorname,
                Nachname = nachname,
                Geburt = ArbeitsmappeNeuePersonGeburtTextBox.Text.Trim(),
                Ort = ArbeitsmappeNeuePersonOrtTextBox.Text.Trim(),
                Beziehung = beziehungstext == "" ? null : new Beziehung { Rolle = beziehungstext }
            };

            allePersonen.Add(neuePerson);

            SortiereAllePersonen();
            AktualisierePersonenAnzeige();

            SpeichereDaten();

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();

            VerknuepfeArbeitsmappenDateienMitPerson(neuePerson, pfade);

            ArbeitsmappeNeuePersonPanel.Visibility = Visibility.Collapsed;

            AktualisiereArbeitsmappe();
        }

        // ============================================================
        // BUILD 5.1: DIE STARTSEITE ALS ZENTRALER EINSTIEGSPUNKT
        // ============================================================

        private void StartseitePersonButton_Click(object sender, RoutedEventArgs e)
        {
            StartseiteBereich.Visibility = Visibility.Collapsed;
            EreignisBereich.Visibility = Visibility.Collapsed;
            EreignismappeBereich.Visibility = Visibility.Collapsed;

            PersonenFormularBereich.Visibility = Visibility.Visible;
            PersonenListeBereich.Visibility = Visibility.Visible;
        }

        private void StartseiteEreignisButton_Click(object sender, RoutedEventArgs e)
        {
            StartseiteBereich.Visibility = Visibility.Collapsed;
            PersonenFormularBereich.Visibility = Visibility.Collapsed;
            PersonenListeBereich.Visibility = Visibility.Collapsed;
            EreignismappeBereich.Visibility = Visibility.Collapsed;

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

            StartseiteBereich.Visibility = Visibility.Visible;

            ZeigeStartseiteVorschlag();
        }

        // ============================================================
        // BUILD 5.1: EREIGNISVERWALTUNG
        // ============================================================

        private void EreignisListeSchreibtisch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Ereignis ereignis = EreignisListeSchreibtisch.SelectedItem as Ereignis;

            aktuellBearbeitetesFreiesEreignis = ereignis;

            if (ereignis == null)
            {
                EreignisTitelSchreibtischTextBox.Clear();
                FreiesEreignisErinnerungenLinkText.Visibility = Visibility.Collapsed;
                EreignisSchreibtischArchivierenButton.Visibility = Visibility.Collapsed;
                return;
            }

            EreignisTitelSchreibtischTextBox.Text = ereignis.Titel;

            int anzahl = (!string.IsNullOrEmpty(ereignis.EreignisFotoDateiname) ? 1 : 0)
                + (ereignis.WeitereFotoDateinamen != null ? ereignis.WeitereFotoDateinamen.Count : 0);

            FreiesEreignisErinnerungenLinkText.Text = James.PersonErinnerungenLink(anzahl);
            FreiesEreignisErinnerungenLinkText.Tag = anzahl > 0 ? ereignis : null;
            FreiesEreignisErinnerungenLinkText.Visibility = Visibility.Visible;

            EreignisSchreibtischArchivierenButton.Visibility = Visibility.Visible;
        }

        private void EreignisSchreibtischSpeichern_Click(object sender, RoutedEventArgs e)
        {
            string titel = EreignisTitelSchreibtischTextBox.Text.Trim();

            if (titel == "")
            {
                James.Hinweis(James.BitteEreignisTitelEingeben);
                return;
            }

            if (aktuellBearbeitetesFreiesEreignis != null)
            {
                aktuellBearbeitetesFreiesEreignis.Titel = titel;
                aktuellBearbeitetesFreiesEreignis.ModifiedAt = DateTime.Now;

                SpeichereDaten();

                ZeigeStatusMeldung(James.FreiesEreignisAktualisiert(titel));

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
            else
            {
                Ereignis neuesEreignis = new Ereignis { Titel = titel };

                freieEreignisse.Add(neuesEreignis);

                SpeichereDaten();
                AktualisiereFreieEreignisseAnzeige();

                EreignisTitelSchreibtischTextBox.Clear();

                ZeigeEreignismappe(neuesEreignis);
            }
        }

        // ============================================================
        // BUILD 6.0: DIE EREIGNISMAPPE
        // ============================================================

        private Ereignis aktuelleEreignismappe = null;

        private void ZeigeEreignismappe(Ereignis ereignis)
        {
            aktuelleEreignismappe = ereignis;

            EreignisBereich.Visibility = Visibility.Collapsed;
            EreignismappeBereich.Visibility = Visibility.Visible;

            EreignismappeTitelText.Text = James.EreignismappeAngelegt(ereignis.Titel);
        }

        private void EreignismappeBilder_Click(object sender, RoutedEventArgs e)
        {
            if (aktuelleEreignismappe == null)
            {
                return;
            }

            string ereignisTitel = aktuelleEreignismappe.Titel;

            EreignismappeBereich.Visibility = Visibility.Collapsed;

            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;

            James.Hinweis(James.EreignismappeBilderHinweis(ereignisTitel));
        }

        private void EreignismappeZurueck_Click(object sender, MouseButtonEventArgs e)
        {
            EreignismappeBereich.Visibility = Visibility.Collapsed;
            EreignisBereich.Visibility = Visibility.Visible;
        }

        private void FreiesEreignisErinnerungenLinkText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Ereignis ereignis = FreiesEreignisErinnerungenLinkText.Tag as Ereignis;

            if (ereignis == null)
            {
                return;
            }

            List<ErinnerungsInfo> erinnerungenListe = SammelErinnerungenFuerEreignis(null, ereignis);

            if (erinnerungenListe.Count == 0)
            {
                return;
            }

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelEreignis(ereignis.Titel), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal);
            fenster.Owner = this;
            fenster.Show();
        }

        private void EreignisSchreibtischArchivieren_Click(object sender, RoutedEventArgs e)
        {
            Ereignis ereignis = EreignisListeSchreibtisch.SelectedItem as Ereignis;

            if (ereignis == null)
            {
                return;
            }

            freieEreignisse.Remove(ereignis);
            freieEreignisseArchiv.Add(ereignis);

            SpeichereDaten();

            aktuellBearbeitetesFreiesEreignis = null;
            EreignisTitelSchreibtischTextBox.Clear();
            FreiesEreignisErinnerungenLinkText.Visibility = Visibility.Collapsed;
            EreignisSchreibtischArchivierenButton.Visibility = Visibility.Collapsed;

            AktualisiereFreieEreignisseAnzeige();

            ZeigeStatusMeldung(James.FreiesEreignisArchiviert(ereignis.Titel));
        }

        // ============================================================
        // BUILD 5.0: ERINNERUNGEN ÜBER PERSONEN ODER EREIGNISSE
        // ============================================================

        private void ArbeitsmappeNeuesFreiesEreignisAnlegen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            FreiesEreignisTitelTextBox.Clear();

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeNeuesFreiesEreignisPanel.Visibility = Visibility.Visible;
            FreiesEreignisTitelTextBox.Focus();
        }

        private void FreiesEreignisSpeichernUndZuordnen_Click(object sender, RoutedEventArgs e)
        {
            string titel = FreiesEreignisTitelTextBox.Text.Trim();

            if (titel == "")
            {
                James.Hinweis(James.BitteEreignisTitelEingeben);
                return;
            }

            Ereignis bestehendesEreignis = freieEreignisse
                .Concat(freieEreignisseArchiv)
                .FirstOrDefault(ereignis => string.Equals((ereignis.Titel ?? "").Trim(), titel, StringComparison.OrdinalIgnoreCase));

            Ereignis zielEreignis;

            if (bestehendesEreignis != null && James.FrageJaNein(James.FrageEreignisBereitsVorhanden(titel), James.TitelEntscheidung))
            {
                zielEreignis = bestehendesEreignis;
            }
            else
            {
                zielEreignis = new Ereignis { Titel = titel };
                freieEreignisse.Add(zielEreignis);
            }

            SpeichereDaten();
            AktualisiereFreieEreignisseAnzeige();

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();
            VerknuepfeArbeitsmappenDateienMitFreiemEreignis(zielEreignis, pfade);

            ArbeitsmappeNeuesFreiesEreignisPanel.Visibility = Visibility.Collapsed;
            AktualisiereArbeitsmappe();
        }

        private void ArbeitsmappeFreiesEreignisZuordnen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            if (freieEreignisse.Count == 0 && freieEreignisseArchiv.Count == 0)
            {
                James.Hinweis(James.BitteErstFreiesEreignisAnlegen);
                return;
            }

            List<Ereignis> alleAuswaehlbaren = freieEreignisse.Concat(freieEreignisseArchiv).ToList();

            FreiesEreignisComboBox.ItemsSource = alleAuswaehlbaren;
            FreiesEreignisComboBox.SelectedIndex = -1;

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeFreiesEreignisAuswahlPanel.Visibility = Visibility.Visible;
        }

        private void FreiesEreignisBestaetigen_Click(object sender, RoutedEventArgs e)
        {
            Ereignis ereignis = FreiesEreignisComboBox.SelectedItem as Ereignis;

            if (ereignis == null)
            {
                James.Hinweis(James.BitteEreignisAuswaehlen);
                return;
            }

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();
            VerknuepfeArbeitsmappenDateienMitFreiemEreignis(ereignis, pfade);

            ArbeitsmappeFreiesEreignisAuswahlPanel.Visibility = Visibility.Collapsed;
            AktualisiereArbeitsmappe();
        }

        private void VerknuepfeArbeitsmappenDateienMitFreiemEreignis(Ereignis ereignis, List<string> pfade)
        {
            if (ereignis == null || pfade == null || pfade.Count == 0)
            {
                return;
            }

            try
            {
                int verbunden = VerbindeDateienMitEreignis(ereignis, pfade, ErinnerungsOrdnerFuer(null, ereignis));

                if (verbunden > 0)
                {
                    SpeichereDaten();
                    SpeichereArbeitsmappeZugeordnet();
                    AktualisiereFreieEreignisseAnzeige();
                }

                if (verbunden == 0)
                {
                    James.Hinweis(James.ArbeitsmappeNurBilder);
                }
                else if (verbunden == 1)
                {
                    ArbeitsmappeStatusText.Text = James.FreiesEreignisVerbunden(ereignis.Titel);
                }
                else
                {
                    ArbeitsmappeStatusText.Text = James.FreiesEreignisVerbundenMehrere(verbunden, ereignis.Titel);
                }
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimSpeichernFoto(ex.Message));
            }
        }

        private void AktualisiereFreieEreignisseAnzeige()
        {
            object ausgewaehltArbeitsmappe = FreieEreignisseListe.SelectedItem;
            object ausgewaehltSchreibtisch = EreignisListeSchreibtisch.SelectedItem;
            object ausgewaehltArchiv = ArchivEreignisseListe.SelectedItem;
            object ausgewaehltPapierkorb = FreieEreignissePapierkorbListe.SelectedItem;

            FreieEreignisseListe.ItemsSource = null;
            FreieEreignisseListe.ItemsSource = freieEreignisse;

            EreignisListeSchreibtisch.ItemsSource = null;
            EreignisListeSchreibtisch.ItemsSource = freieEreignisse;

            ArchivEreignisseListe.ItemsSource = null;
            ArchivEreignisseListe.ItemsSource = freieEreignisseArchiv;

            FreieEreignissePapierkorbListe.ItemsSource = null;
            FreieEreignissePapierkorbListe.ItemsSource = freieEreignissePapierkorb;

            if (ausgewaehltArbeitsmappe != null && freieEreignisse.Contains(ausgewaehltArbeitsmappe))
            {
                FreieEreignisseListe.SelectedItem = ausgewaehltArbeitsmappe;
            }

            if (ausgewaehltSchreibtisch != null && freieEreignisse.Contains(ausgewaehltSchreibtisch))
            {
                EreignisListeSchreibtisch.SelectedItem = ausgewaehltSchreibtisch;
            }

            if (ausgewaehltArchiv != null && freieEreignisseArchiv.Contains(ausgewaehltArchiv))
            {
                ArchivEreignisseListe.SelectedItem = ausgewaehltArchiv;
            }

            if (ausgewaehltPapierkorb != null && freieEreignissePapierkorb.Contains(ausgewaehltPapierkorb))
            {
                FreieEreignissePapierkorbListe.SelectedItem = ausgewaehltPapierkorb;
            }
        }

        private void FreieEreignisseListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool istAusgewaehlt = FreieEreignisseListe.SelectedItem != null;

            FreiesEreignisErinnerungenAnsehenButton.IsEnabled = istAusgewaehlt;
            FreiesEreignisArchivierenButton.IsEnabled = istAusgewaehlt;
            FreiesEreignisInPapierkorbButton.IsEnabled = istAusgewaehlt;
        }

        private void FreiesEreignisInPapierkorb_Click(object sender, RoutedEventArgs e)
        {
            List<Ereignis> ausgewaehlteEreignisse = FreieEreignisseListe.SelectedItems.Cast<Ereignis>().ToList();

            if (ausgewaehlteEreignisse.Count == 0)
            {
                James.Hinweis(James.BitteFreieEreignisseAuswaehlen);
                return;
            }

            string frage = ausgewaehlteEreignisse.Count == 1
                ? James.FrageInPapierkorbEinzeln(ausgewaehlteEreignisse[0].Titel)
                : James.FrageInPapierkorbMehrere(ausgewaehlteEreignisse.Count);

            bool ergebnis = James.FrageJaNein(frage, James.TitelEntscheidung, MessageBoxImage.Warning);

            if (!ergebnis)
            {
                return;
            }

            foreach (Ereignis ereignis in ausgewaehlteEreignisse)
            {
                freieEreignisse.Remove(ereignis);
                freieEreignissePapierkorb.Add(ereignis);
            }

            SpeichereDaten();
            AktualisiereFreieEreignisseAnzeige();

            if (ausgewaehlteEreignisse.Count == 1)
            {
                ArbeitsmappeStatusText.Text = James.InPapierkorbGelegtEinzeln(ausgewaehlteEreignisse[0].Titel);
            }
            else
            {
                ArbeitsmappeStatusText.Text = James.InPapierkorbGelegtMehrere(ausgewaehlteEreignisse.Count);
            }
        }

        private void FreiesEreignisErinnerungenAnsehen_Click(object sender, RoutedEventArgs e)
        {
            Ereignis ereignis = FreieEreignisseListe.SelectedItem as Ereignis;

            if (ereignis == null)
            {
                return;
            }

            List<ErinnerungsInfo> erinnerungenListe = SammelErinnerungenFuerEreignis(null, ereignis);

            if (erinnerungenListe.Count == 0)
            {
                return;
            }

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelEreignis(ereignis.Titel), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal);
            fenster.Owner = this;
            fenster.Show();
        }

        private void FreiesEreignisArchivieren_Click(object sender, RoutedEventArgs e)
        {
            Ereignis ereignis = FreieEreignisseListe.SelectedItem as Ereignis;

            if (ereignis == null)
            {
                return;
            }

            freieEreignisse.Remove(ereignis);
            freieEreignisseArchiv.Add(ereignis);

            SpeichereDaten();
            AktualisiereFreieEreignisseAnzeige();

            ArbeitsmappeStatusText.Text = James.FreiesEreignisArchiviert(ereignis.Titel);
        }

        private void ArchivEreignisseListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ArchivEreignisAktionPanel.Visibility = Visibility.Collapsed;
        }

        private void ArchivEreignisAktion_Click(object sender, RoutedEventArgs e)
        {
            if (ArchivEreignisseListe.SelectedItem == null)
            {
                James.Hinweis(James.BitteFreieEreignisseAuswaehlen);
                return;
            }

            ArchivEreignisAktionPanel.Visibility = ArchivEreignisAktionPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void ArchivEreignisErinnerungenAnsehen_Click(object sender, RoutedEventArgs e)
        {
            Ereignis ereignis = ArchivEreignisseListe.SelectedItem as Ereignis;

            if (ereignis == null)
            {
                return;
            }

            List<ErinnerungsInfo> erinnerungenListe = SammelErinnerungenFuerEreignis(null, ereignis);

            if (erinnerungenListe.Count == 0)
            {
                return;
            }

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelEreignis(ereignis.Titel), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal);
            fenster.Owner = this;
            fenster.Show();
        }

        private void ArchivEreignisWiederherstellen_Click(object sender, RoutedEventArgs e)
        {
            Ereignis ereignis = ArchivEreignisseListe.SelectedItem as Ereignis;

            if (ereignis == null)
            {
                return;
            }

            freieEreignisseArchiv.Remove(ereignis);
            freieEreignisse.Add(ereignis);

            SpeichereDaten();
            AktualisiereFreieEreignisseAnzeige();
        }

        private void ArchivEreignisInPapierkorb_Click(object sender, RoutedEventArgs e)
        {
            List<Ereignis> ausgewaehlteEreignisse = ArchivEreignisseListe.SelectedItems.Cast<Ereignis>().ToList();

            if (ausgewaehlteEreignisse.Count == 0)
            {
                James.Hinweis(James.BitteFreieEreignisseAuswaehlen);
                return;
            }

            string frage = ausgewaehlteEreignisse.Count == 1
                ? James.FrageInPapierkorbEinzeln(ausgewaehlteEreignisse[0].Titel)
                : James.FrageInPapierkorbMehrere(ausgewaehlteEreignisse.Count);

            bool ergebnis = James.FrageJaNein(frage, James.TitelEntscheidung, MessageBoxImage.Warning);

            if (!ergebnis)
            {
                return;
            }

            foreach (Ereignis ereignis in ausgewaehlteEreignisse)
            {
                freieEreignisseArchiv.Remove(ereignis);
                freieEreignissePapierkorb.Add(ereignis);
            }

            SpeichereDaten();
            AktualisiereFreieEreignisseAnzeige();
        }

        private void FreieEreignissePapierkorbListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool istAusgewaehlt = FreieEreignissePapierkorbListe.SelectedItem != null;

            FreiesEreignisWiederherstellenButton.IsEnabled = istAusgewaehlt;
            FreiesEreignisEndgueltigLoeschenButton.IsEnabled = istAusgewaehlt;
        }

        private void FreiesEreignisWiederherstellen_Click(object sender, RoutedEventArgs e)
        {
            List<Ereignis> ausgewaehlteEreignisse = FreieEreignissePapierkorbListe.SelectedItems.Cast<Ereignis>().ToList();

            if (ausgewaehlteEreignisse.Count == 0)
            {
                James.Hinweis(James.BitteEreignisPapierkorbAuswaehlen);
                return;
            }

            foreach (Ereignis ereignis in ausgewaehlteEreignisse)
            {
                freieEreignissePapierkorb.Remove(ereignis);
                freieEreignisse.Add(ereignis);
            }

            SpeichereDaten();
            AktualisiereFreieEreignisseAnzeige();

            if (ausgewaehlteEreignisse.Count == 1)
            {
                James.Hinweis(James.WiederhergestelltEinzeln(ausgewaehlteEreignisse[0].Titel), James.TitelWiederhergestellt);
            }
            else
            {
                James.Hinweis(James.WiederhergestelltMehrere(ausgewaehlteEreignisse.Count), James.TitelWiederhergestellt);
            }
        }

        private void FreiesEreignisEndgueltigLoeschen_Click(object sender, RoutedEventArgs e)
        {
            List<Ereignis> ausgewaehlteEreignisse = FreieEreignissePapierkorbListe.SelectedItems.Cast<Ereignis>().ToList();

            if (ausgewaehlteEreignisse.Count == 0)
            {
                James.Hinweis(James.BitteEreignisPapierkorbAuswaehlen);
                return;
            }

            string frage = ausgewaehlteEreignisse.Count == 1
                ? James.FrageEndgueltigLoeschenEinzeln(ausgewaehlteEreignisse[0].Titel)
                : James.FrageEndgueltigLoeschenMehrere(ausgewaehlteEreignisse.Count);

            bool ergebnis = James.FrageJaNein(frage, James.TitelEndgueltigeEntscheidung, MessageBoxImage.Warning);

            if (ergebnis)
            {
                foreach (Ereignis ereignis in ausgewaehlteEreignisse)
                {
                    freieEreignissePapierkorb.Remove(ereignis);
                }

                SpeichereDaten();
                AktualisiereFreieEreignisseAnzeige();
            }
        }

        private void VersteckeAlleArbeitsmappenPanels()
        {
            ArbeitsmappeEreignisAuswahlPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappePersonAuswahlPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeNeuesEreignisPersonPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeNeuesEreignisFormularPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeNeuePersonPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeNeuesFreiesEreignisPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeFreiesEreignisAuswahlPanel.Visibility = Visibility.Collapsed;
        }

        private void ZeigeArbeitsmappeEreignisOeffnenButton(Person person, Ereignis ereignis)
        {
            arbeitsmappeLetztesEreignisPerson = person;
            arbeitsmappeLetztesEreignis = ereignis;
            ArbeitsmappeEreignisOeffnenButton.Visibility = Visibility.Visible;
        }

        private void ArbeitsmappeEreignisOeffnen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeLetztesEreignisPerson == null || arbeitsmappeLetztesEreignis == null)
            {
                return;
            }

            SucheTextBox.Text = "";

            HauptTabControl.SelectedIndex = 0;
            PersonenListe.SelectedItem = arbeitsmappeLetztesEreignisPerson;
            EreignisseListe.SelectedItem = arbeitsmappeLetztesEreignis;

            ArbeitsmappeEreignisOeffnenButton.Visibility = Visibility.Collapsed;
        }

        private void VerknuepfeArbeitsmappenDateienMitPerson(Person person, List<string> pfade)
        {
            if (person == null || pfade == null || pfade.Count == 0)
            {
                return;
            }

            int verbunden = 0;

            try
            {
                string zielOrdner = PersonErinnerungsOrdner(person);
                Directory.CreateDirectory(zielOrdner);

                if (person.ErinnerungsDateinamen == null)
                {
                    person.ErinnerungsDateinamen = new List<string>();
                }

                foreach (string pfad in pfade)
                {
                    GefundeneDatei datei = arbeitsmappeAlleDateien.FirstOrDefault(d => d.VollstaendigerPfad == pfad);

                    if (datei == null || datei.Dateityp != "Bilder" || !File.Exists(datei.VollstaendigerPfad))
                    {
                        continue;
                    }

                    string dateiendung = Path.GetExtension(datei.VollstaendigerPfad);
                    string neuerDateiname = Guid.NewGuid().ToString() + dateiendung;
                    string zielPfad = Path.Combine(zielOrdner, neuerDateiname);

                    File.Copy(datei.VollstaendigerPfad, zielPfad, true);

                    person.ErinnerungsDateinamen.Add(neuerDateiname);

                    if (string.IsNullOrEmpty(person.TitelbildDateiname))
                    {
                        person.TitelbildDateiname = neuerDateiname;
                    }

                    arbeitsmappeAusgewaehlt.Remove(pfad);
                    arbeitsmappeBereitsZugeordnet.Add(pfad);
                    verbunden++;
                }

                if (verbunden == 0)
                {
                    James.Hinweis(James.ArbeitsmappeNurBilder);
                    return;
                }

                person.ModifiedAt = DateTime.Now;

                SpeichereDaten();
                SpeichereArbeitsmappeZugeordnet();

                int gesamtErinnerungen = ZaehleErinnerungenDerPerson(person);

                ArbeitsmappeStatusText.Text = verbunden == 1
                    ? James.ArbeitsmappeErinnerungZugeordnet(person.ToString(), gesamtErinnerungen)
                    : James.ArbeitsmappeErinnerungenZugeordnetMehrere(verbunden, person.ToString(), gesamtErinnerungen);

                AktualisiereSchreibtischFallsBetroffen(person);
                AktualisiereErinnerungskarteFallsBetroffen(person, null);
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimSpeichernFoto(ex.Message));
            }
        }

        private void ArbeitsmappePersonZuordnen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            List<Person> alleAuswaehlbarenPersonen = allePersonen
                .Concat(ArchivListe.Items.Cast<Person>())
                .ToList();

            ArbeitsmappeTitelbildPersonComboBox.ItemsSource = alleAuswaehlbarenPersonen;
            ArbeitsmappeTitelbildPersonComboBox.SelectedIndex = -1;

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappePersonAuswahlPanel.Visibility = Visibility.Visible;
        }

        private void ArbeitsmappeTitelbildPersonComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Person person = ArbeitsmappeTitelbildPersonComboBox.SelectedItem as Person;

            if (person != null)
            {
                int anzahlVorher = ZaehleErinnerungenDerPerson(person);
                ArbeitsmappeStatusText.Text = James.DiagnosePersonAusgewaehltFuerErinnerungen(person.ToString(), anzahlVorher);
            }
        }

        private void ArbeitsmappeTitelbildBestaetigen_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArbeitsmappeTitelbildPersonComboBox.SelectedItem as Person;

            if (person == null)
            {
                James.Hinweis(James.BittePersonAuswaehlen);
                return;
            }

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();

            VerknuepfeArbeitsmappenDateienMitPerson(person, pfade);

            int anzahlNachher = ZaehleErinnerungenDerPerson(person);
            ArbeitsmappeStatusText.Text = James.DiagnoseNachZuordnung(person.ToString(), anzahlNachher);

            ArbeitsmappePersonAuswahlPanel.Visibility = Visibility.Collapsed;
            AktualisiereArbeitsmappe();
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && ArbeitsmappeGrossansichtPanel.Visibility == Visibility.Visible)
            {
                ArbeitsmappeGrossansichtSchliessen_Click(sender, e);
            }
        }

        // ============================================================
        // ETAPPE A: DIE EINHEITLICHE EREIGNISLISTE (Vorbereitung)
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