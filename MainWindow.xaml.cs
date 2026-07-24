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

        // Vorbereitung für später: James soll dereinst als eigene Klasse (James.cs)
        // situationsabhängige Begrüßungen liefern (Erststart, normaler Start, nach
        // längerer Pause, neue Erinnerungen vorhanden, keine Änderungen).
        // Noch keine Implementierung, nur diese Vorbereitung.

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

        // Etappe B.1: eine gemeinsame Ordner-Ermittlung für Ereignisse -
        // ersetzt die bisherigen zwei getrennten Methoden
        // (EreignisErinnerungsOrdner / FreiesEreignisErinnerungsOrdner).
        // Erzeugt für dieselbe Person/dasselbe Ereignis exakt denselben
        // Pfad wie bisher - bestehende Ordner bleiben also unverändert
        // gültig, es wurde nichts an der Speicherstruktur geändert.
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

        // Build 2.2a: kleine, persistente Liste bereits zugeordneter
        // Pfade (siehe arbeitsmappeBereitsZugeordnet weiter unten).
        private static readonly string ArbeitsmappeZugeordnetPfad = Path.Combine(OrdnerPfad, "arbeitsmappe_zugeordnet.json");

        // ============================================================
        // ARCHITEKTUR: ARBEITSMAPPE (Build 2.1)
        // ============================================================
        // Arbeitet ausschließlich mit dem bereits vorhandenen
        // Erinnerungsverzeichnis (keine neue Datenhaltung). Die Auswahl
        // wird über die vollständigen Pfade der Dateien nachgehalten -
        // einfacher als eine eigene Auswahl-Eigenschaft pro Datei, und
        // übersteht auch ein Neuzeichnen der Kacheln unbeschadet.
        private List<GefundeneDatei> arbeitsmappeAlleDateien = new List<GefundeneDatei>();
        private string arbeitsmappeFilter = "Alle";
        private int arbeitsmappeSeite = 1;
        private const int ArbeitsmappeProSeite = 25;
        private HashSet<string> arbeitsmappeAusgewaehlt = new HashSet<string>();

        // Build 2.2a: gemerkte Person zwischen Stufe 1 (Person waehlen)
        // und Stufe 2 (Ereignisformular) beim inline-Weg "Neues Ereignis
        // anlegen" innerhalb der Arbeitsmappe.
        private Person arbeitsmappeNeuesEreignisPerson = null;

        // Build 2.3, Punkt 1: merkt sich das zuletzt angelegte/verbundene
        // Ereignis, damit der Benutzer es optional öffnen kann, ohne die
        // Arbeitsmappe zu verlassen.
        private Person arbeitsmappeLetztesEreignisPerson = null;
        private Ereignis arbeitsmappeLetztesEreignis = null;

        // Build 2.2a: einfache, persistente Markierung, welche Pfade aus
        // dem Erinnerungsverzeichnis bereits einer Person oder einem
        // Ereignis zugeordnet wurden - damit der Status auf den Kacheln
        // ("Noch nicht eingeordnet" / "Bereits zugeordnet") tatsächlich
        // stimmt und sich nach jeder Zuordnung sofort aktualisiert.
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
        
        // ============================================================
        // BUILD 1.6: JAMES ZEIGT ERINNERUNGEN (ERINNERUNGSKARTE)
        // ============================================================
        // Architekturbeschluss 009: Gefundene Erinnerungen werden nicht
        // mehr nur als Zeile in einer Liste dargestellt, sondern als
        // großzügige Erinnerungskarte neben der Trefferliste. Verwendet
        // ausschließlich bereits vorhandene Daten - keine neuen Felder,
        // keine KI, keine automatische Geschichte. Die Karte darf später
        // wachsen (z.B. um Videos, Dokumente, Tonaufnahmen) und soll
        // dereinst die Grundlage für das Lebensbuch bilden.
        // Build 2.2a: merkt, welche Person/welches Ereignis die
        // Erinnerungskarte gerade zeigt - damit wir sie nach einer
        // Zuordnung aus der Arbeitsmappe nur dann auffrischen, wenn sie
        // tatsächlich betroffen ist (nie eine andere Person/ein anderes
        // Ereignis überschreiben).
        private Person erinnerungskarteAktuellePerson = null;
        private Ereignis erinnerungskarteAktuellesEreignis = null;

        // Aktualisiert die Erinnerungskarte nur dann, wenn sie gerade
        // exakt diese Person (und, falls angegeben, exakt dieses
        // Ereignis) zeigt. So kann nach einer Zuordnung aus der
        // Arbeitsmappe niemals versehentlich die Erinnerung einer
        // anderen Person/eines anderen Ereignisses überschrieben werden.
        private void AktualisiereErinnerungskarteFallsBetroffen(Person person, Ereignis ereignis)
        {
            if (erinnerungskarteAktuellePerson == null || erinnerungskarteAktuellesEreignis == null)
            {
                return;
            }

            bool selbePerson = erinnerungskarteAktuellePerson.Id == person.Id;
            bool selbesEreignis = ereignis == null || erinnerungskarteAktuellesEreignis.Id == ereignis.Id;

            if (selbePerson && selbesEreignis)
            {
                ZeigeErinnerungskarte(erinnerungskarteAktuellePerson, erinnerungskarteAktuellesEreignis);
            }
        }

        // Aktualisiert Titelbild/Ereignisliste auf dem Schreibtisch nur
        // dann, wenn dort gerade exakt diese Person ausgewählt ist -
        // sonst bliebe dort sonst das Bild einer anderen Person stehen
        // bzw. würde durch die Daten einer anderen Person überschrieben.
        private void AktualisiereSchreibtischFallsBetroffen(Person person)
        {
            Person aufSchreibtischAusgewaehlt = PersonenListe.SelectedItem as Person;

            if (aufSchreibtischAusgewaehlt != null && aufSchreibtischAusgewaehlt.Id == person.Id)
            {
                ZeigeFoto(person);
                AktualisiereEreignisseAnzeige(person);
                ZeigePersonErinnerungenLink(person);
            }
        }

        // ============================================================
        // BUILD 2.3: PERSON BESITZT ERINNERUNGEN (nicht nur ein Titelbild)
        // ============================================================
        // Architekturbeschluss "Abschluss Etappe A": eine Erinnerung einer
        // Person ist jedes Foto, das über eines ihrer Ereignisse mit ihr
        // verbunden ist (Titelbild+EreignisFoto+WeitereFotos). Das
        // Titelbild selbst zählt bewusst NICHT mit - es bleibt laut
        // Architekt "nur das Gesicht der Person".

        private static int ZaehleErinnerungenDerPerson(Person person)
        {
            if (person == null)
            {
                return 0;
            }

            // Build 2.4: die direkte Erinnerungssammlung der Person
            // (unabhängig von Ereignissen) zählt jetzt mit.
            int anzahl = person.ErinnerungsDateinamen != null ? person.ErinnerungsDateinamen.Count : 0;

            if (person.Ereignisse != null)
            {
                foreach (Ereignis ereignis in person.Ereignisse)
                {
                    if (!string.IsNullOrEmpty(ereignis.EreignisFotoDateiname))
                    {
                        anzahl++;
                    }

                    if (ereignis.WeitereFotoDateinamen != null)
                    {
                        anzahl += ereignis.WeitereFotoDateinamen.Count;
                    }
                }
            }

            return anzahl;
        }

        private void ZeigePersonErinnerungenLink(Person person)
        {
            if (person == null)
            {
                PersonErinnerungenLinkText.Text = "";
                PersonErinnerungenLinkText.Tag = null;
                return;
            }

            int anzahl = ZaehleErinnerungenDerPerson(person);
            PersonErinnerungenLinkText.Text = James.PersonErinnerungenLink(anzahl);
            PersonErinnerungenLinkText.Tag = anzahl > 0 ? person : null;
        }

        // Build 2.9 (Etappe B.1): Öffnet das einfache Erinnerungsfenster mit
        // allen Vorschaubildern dieser Person - sowohl direkt zugeordnete
        // Erinnerungen als auch alle über Ereignisse verknüpften Fotos.
        // Bewusst nur anzeigen (siehe ErinnerungenFenster) - keine
        // Bearbeitung, keine Titelbild-Auswahl mehr an dieser Stelle.
        private void PersonErinnerungenLinkText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Person person = PersonErinnerungenLinkText.Tag as Person;

            if (person == null)
            {
                return;
            }

            List<ErinnerungsInfo> erinnerungenListe = SammelErinnerungenFuerPerson(person);

            if (erinnerungenListe.Count == 0)
            {
                return;
            }

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelPerson(person.ToString()), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal);
            fenster.Owner = this;
            fenster.Show();
        }

        // Sammelt alle Erinnerungen einer Person samt Kontext (Build 3.0) -
        // direkt zugeordnete (ErinnerungsDateinamen, ohne Ereignis-Kontext)
        // und alle über ihre Ereignisse verknüpften Fotos (mit vollem
        // Kontext: Titel, Datum, Ort, Beschreibung, Beteiligte).
        private List<ErinnerungsInfo> SammelErinnerungenFuerPerson(Person person)
        {
            List<ErinnerungsInfo> erinnerungenListe = new List<ErinnerungsInfo>();

            if (person.ErinnerungsDateinamen != null)
            {
                string ordner = PersonErinnerungsOrdner(person);

                foreach (string dateiname in person.ErinnerungsDateinamen)
                {
                    erinnerungenListe.Add(new ErinnerungsInfo
                    {
                        Pfad = Path.Combine(ordner, dateiname),
                        PersonName = person.ToString()
                    });
                }
            }

            if (person.Ereignisse != null)
            {
                foreach (Ereignis ereignis in person.Ereignisse)
                {
                    erinnerungenListe.AddRange(SammelErinnerungenFuerEreignis(person, ereignis));
                }
            }

            return erinnerungenListe;
        }

        // Sammelt alle Erinnerungen eines einzelnen Ereignisses samt
        // Kontext (Titelbild + weitere Fotos, jeweils mit denselben
        // Ereignis-Angaben, da sie zum selben Ereignis gehören).
        // Etappe B.1: unterstützt jetzt auch person == null (freies
        // Ereignis) - ersetzt die bisherige zweite, fast identische
        // Methode SammelErinnerungenFuerFreiesEreignis.
        private List<ErinnerungsInfo> SammelErinnerungenFuerEreignis(Person person, Ereignis ereignis)
        {
            List<ErinnerungsInfo> erinnerungenListe = new List<ErinnerungsInfo>();
            string ordner = ErinnerungsOrdnerFuer(person, ereignis);

            List<string> beschreibungsTeile = new List<string>();

            if (!string.IsNullOrWhiteSpace(ereignis.Beschreibung))
            {
                beschreibungsTeile.Add(ereignis.Beschreibung);
            }

            if (!string.IsNullOrWhiteSpace(ereignis.Bemerkungen))
            {
                beschreibungsTeile.Add(ereignis.Bemerkungen);
            }

            string beschreibung = string.Join("\n", beschreibungsTeile);

            List<string> alleDateinamen = new List<string>();

            if (!string.IsNullOrEmpty(ereignis.EreignisFotoDateiname))
            {
                alleDateinamen.Add(ereignis.EreignisFotoDateiname);
            }

            if (ereignis.WeitereFotoDateinamen != null)
            {
                alleDateinamen.AddRange(ereignis.WeitereFotoDateinamen);
            }

            foreach (string dateiname in alleDateinamen)
            {
                erinnerungenListe.Add(new ErinnerungsInfo
                {
                    Pfad = Path.Combine(ordner, dateiname),
                    Titel = ereignis.Titel,
                    Datum = ereignis.Datum,
                    Ort = ereignis.Ort,
                    Beschreibung = beschreibung,
                    PersonName = person != null ? person.ToString() : "",
                    Beteiligte = ereignis.Beteiligte
                });
            }

            return erinnerungenListe;
        }

        // Build 2.9 (Etappe B.1): Öffnet dasselbe einfache Erinnerungsfenster,
        // diesmal beschränkt auf die Erinnerungen eines einzelnen Ereignisses.
        private void EreignisErinnerungenLinkText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Ereignis ereignis = EreignisErinnerungenLinkText.Tag as Ereignis;
            Person person = PersonenListe.SelectedItem as Person;

            if (ereignis == null || person == null)
            {
                return;
            }

            List<ErinnerungsInfo> erinnerungenListe = SammelErinnerungenFuerEreignis(person, ereignis);

            if (erinnerungenListe.Count == 0)
            {
                return;
            }

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelEreignis(ereignis.Titel), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal);
            fenster.Owner = this;
            fenster.Show();
        }

        // Aktualisiert den "Erinnerungen (N)"-Link für das aktuell
        // ausgewählte Ereignis (analog zu ZeigePersonErinnerungenLink).
        private void ZeigeEreignisErinnerungenLink(Ereignis ereignis)
        {
            if (ereignis == null)
            {
                EreignisErinnerungenLinkText.Text = "";
                EreignisErinnerungenLinkText.Tag = null;
                return;
            }

            int anzahl = (!string.IsNullOrEmpty(ereignis.EreignisFotoDateiname) ? 1 : 0) +
                         (ereignis.WeitereFotoDateinamen != null ? ereignis.WeitereFotoDateinamen.Count : 0);

            EreignisErinnerungenLinkText.Text = James.PersonErinnerungenLink(anzahl);
            EreignisErinnerungenLinkText.Tag = anzahl > 0 ? ereignis : null;
        }

        private void ZeigeErinnerungskarte(Person person, Ereignis ereignis)
        {
            erinnerungskarteAktuellePerson = person;
            erinnerungskarteAktuellesEreignis = ereignis;

            ErinnerungskarteTitelText.Text = ereignis.Titel;

            List<string> datumUndJahreszeit = new List<string>();

            if (!string.IsNullOrWhiteSpace(ereignis.Datum))
            {
                datumUndJahreszeit.Add(ereignis.Datum);
            }

            if (!string.IsNullOrWhiteSpace(ereignis.Jahreszeit))
            {
                datumUndJahreszeit.Add(ereignis.Jahreszeit);
            }

            if (datumUndJahreszeit.Count > 0)
            {
                ErinnerungskarteDatumText.Text = "📅 " + string.Join(" · ", datumUndJahreszeit);
                ErinnerungskarteDatumText.Visibility = Visibility.Visible;
            }
            else
            {
                ErinnerungskarteDatumText.Visibility = Visibility.Collapsed;
            }

            string personenzeile = "👤 " + person.ToString();

            if (person.Beziehung != null && !string.IsNullOrWhiteSpace(person.Beziehung.ToString()))
            {
                personenzeile += " (" + person.Beziehung.ToString() + ")";
            }

            ErinnerungskartePersonText.Text = personenzeile;

            if (!string.IsNullOrWhiteSpace(ereignis.Ort))
            {
                ErinnerungskarteOrtText.Text = "📍 " + ereignis.Ort;
                ErinnerungskarteOrtText.Visibility = Visibility.Visible;
            }
            else
            {
                ErinnerungskarteOrtText.Visibility = Visibility.Collapsed;
            }

            if (ereignis.Stichwoerter != null && ereignis.Stichwoerter.Count > 0)
            {
                ErinnerungskarteStichwoerterText.Text = "🏷️ " + string.Join(", ", ereignis.Stichwoerter);
                ErinnerungskarteStichwoerterText.Visibility = Visibility.Visible;
            }
            else
            {
                ErinnerungskarteStichwoerterText.Visibility = Visibility.Collapsed;
            }

            if (ereignis.Bewertungen != null && ereignis.Bewertungen.Count > 0)
            {
                ErinnerungskarteBewertungenText.Text = "⭐ " + string.Join(", ", ereignis.Bewertungen);
                ErinnerungskarteBewertungenText.Visibility = Visibility.Visible;
            }
            else
            {
                ErinnerungskarteBewertungenText.Visibility = Visibility.Collapsed;
            }

            if (ereignis.Beteiligte != null && ereignis.Beteiligte.Count > 0)
            {
                ErinnerungskarteBeteiligteText.Text = "👥 Auch dabei: " + string.Join(", ", ereignis.Beteiligte);
                ErinnerungskarteBeteiligteText.Visibility = Visibility.Visible;
            }
            else
            {
                ErinnerungskarteBeteiligteText.Visibility = Visibility.Collapsed;
            }

            List<string> beschreibungUndBemerkungen = new List<string>();

            if (!string.IsNullOrWhiteSpace(ereignis.Beschreibung))
            {
                beschreibungUndBemerkungen.Add(ereignis.Beschreibung);
            }

            if (!string.IsNullOrWhiteSpace(ereignis.Bemerkungen))
            {
                beschreibungUndBemerkungen.Add(ereignis.Bemerkungen);
            }

            if (beschreibungUndBemerkungen.Count > 0)
            {
                ErinnerungskarteBemerkungenText.Text = "📝 " + string.Join("\n", beschreibungUndBemerkungen);
                ErinnerungskarteBemerkungenText.Visibility = Visibility.Visible;
            }
            else
            {
                ErinnerungskarteBemerkungenText.Visibility = Visibility.Collapsed;
            }

            if (ereignis.WeitereFotoDateinamen != null && ereignis.WeitereFotoDateinamen.Count > 0)
            {
                ErinnerungskarteWeitereFotosText.Text = "📷 + " + ereignis.WeitereFotoDateinamen.Count + " weitere Erinnerungen zu diesem Ereignis";
                ErinnerungskarteWeitereFotosText.Visibility = Visibility.Visible;
            }
            else
            {
                ErinnerungskarteWeitereFotosText.Visibility = Visibility.Collapsed;
            }

            // Titelbild der Karte: bevorzugt das Foto des Ereignisses. Gibt es
            // noch keines, zeigen wir ersatzweise das Titelbild der Person,
            // damit die Karte nicht leer wirkt (reine Darstellungsentscheidung,
            // kein neues Datenfeld).
            bool ereignisBildGezeigt = ZeigeBildFuerKarte(
                ErinnerungsOrdnerFuer(person, ereignis),
                ereignis.EreignisFotoDateiname);

            if (!ereignisBildGezeigt)
            {
                ZeigeBildFuerKarte(PersonErinnerungsOrdner(person), person.TitelbildDateiname);
            }

            ErinnerungskartePlatzhalterText.Visibility = Visibility.Collapsed;
            ErinnerungskartePanel.Visibility = Visibility.Visible;

            ZeigeVerknuepfungen(person, ereignis);
        }

        // Laedt ein Bild in die Erinnerungskarte, falls die Datei existiert.
        // Gibt zurueck, ob tatsaechlich ein Bild angezeigt wurde.
        private bool ZeigeBildFuerKarte(string ordner, string dateiname)
        {
            if (dateiname != null)
            {
                string pfad = Path.Combine(ordner, dateiname);

                if (File.Exists(pfad))
                {
                    BitmapImage bild = new BitmapImage();
                    bild.BeginInit();
                    bild.CacheOption = BitmapCacheOption.OnLoad;
                    bild.UriSource = new Uri(pfad);
                    bild.EndInit();

                    ErinnerungskarteBild.Source = bild;
                    return true;
                }
            }

            ErinnerungskarteBild.Source = null;
            return false;
        }

        // ============================================================
        // BUILD 1.8: ERINNERUNGEN VERKNÜPFEN
        // ============================================================
        // Architekturbeschluss 011: James beginnt, Zusammenhänge zwischen
        // Erinnerungen sichtbar zu machen - "Dazu könnten auch gehören"
        // auf der Erinnerungskarte. Ausschließlich aus bereits
        // vorhandenen Daten, in fester Rangfolge (gleiche Person >
        // gleiche Beziehung > gemeinsames Stichwort > gleiche Jahreszeit
        // > ähnliche Zeit > gleicher Ort). James nennt dabei NUR die
        // Kategorie ("gleicher Ort"), niemals eine Erklärung, warum das
        // wichtig sein könnte - das zu beurteilen bleibt dem Benutzer
        // überlassen. Noch keine KI, keine automatische Geschichte,
        // keine neuen Datenfelder.

        private const int VerknuepfungenMax = 5;

        private void ZeigeVerknuepfungen(Person basisPerson, Ereignis basisEreignis)
        {
            List<Verknuepfung> verknuepfungen = ErmittleVerknuepfungen(basisPerson, basisEreignis);

            if (verknuepfungen.Count == 0)
            {
                ErinnerungskarteVerknuepfungenPanel.Visibility = Visibility.Collapsed;
                return;
            }

            ErinnerungskarteVerknuepfungenListe.ItemsSource = verknuepfungen;
            ErinnerungskarteVerknuepfungenPanel.Visibility = Visibility.Visible;
        }

        private List<Verknuepfung> ErmittleVerknuepfungen(Person basisPerson, Ereignis basisEreignis)
        {
            List<Verknuepfung> kandidaten = new List<Verknuepfung>();

            foreach (Person person in allePersonen)
            {
                SammleVerknuepfungsKandidaten(person, false, basisPerson, basisEreignis, kandidaten);
            }

            foreach (object element in ArchivListe.Items)
            {
                Person person = element as Person;

                if (person != null)
                {
                    SammleVerknuepfungsKandidaten(person, true, basisPerson, basisEreignis, kandidaten);
                }
            }

            return kandidaten
                .OrderBy(k => k.Prioritaet)
                .Take(VerknuepfungenMax)
                .ToList();
        }

        private void SammleVerknuepfungsKandidaten(Person kandidatPerson, bool istArchiviert, Person basisPerson, Ereignis basisEreignis, List<Verknuepfung> ergebnis)
        {
            if (kandidatPerson.Ereignisse == null)
            {
                return;
            }

            foreach (Ereignis kandidatEreignis in kandidatPerson.Ereignisse)
            {
                if (kandidatEreignis.Id == basisEreignis.Id)
                {
                    // nicht sich selbst als eigene Verknuepfung vorschlagen
                    continue;
                }

                int prioritaet = ErmittleVerknuepfungsprioritaet(basisPerson, basisEreignis, kandidatPerson, kandidatEreignis);

                if (prioritaet > 0)
                {
                    ergebnis.Add(new Verknuepfung
                    {
                        Grund = VerknuepfungsgrundText(prioritaet),
                        Prioritaet = prioritaet,
                        Person = kandidatPerson,
                        Ereignis = kandidatEreignis,
                        IstArchiviert = istArchiviert
                    });
                }
            }
        }

        // Prüft die Kategorien in der vom Architekten vorgegebenen
        // Rangfolge und gibt die erste zutreffende zurück (1 = höchste
        // Rangstufe). 0 bedeutet: keine Verbindung gefunden.
        private static int ErmittleVerknuepfungsprioritaet(Person basisPerson, Ereignis basisEreignis, Person kandidatPerson, Ereignis kandidatEreignis)
        {
            if (kandidatPerson.Id == basisPerson.Id)
            {
                return 1;
            }

            string basisBeziehung = basisPerson.Beziehung != null ? basisPerson.Beziehung.ToString() : null;
            string kandidatBeziehung = kandidatPerson.Beziehung != null ? kandidatPerson.Beziehung.ToString() : null;

            if (!string.IsNullOrWhiteSpace(basisBeziehung) && string.Equals(basisBeziehung, kandidatBeziehung, StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (basisEreignis.Stichwoerter != null && kandidatEreignis.Stichwoerter != null)
            {
                foreach (string wort in basisEreignis.Stichwoerter)
                {
                    if (kandidatEreignis.Stichwoerter.Any(k => string.Equals(k, wort, StringComparison.OrdinalIgnoreCase)))
                    {
                        return 3;
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(basisEreignis.Jahreszeit) &&
                string.Equals(basisEreignis.Jahreszeit, kandidatEreignis.Jahreszeit, StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            DateTime basisDatum;
            DateTime kandidatDatum;

            if (VersucheDatumZuLesen(basisEreignis.Datum, out basisDatum) &&
                VersucheDatumZuLesen(kandidatEreignis.Datum, out kandidatDatum))
            {
                if (Math.Abs((basisDatum - kandidatDatum).TotalDays) <= 21)
                {
                    return 5;
                }
            }

            if (!string.IsNullOrWhiteSpace(basisEreignis.Ort) &&
                string.Equals(basisEreignis.Ort.Trim(), kandidatEreignis.Ort != null ? kandidatEreignis.Ort.Trim() : null, StringComparison.OrdinalIgnoreCase))
            {
                return 6;
            }

            return 0;
        }

        private static string VerknuepfungsgrundText(int prioritaet)
        {
            switch (prioritaet)
            {
                case 1: return "gleiche Person";
                case 2: return "gleiche Beziehung";
                case 3: return "gleiches Stichwort";
                case 4: return "gleiche Jahreszeit";
                case 5: return "ähnliche Zeit";
                case 6: return "gleicher Ort";
                default: return "";
            }
        }

        private void ErinnerungskarteVerknuepfungenListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Verknuepfung verknuepfung = ErinnerungskarteVerknuepfungenListe.SelectedItem as Verknuepfung;

            if (verknuepfung == null)
            {
                return;
            }

            if (verknuepfung.IstArchiviert)
            {
                bool ergebnis = James.FrageJaNein(James.FrageTrefferImArchiv(verknuepfung.Person.ToString()));

                if (ergebnis)
                {
                    HoleAusArchivZurueckAufSchreibtisch(verknuepfung.Person, verknuepfung.Ereignis);
                }
            }
            else
            {
                SucheTextBox.Text = "";

                HauptTabControl.SelectedIndex = 0;
                PersonenListe.SelectedItem = verknuepfung.Person;
                EreignisseListe.SelectedItem = verknuepfung.Ereignis;
            }

            // Karte auf die angeklickte Verknuepfung umschalten, damit man
            // durch das entstehende Erinnerungsnetz weiterklicken kann.
            ZeigeErinnerungskarte(verknuepfung.Person, verknuepfung.Ereignis);

            // Auswahl zurücksetzen, damit ein erneutes Anklicken desselben
            // Eintrags wieder ein SelectionChanged auslöst.
            ErinnerungskarteVerknuepfungenListe.SelectedItem = null;
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
        // Rein manuelle Einordnung durch den Menschen. Die Ankreuzfelder
        // spiegeln direkt Ereignis.Bewertungen wider - beim Anklicken eines
        // Ereignisses werden sie entsprechend gesetzt, beim Speichern wieder
        // zurueckgeschrieben.

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

        // Baut aus den beschreibenden Feldern eines Ereignisses (Build 0.7)
        // einen gut lesbaren Text zusammen, damit man sich diese Angaben
        // auch nach dem Anlegen jederzeit wieder ansehen kann.
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
                // Build 2.2: bereits bekannte Personen werden beim Anzeigen
                // erkannt (nicht dauerhaft verknüpft) und mit ✓ markiert.
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

        // Build 2.5: Die Arbeitsmappe ist der einzige Einstiegspunkt, um
        // einem Ereignis Erinnerungen zuzuordnen (siehe
        // VerknuepfeArbeitsmappenDateienMitEreignis). Kein Windows-
        // Dateidialog mehr - die Zuordnung erfolgt ausschließlich über
        // bereits im Erinnerungsverzeichnis vorhandene Dateien.
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

            // Build 2.2: "Wer war noch dabei?" - vorerst nur als Textliste
            // gespeichert (keine automatische Personenanlage). Ob ein Name
            // bereits einer bekannten Person entspricht, wird beim
            // Anzeigen dynamisch geprüft (ErstelleEreignisDetailsText),
            // nicht dauerhaft verknüpft - so bleibt die Architektur offen,
            // falls daraus später einmal vollwertige Personen entstehen.
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

            // Build 2.3, Punkt 1: das neue Ereignis sofort sichtbar machen,
            // statt es "unsichtbar" in der Liste verschwinden zu lassen -
            // James zeigt es direkt mit allen Details an.
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
                    // Manche Laufwerke (z.B. leere CD/DVD-Laufwerke) verweigern
                    // Auskunft über ihre Eigenschaften - dann einfach überspringen.
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

        // Erzeugt einen unsichtbaren Platzhalter-Unterknoten, damit ein
        // Ordner im Baum ein Aufklapp-Symbol erhält, auch wenn seine
        // tatsächlichen Unterordner noch nicht eingelesen wurden.
        private static OrdnerKnoten ErzeugePlatzhalterKnoten()
        {
            return new OrdnerKnoten
            {
                Name = "Wird geladen ...",
                VollstaendigerPfad = null
            };
        }

        // Liest beim Aufklappen eines Ordners im Baum dessen tatsächliche
        // Unterordner ein (lazy loading) - erst bei Bedarf, nicht im Voraus.
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
                        // Einzelner Unterordner nicht zugänglich - überspringen.
                    }
                }
            }
            catch
            {
                // Ordner nicht zugänglich (z.B. fehlende Rechte) - Baum
                // bleibt an dieser Stelle einfach leer.
            }

            e.Handled = true;
        }

        // Sammelt rekursiv alle vom Benutzer angehakten Ordner. Ist ein
        // Ordner angehakt, werden seine Unterordner NICHT gesondert
        // betrachtet - der Rundgang durchsucht diesen Ordner ohnehin
        // bereits vollständig samt aller Unterordner.
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
                // Ordnergedächtnis konnte nicht gelesen werden - James
                // beginnt in diesem Fall einfach mit einem leeren
                // Gedächtnis, statt den Start zu verweigern.
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

            // Ordnergedächtnis aktualisieren (Build 1.1): auch bei einem
            // abgebrochenen Rundgang tragen wir die bis dahin durchsuchten
            // Ordner ein - besser eine ehrliche Teilinformation als gar keine.
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
                            // Hashwert konnte nicht berechnet werden (z.B. Datei gerade
                            // in Verwendung) - Datei wird trotzdem erfasst, nur ohne Hashwert.
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
                        // Einzelne Datei nicht lesbar - überspringen, Rundgang fortsetzen.
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Ordner nicht zugänglich - überspringen, Rundgang fortsetzen.
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
                // Unterordner nicht zugänglich - überspringen, Rundgang fortsetzen.
            }
        }

        // Berechnet einen eindeutigen "Fingerabdruck" (SHA256-Hashwert) des
        // Dateiinhalts. Zwei Dateien mit identischem Hashwert besitzen exakt
        // denselben Inhalt (Build 1.0, Grundlage für "Echte Doppelgänger").
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

        // Build 2.5: Die Arbeitsmappe ist der einzige Einstiegspunkt, um
        // einer Person Erinnerungen zuzuordnen (siehe
        // VerknuepfeArbeitsmappenDateienMitPerson). Dieser Button öffnet
        // deshalb keinen Windows-Dateidialog mehr, sondern führt direkt
        // dorthin - so entsteht keine zweite, abweichende Zuordnungslogik
        // mehr (die vorherige Fassung setzte TitelbildDateiname z.B.
        // direkt, ohne die Erinnerung auch in ErinnerungsDateinamen
        // aufzunehmen).
        private void FotoHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            Person person = PersonenListe.SelectedItem as Person;

            if (person == null)
            {
                James.Hinweis(James.BittePersonAuswaehlen);
                return;
            }

            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;
            James.Hinweis(James.ArbeitsmappeUmleitungPerson(person.ToString()));
        }

        private void ZeigeFoto(Person person)
        {
            if (person != null && person.TitelbildDateiname != null)
            {
                string pfad = Path.Combine(PersonErinnerungsOrdner(person), person.TitelbildDateiname);

                if (File.Exists(pfad))
                {
                    BitmapImage bild = new BitmapImage();
                    bild.BeginInit();
                    bild.CacheOption = BitmapCacheOption.OnLoad;
                    bild.UriSource = new Uri(pfad);
                    bild.EndInit();

                    PersonFotoBild.Source = bild;
                    return;
                }
            }

            PersonFotoBild.Source = null;
        }

        // ============================================================
        // BUILD 1.2: BEZIEHUNGEN VERSTEHEN
        // ============================================================

        // Zeigt die gespeicherte Beziehung einer Person im Rollenfeld an
        // (oder leert es, wenn keine Person/keine Beziehung vorhanden ist).
        //
        // Build 2.1a, Ergonomie-Anpassung: Das Rollenfeld ist jetzt frei
        // beschreibbar (IsEditable="True" in der XAML) statt einer
        // Auswahlliste mit gesonderter "Sonstige"-Bezeichnung. Dank
        // Beziehung.ToString() (löst "Sonstige" + eigene Bezeichnung
        // automatisch auf) reicht ein einziger Zeilencode - bereits
        // gespeicherte, alte Beziehungen werden dabei unverändert korrekt
        // angezeigt, ohne dass sich an der Datenstruktur etwas ändert.
        private void ZeigeBeziehung(Person person)
        {
            BeziehungRolleComboBox.Text = person != null && person.Beziehung != null
                ? person.Beziehung.ToString()
                : "";
        }

        // Baut aus der aktuellen Eingabe ein Beziehung-Objekt (oder null,
        // wenn das Feld leer gelassen wurde). Der Benutzer kann frei
        // tippen oder einen der vorgeschlagenen Begriffe aus der
        // Auswahlliste übernehmen - beides landet gleichermaßen in Rolle.
        private Beziehung ErstelleBeziehungAusEingabe()
        {
            string text = BeziehungRolleComboBox.Text != null ? BeziehungRolleComboBox.Text.Trim() : "";

            if (text == "")
            {
                return null;
            }

            return new Beziehung
            {
                Rolle = text,
                EigeneBezeichnung = null
            };
        }

        private void PersonenListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Person person = PersonenListe.SelectedItem as Person;
            aktuellBearbeitetePerson = person;

            if (person != null)
            {
                VornameTextBox.Text = person.Vorname;
                NachnameTextBox.Text = person.Nachname;
                GeburtTextBox.Text = person.Geburt;
                OrtTextBox.Text = person.Ort;
            }
            else
            {
                VornameTextBox.Clear();
                NachnameTextBox.Clear();
                GeburtTextBox.Clear();
                OrtTextBox.Clear();
            }

            ZeigeBeziehung(person);
            ZeigeFoto(person);
            AktualisiereEreignisseAnzeige(person);
            ZeigePersonErinnerungenLink(person);
            EreignisFormularPanel.Visibility = Visibility.Collapsed;
            SpeichereArbeitsstand();
        }

        private void ArchivListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Person person = ArchivListe.SelectedItem as Person;
            ZeigeArchivFoto(person);
            ArchivAktionPanel.Visibility = Visibility.Collapsed;
        }

        // Etappe B.1b, Punkt 2: ersetzt den bisherigen grünen Link aus
        // Etappe B.1a - dieselbe, bereits vorhandene Sammel- und
        // Fenster-Logik, jetzt aber als Menüpunkt im "Was möchten wir
        // tun?"-Menü erreichbar, genau wie beim Ereignis-Archiv.
        private void ArchivPersonErinnerungenAnsehen_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArchivListe.SelectedItem as Person;

            if (person == null)
            {
                return;
            }

            List<ErinnerungsInfo> erinnerungenListe = SammelErinnerungenFuerPerson(person);

            if (erinnerungenListe.Count == 0)
            {
                return;
            }

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelPerson(person.ToString()), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal);
            fenster.Owner = this;
            fenster.Show();
        }

        private void ZeigeArchivFoto(Person person)
        {
            if (person != null && person.TitelbildDateiname != null)
            {
                string pfad = Path.Combine(PersonErinnerungsOrdner(person), person.TitelbildDateiname);

                if (File.Exists(pfad))
                {
                    BitmapImage bild = new BitmapImage();
                    bild.BeginInit();
                    bild.CacheOption = BitmapCacheOption.OnLoad;
                    bild.UriSource = new Uri(pfad);
                    bild.EndInit();

                    ArchivFotoBild.Source = bild;
                    return;
                }
            }

            ArchivFotoBild.Source = null;
        }

        private void ArchivAktion_Click(object sender, RoutedEventArgs e)
        {
            if (ArchivListe.SelectedItem == null)
            {
                James.Hinweis(James.BitteArchivPersonAuswaehlen);
                return;
            }

            if (ArchivAktionPanel.Visibility == Visibility.Visible)
            {
                ArchivAktionPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                ArchivAktionPanel.Visibility = Visibility.Visible;
            }
        }

        // Build 2.5: gleiche Konsolidierung wie bei FotoHinzufuegen_Click -
        // auch für archivierte Personen läuft die Zuordnung jetzt
        // ausschließlich über die Arbeitsmappe (deren Personen-Auswahl
        // dafür um archivierte Personen erweitert wurde, siehe
        // ArbeitsmappePersonZuordnen_Click).
        private void ArchivFotoHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArchivListe.SelectedItem as Person;

            if (person == null)
            {
                return;
            }

            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;
            James.Hinweis(James.ArbeitsmappeUmleitungPerson(person.ToString()));
        }

        private void ArchivZurueck_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArchivListe.SelectedItem as Person;

            if (person == null)
            {
                return;
            }

            HoleAusArchivZurueckAufSchreibtisch(person, null);
        }

        // Holt eine archivierte Person zurück auf den Schreibtisch und macht
        // sie dort vollständig bearbeitbar. Wird sowohl vom Button "Zurück
        // auf den Schreibtisch" im Archiv genutzt, als auch beim Anklicken
        // eines archivierten James-Suchtreffers (Build 0.8).
        private void HoleAusArchivZurueckAufSchreibtisch(Person person, Ereignis auszuwaehlendesEreignis)
        {
            ArchivListe.Items.Remove(person);
            allePersonen.Add(person);

            SortiereAllePersonen();
            AktualisierePersonenAnzeige();

            SpeichereDaten();

            ArchivAktionPanel.Visibility = Visibility.Collapsed;
            ArchivFotoBild.Source = null;

            HauptTabControl.SelectedIndex = 0;

            // Derselbe Wechsel wie an anderer Stelle bereits korrekt
            // gemacht (Build 5.1, "Letzte Arbeit fortsetzen") - fehlte
            // hier bisher, wodurch die Startseite sichtbar blieb und die
            // wiederhergestellte Person "leer" wirkte.
            StartseiteBereich.Visibility = Visibility.Collapsed;
            EreignisBereich.Visibility = Visibility.Collapsed;
            EreignismappeBereich.Visibility = Visibility.Collapsed;
            PersonenFormularBereich.Visibility = Visibility.Visible;
            PersonenListeBereich.Visibility = Visibility.Visible;

            PersonenListe.SelectedItem = person;

            VornameTextBox.Text = person.Vorname;
            NachnameTextBox.Text = person.Nachname;
            GeburtTextBox.Text = person.Geburt;
            OrtTextBox.Text = person.Ort;

            aktuellBearbeitetePerson = person;

            ZeigeBeziehung(person);
            ZeigeFoto(person);
            AktualisiereEreignisseAnzeige(person);
            ZeigePersonErinnerungenLink(person);

            if (auszuwaehlendesEreignis != null)
            {
                EreignisseListe.SelectedItem = auszuwaehlendesEreignis;
            }

            ZeigeStatusMeldung(James.ZurueckAufSchreibtisch(person.ToString()));
        }

        // Etappe B.1b, Punkt 4: ersetzt die bisherige sofortige, endgültige
        // Entfernung durch dieselbe Papierkorb-Logik, die für freie
        // Ereignisse bereits verwendet wird (siehe ArchivEreignisInPapierkorb_Click) -
        // dieselben James-Texte, dasselbe Bestätigungsprinzip.
        private void ArchivPersonInPapierkorb_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArchivListe.SelectedItem as Person;

            if (person == null)
            {
                return;
            }

            bool ergebnis = James.FrageJaNein(James.FrageInPapierkorbEinzeln(person.ToString()), James.TitelEntscheidung, MessageBoxImage.Warning);

            if (ergebnis)
            {
                ArchivListe.Items.Remove(person);
                PapierkorbListe.Items.Add(person);
                SpeichereDaten();

                ArchivAktionPanel.Visibility = Visibility.Collapsed;
                ArchivFotoBild.Source = null;
            }
        }

        // ============================================================
        // BUILD 1.9: JAMES MERKT SICH DIE ARBEIT
        // ============================================================
        // Architekturbeschluss 011 (Fortsetzung): Kein KI, keine
        // automatische Entscheidung - James merkt sich lediglich, welche
        // Person/welches Ereignis zuletzt auf dem Schreibtisch geöffnet
        // war, und bietet beim nächsten Start an, dort weiterzumachen.
        // Sagt der Benutzer Nein, startet James ganz normal (inkl. der
        // gewohnten Vorschläge aus Build 1.7).

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
                // Arbeitsstand nicht lesbar - dann eben kein Angebot zur
                // Fortsetzung, kein Fehlerdialog noetig (reine Komfortfunktion).
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
                    // Keine aktive Auswahl auf dem Schreibtisch - der
                    // zuletzt gespeicherte Arbeitsstand bleibt bewusst
                    // unveraendert bestehen (kein Loeschen bei blossem
                    // Abwaehlen).
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
                // Arbeitsstand ist eine Komfortfunktion - schlaegt das
                // Speichern fehl, soll das die eigentliche Arbeit nicht
                // stoeren (kein Fehlerdialog).
            }
        }

        // Prüft beim Start, ob eine noch nicht fertige Arbeit existiert,
        // und bietet - falls ja - freundlich an, dort weiterzumachen.
        // Gibt true zurueck, wenn tatsaechlich fortgesetzt wurde (dann
        // zeigt der Aufrufer keine Vorschläge zusätzlich an).
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
                // Person ist inzwischen nicht mehr auf dem Schreibtisch
                // (z.B. archiviert oder in den Papierkorb gelegt) - dann
                // gibt es nichts sinnvoll fortzusetzen.
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

            // Build 5.1: die Startseite verlassen und den Personen-Bereich
            // zeigen, da sonst die wiederhergestellte Auswahl "hinter" der
            // neuen Startseite verborgen bliebe.
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
        // Architekturbeschluss 013: James zeigt keine Dateilisten mehr,
        // sondern legt gefundene Erinnerungen aus dem bereits
        // vorhandenen Erinnerungsverzeichnis übersichtlich als Kacheln
        // aus. Keine neue Datenhaltung, keine neue Suchlogik (nur ein
        // einfacher Namensfilter), keine KI, keine automatische
        // Bilderkennung oder Dublettenerkennung.
        //
        // EHRLICHE VEREINFACHUNGEN (bewusst, siehe Projektarchiv):
        // - PDFs, Dokumente, Videos und Audio werden beim Doppelklick mit
        //   dem Standardprogramm des Betriebssystems geöffnet - kein
        //   eigener Betrachter eingebaut. Nur Bilder werden direkt in der
        //   Arbeitsmappe groß angezeigt.
        // - "Noch keinem Ereignis zugeordnet" gilt aktuell für JEDE
        //   Datei, weil es noch keine Verknüpfung zwischen dem
        //   Erinnerungsverzeichnis (Rohdateien) und bereits zugeordneten
        //   Fotos gibt - das wäre neue Datenhaltung und blieb bewusst
        //   dem Architekturauftrag entsprechend unangetastet.
        // - "Mit Ereignis verbinden" und "Person zuordnen" funktionieren
        //   nur mit genau einer ausgewählten Datei vom Typ Bild, weil
        //   Ereignis/Person aktuell jeweils nur EIN Foto besitzen können
        //   (Architekturentscheidung aus früheren Builds) - das wollte
        //   der Architekt diesmal nicht ändern ("keine neue
        //   Datenhaltung").
        // - "Arbeitskorb", "Asservatenkammer" und "Papierkorb" für
        //   Dateien sind als Platzhalter angelegt (wie schon an anderen
        //   Stellen im Programm üblich) - sie würden eine neue,
        //   dauerhafte Markierung pro Datei benötigen, die dieser Build
        //   bewusst noch nicht einführt.

        private void HauptTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Build 2.8, entscheidender Fund: SelectionChanged ist in WPF
            // ein "bubbling" Ereignis - es steigt im Steuerelement-Baum
            // nach oben. JEDE ComboBox innerhalb der Arbeitsmappe (Personen-
            // Auswahl, Ereignis-Auswahl, Jahreszeit usw.) ist ebenfalls ein
            // Selector und löst beim Auswählen ebenfalls SelectionChanged
            // aus - das bis hierher zum TabControl hochsteigt, obwohl sich
            // der Reiter dabei gar nicht geändert hat. Ohne diese Prüfung
            // wurde dadurch bei JEDER Auswahl in einer Arbeitsmappen-
            // ComboBox versehentlich OeffneArbeitsmappe() erneut ausgelöst -
            // das leert arbeitsmappeAusgewaehlt vollständig und schließt
            // alle offenen Unterformulare. Genau das war die gemeinsame
            // Ursache für beide gemeldeten Fehler (Ereignisfeld verschwindet,
            // Erinnerungen kommen nirgends an).
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

        // Build 2.8, Punkt 4: sichtbarer Zähler + Zeitstempel, der beweist,
        // ob/wann OeffneArbeitsmappe() tatsächlich (erneut) ausgeführt
        // wird. Bleibt bewusst dauerhaft sichtbar (kein Verschwinden), damit
        // beim Test sofort auffällt, falls die Methode unerwartet mehrfach
        // läuft, während der Benutzer nur in einer ComboBox auswählt.
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
                // Nicht lesbar - dann eben mit leerer Liste weiterarbeiten
                // (reine Komfortfunktion für die Statusanzeige).
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
                // Reine Komfortfunktion für die Statusanzeige - schlägt das
                // Speichern fehl, wird die eigentliche Zuordnung dadurch
                // nicht gestört.
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
                // Erinnerungsverzeichnis nicht lesbar - dann eben eine
                // leere Arbeitsmappe zeigen, kein Fehlerdialog nötig.
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

        // Doppelklick: Bilder werden direkt in der Arbeitsmappe groß
        // angezeigt. Alle anderen Dateitypen (PDF, Dokumente, Videos,
        // Audio) werden ehrlicherweise mit dem Standardprogramm des
        // Betriebssystems geöffnet - ein eigener Betrachter dafür ist
        // bewusst (noch) nicht eingebaut.
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

        // ---- Werkzeugbereich rechts: Aktionen für die Auswahl ----

        private void AktualisiereArbeitsmappenWerkzeuge()
        {
            int anzahl = arbeitsmappeAusgewaehlt.Count;

            ArbeitsmappeAuswahlText.Text = James.ArbeitsmappeAuswahlText(anzahl);

            // Build 2.4: "Erinnerungen zuordnen" (Person) erlaubt jetzt
            // ebenfalls Mehrfachauswahl - eine Person besitzt eine ganze
            // Erinnerungssammlung, nicht nur ein einzelnes Titelbild.
            ArbeitsmappeNeuesEreignisAnlegenButton.IsEnabled = anzahl > 0;
            ArbeitsmappeMitEreignisVerbindenButton.IsEnabled = anzahl > 0;
            ArbeitsmappeNeuePersonAnlegenButton.IsEnabled = anzahl > 0;
            ArbeitsmappePersonZuordnenButton.IsEnabled = anzahl > 0;
            ArbeitsmappeMarkierungAufhebenButton.IsEnabled = anzahl > 0;

            // Build 5.0: die beiden Wege für freie (personenunabhängige)
            // Ereignisse - gleichberechtigt neben den Personen-Wegen.
            ArbeitsmappeNeuesFreiesEreignisButton.IsEnabled = anzahl > 0;
            ArbeitsmappeFreiesEreignisZuordnenButton.IsEnabled = anzahl > 0;

            // Bug 2 (Build 2.4): Vorher wurden hier IMMER alle offenen
            // Unterformulare geschlossen, sobald sich die Auswahl auch
            // nur um eine einzige Erinnerung änderte (z.B. weiter
            // Kästchen angekreuzt, während man mitten in "Neues Ereignis
            // anlegen" war) - das ließ Formulare scheinbar grundlos
            // "verschwinden". Jetzt werden die Unterformulare nur noch
            // geschlossen, wenn die Auswahl komplett aufgehoben wurde -
            // dann ergibt eine offene Zuordnung ohnehin keinen Sinn mehr.
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

        // ---- Weg 1: Vorhandenem Ereignis zuordnen (jetzt mit Mehrfachauswahl) ----

        private void ArbeitsmappeMitEreignisVerbinden_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            // Etappe A.6: dieselbe Korrektur wie bereits bei Weg 4
            // (ArbeitsmappePersonZuordnen_Click, Build 2.5) - ein bereits
            // bei einer archivierten Person angelegtes Ereignis muss
            // trotzdem zuverlässig auswählbar bleiben. Vorher wurden hier
            // ausschließlich Personen vom Schreibtisch angeboten, wodurch
            // deren Ereignisse für die Arbeitsmappe unsichtbar wurden,
            // sobald die Person archiviert war.
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

            // Build 2.7: sichtbare Diagnose statt Annahme - zeigt sofort und
            // überprüfbar, ob der Handler tatsächlich ausgeführt wurde und
            // wie viele Ereignisse die gewählte Person tatsächlich besitzt.
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

        // Gemeinsame Kopier-/Verknüpfungslogik: Die erste Erinnerung wird
        // (falls noch nicht vorhanden) zum Titelbild des Ereignisses, alle
        // weiteren landen in WeitereFotoDateinamen (Build 2.2). Wird sowohl
        // von "Vorhandenem Ereignis zuordnen" als auch vom neuen "Neues
        // Ereignis anlegen"-Weg genutzt. Andere Dateitypen als Bilder
        // werden dabei übersprungen statt einen Fehler zu zeigen.
        // Etappe B.1: der eigentliche Kopiervorgang ist für Personen- und
        // freie Ereignisse identisch und wurde deshalb hierher
        // zusammengeführt. Was anschließend mit dem Ergebnis geschieht
        // (Meldungstext, welche Anzeigen aktualisiert werden), bleibt
        // bewusst in den beiden Aufrufern getrennt - das unterscheidet
        // sich zwischen Personen- und freien Ereignissen tatsächlich
        // (z.B. der zusätzliche Erinnerungs-Zähler der Person).
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

                    // Build 2.3, Punkt 1: "Ereignis öffnen" anbieten, ohne
                    // die Arbeitsmappe zu verlassen (rein optional, auf
                    // Wunsch des Benutzers).
                    ZeigeArbeitsmappeEreignisOeffnenButton(person, ereignis);
                }

                if (verbunden > 0)
                {
                    // Build 2.3, Punkt 4: alle betroffenen Anzeigen sofort
                    // aktualisieren - Erinnerungskarte (falls sie genau
                    // dieses Ereignis zeigt), Schreibtisch (falls genau
                    // diese Person dort gerade ausgewählt ist, inkl.
                    // Erinnerungen-Zähler) und die Arbeitsmappe selbst
                    // (Kachel-Status, siehe Aufrufer).
                    AktualisiereErinnerungskarteFallsBetroffen(person, ereignis);
                    AktualisiereSchreibtischFallsBetroffen(person);
                }
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimSpeichernFoto(ex.Message));
            }
        }

        // ---- Weg 2: Neues Ereignis anlegen (Build 2.2) ----
        // Da ein Ereignis architektonisch stets zu einer Person gehört,
        // wird zunächst kurz gefragt, für welche Person es angelegt wird -
        // danach übernimmt das bekannte Ereignisformular auf dem
        // Schreibtisch, und James kehrt nach dem Speichern von selbst
        // zur Arbeitsmappe zurück.

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

        // Build 2.2a: Speichert das neue Ereignis vollständig innerhalb
        // der Arbeitsmappe - kein Wechsel zum Schreibtisch, kein
        // Windows-Dateidialog. Die markierten Erinnerungen werden direkt
        // im Anschluss verknüpft.
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

            // Build 2.3, Punkt 1+4: Das Ereignis wird sofort gespeichert,
            // unabhängig davon, ob im Anschluss auch Fotos verknüpft
            // werden konnten - so geht es nie "unsichtbar" verloren.
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

        // ---- Weg 3: Neue Person anlegen (Build 2.2a, komplett inline) ----

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

        // Build 2.2a: Legt die neue Person vollständig innerhalb der
        // Arbeitsmappe an - kein Wechsel zum Schreibtisch, kein
        // Windows-Dateidialog. Die markierte Erinnerung wird direkt im
        // Anschluss als Titelbild übernommen.
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

            // Wie beim Ereignis (Build 2.3): die Person wird sofort
            // gespeichert, unabhängig davon, ob im Anschluss auch
            // Erinnerungen zugeordnet werden konnten.
            SpeichereDaten();

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();

            VerknuepfeArbeitsmappenDateienMitPerson(neuePerson, pfade);

            ArbeitsmappeNeuePersonPanel.Visibility = Visibility.Collapsed;

            AktualisiereArbeitsmappe();
        }

        // ============================================================
        // BUILD 5.1: DIE STARTSEITE ALS ZENTRALER EINSTIEGSPUNKT
        // ============================================================
        // "Womit möchten wir uns heute beschäftigen?" - der Benutzer
        // trifft genau eine Entscheidung, James stellt anschließend die
        // passende Arbeitsumgebung bereit. Dokumente/Erinnerungen
        // durchsuchen/Letzte Arbeit fortsetzen sind bewusst nur sichtbar,
        // aber deaktiviert (IsEnabled="False" in der XAML) - sie erhalten
        // erst in späteren Bauabschnitten ihre Funktion.

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

            // Build 6.0: der Vorschlag kann sich seit dem letzten Besuch
            // der Startseite verändert haben (z.B. weil gerade eine neue
            // Erinnerung zugeordnet wurde) - deshalb sicherheitshalber
            // neu ermitteln.
            ZeigeStartseiteVorschlag();
        }

        // ============================================================
        // BUILD 5.1: EREIGNISVERWALTUNG (DIESELBE BEDIENLOGIK WIE
        // DIE PERSONENVERWALTUNG)
        // ============================================================
        // Ein einfaches Eingabefeld ("Ereignis: ____") - James macht
        // keinerlei Namensvorschläge, der Benutzer vergibt den Namen
        // selbst. Auswählen eines vorhandenen Ereignisses in der Liste
        // füllt das Formular zum Bearbeiten, genau wie bei Personen.

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
                // Vorhandenes Ereignis bearbeiten - unverändert wie bisher,
                // dieselbe Bedienlogik wie beim Bearbeiten einer Person.
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
                // Build 6.0: Ein neu angelegtes Ereignis wird zur Mappe -
                // James kehrt NICHT zu einer leeren Eingabemaske zurück,
                // sondern öffnet unmittelbar die neue Ereignismappe.
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
        // Nach dem Anlegen eines neuen Ereignisses fragt James sofort,
        // was der Benutzer der Mappe hinzufügen möchte. Nur "Bilder"
        // funktioniert bereits vollständig (nutzt die bestehende
        // Arbeitsmappe, Build 5.0) - Dokumente/Videos/Kommentare sind
        // bewusst nur sichtbar, aber deaktiviert (kein Platzhalterdialog,
        // wie ausdrücklich gewünscht) und dienen als vorbereitete
        // Arbeitsbereiche für spätere Bauabschnitte.

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

            // Wiederverwendung der bestehenden Arbeitsmappe (Build 5.0):
            // dort ordnet der Benutzer markierte Erinnerungen über
            // "Vorhandenem Ereignis zuordnen" bereits jetzt diesem
            // Ereignis zu - keine neue Zuordnungslogik nötig.
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
        // Architekturerweiterung: ein "freies" Ereignis ist eigenständig,
        // unabhängig von jeder Person. James macht dabei keinerlei
        // Namensvorschläge - der Benutzer vergibt den Namen selbst, und
        // nur selbst angelegte Ereignisse erscheinen später zur Auswahl.
        // Grundsatz: James verwaltet ausschließlich Wissen, das der
        // Benutzer selbst angelegt oder ausdrücklich bestätigt hat.

        // ---- Weg: Neues freies Ereignis anlegen ----

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

            // Etappe A.5, Punkt 3: versehentliche Dubletten vermeiden -
            // James fragt höflich nach, statt kommentarlos ein weiteres,
            // gleichnamiges Ereignis anzulegen. Der Benutzer kann sich
            // trotzdem bewusst für ein neues Ereignis entscheiden.
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

            // Wie bei Person/Ereignis (Build 2.2a/2.3): sofort speichern,
            // unabhängig davon, ob im Anschluss auch eine Erinnerung
            // zugeordnet werden kann - das Ereignis darf nie "unsichtbar"
            // verloren gehen.
            SpeichereDaten();
            AktualisiereFreieEreignisseAnzeige();

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();
            VerknuepfeArbeitsmappenDateienMitFreiemEreignis(zielEreignis, pfade);

            ArbeitsmappeNeuesFreiesEreignisPanel.Visibility = Visibility.Collapsed;
            AktualisiereArbeitsmappe();
        }

        // ---- Weg: Vorhandenem freien Ereignis zuordnen ----

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

            // Die Auswahlliste enthält ausschließlich bereits vom Benutzer
            // selbst angelegte Ereignisse (auch archivierte) - James
            // erzeugt keine eigenen Vorschläge.
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

        // Gemeinsame Kopier-/Verknüpfungslogik für freie Ereignisse -
        // arbeitet genau wie VerknuepfeArbeitsmappenDateienMitEreignis,
        // nur ohne Personenbezug und mit eigenem Ordnerzweig.
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

        // ---- Übersicht "Meine freien Ereignisse" ----

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

        // Etappe A.5, Punkt 2: dieselbe Bedienlogik wie Loeschen_Click bei
        // Personen (Papierkorb statt sofortigem, endgültigem Löschen),
        // hier für ein oder mehrere freie Ereignisse.
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

        // ---- Build 6.0, Punkt 6: Archivierte Ereignisse (dieselbe
        // Bedienlogik wie das bereits bestehende Personen-Archiv) ----

        private void ArchivEreignisseListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ArchivEreignisAktionPanel.Visibility = Visibility.Collapsed;
        }

        // Etappe B.1b, Punkt 1+3: dasselbe Bedienprinzip wie ArchivAktion_Click
        // beim Personen-Archiv.
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

        // Etappe A.5, Punkt 2: auch aus dem Ereignis-Archiv heraus soll
        // ein Ereignis in den Papierkorb gelegt werden können - dieselbe
        // Logik wie FreiesEreignisInPapierkorb_Click, nur ausgehend von
        // freieEreignisseArchiv statt freieEreignisse.
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

        // ---- Etappe A.5, Punkt 2: Papierkorb für freie Ereignisse
        // (dieselbe Bedienlogik wie Wiederherstellen_Click/
        // EndgueltigLoeschen_Click bei Personen) ----

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

        // Blendet alle Arbeitsmappen-Unterformulare aus - wird vor dem
        // Öffnen eines einzelnen Weges aufgerufen, damit niemals zwei
        // gleichzeitig sichtbar sind.
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

        // Build 2.3, Punkt 1: "Ereignis öffnen" merken und anbieten, ohne
        // die Arbeitsmappe automatisch zu verlassen - der Benutzer
        // entscheidet selbst, ob er jetzt hinüberspringen möchte.
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

        // Gemeinsame Verknüpfungslogik für den Weg "Neue/Vorhandene Person
        // zuordnen" - genau eine Erinnerung wird zum Titelbild, analog zu
        // ArbeitsmappeTitelbildBestaetigen_Click.
        // Build 2.4, Bugfix "Titelbild ≠ Erinnerungssammlung": Jede
        // ausgewählte Erinnerung wird jetzt dauerhaft in
        // Person.ErinnerungsDateinamen aufgenommen (die eigentliche
        // Sammlung). Das Titelbild wird nur automatisch gesetzt, wenn die
        // Person noch gar keines besitzt - ein bereits gewähltes
        // Titelbild wird durch weitere Erinnerungen NICHT überschrieben.
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

                // Build 2.3/2.4, Punkt 3+4: sofort überall aktualisieren,
                // wo diese Person gerade sichtbar sein könnte - niemals
                // das Bild/die Sammlung einer anderen Person stehen lassen.
                AktualisiereSchreibtischFallsBetroffen(person);
                AktualisiereErinnerungskarteFallsBetroffen(person, null);
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimSpeichernFoto(ex.Message));
            }
        }

        // ---- Weg 4: Erinnerungen einer vorhandenen Person zuordnen (jetzt mit Mehrfachauswahl) ----

        private void ArbeitsmappePersonZuordnen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            // Build 2.5: Da der bisherige Weg über den Archiv-Reiter
            // ("Foto hinzufügen/ändern") entfällt, muss diese Liste auch
            // archivierte Personen umfassen - sonst würden archivierte
            // Personen keine Erinnerungen mehr zugeordnet bekommen können.
            List<Person> alleAuswaehlbarenPersonen = allePersonen
                .Concat(ArchivListe.Items.Cast<Person>())
                .ToList();

            ArbeitsmappeTitelbildPersonComboBox.ItemsSource = alleAuswaehlbarenPersonen;
            ArbeitsmappeTitelbildPersonComboBox.SelectedIndex = -1;

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappePersonAuswahlPanel.Visibility = Visibility.Visible;
        }

        // Build 2.7: sichtbare Diagnose - bestätigt sofort und überprüfbar,
        // welche Person ausgewählt wurde und wie viele Erinnerungen sie VOR
        // der Zuordnung bereits besitzt.
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

            // Build 2.7: sichtbare Diagnose NACH der Zuordnung - überschreibt
            // bewusst absichtlich die Meldung aus VerknuepfeArbeitsmappen-
            // DateienMitPerson, damit die tatsächlich aktuell im
            // Datenbestand stehende Zahl unmittelbar überprüfbar ist.
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
        // Baut "alleEreignisse" ausschließlich aus den bereits vorhandenen,
        // unveränderten Strukturen auf - rein additiv, rein lesend, keine
        // neue Persistenz. Wird aktuell von keiner Oberfläche verwendet;
        // dient ausschließlich als geprüfte Grundlage für spätere Etappen
        // (z.B. eine Suche, die auch freie Ereignisse findet).
        //
        // Bewusst analog zum bestehenden Muster der James-Suche
        // (SammleTrefferFuerPerson/FuehreJamesSucheAus): Personen auf dem
        // Schreibtisch und im Archiv werden berücksichtigt, der Papierkorb
        // bewusst nicht (dort liegen Erinnerungen, die entfernt werden
        // sollen - genau wie die bisherige Suche das schon handhabt).
        // Etappe B (Build 2.9)/Build 3.0: vom Erinnerungsfenster aus
        // aufgerufen - liest die bisher gemerkten Merkmale (samt
        // Kategorie) zu genau einer Erinnerung (identifiziert über ihren
        // Dateinamen, siehe ErinnerungsGedaechtnisEintrag oben). Gibt
        // bewusst Kopien zurück, keine internen Referenzen. Rein lesend.
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

        // Speichert die vollständige, aktuelle Liste der Merkmale zu
        // einer Erinnerung. Der Benutzer hat bereits selbst
        // hinzugefügt/entfernt (James' erstes Gesetz) - dieser Aufruf
        // schreibt nur noch das Ergebnis.
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
        // Build 3.1: liefert James' Wissensbibliothek (MerkmalAuswertung)
        // den Zugriff auf das gesamte Erinnerungsgedächtnis, ohne dass
        // ErinnerungenFenster selbst dieses Feld kennen muss - gleiches
        // Prinzip wie schon bei LiesVisuelleMerkmale/SpeichereVisuelleMerkmale.
        private int ZaehleVorkommenVisuellesMerkmal(string bezeichnung, string kategorie, string aktuellerDateiname)
        {
            return MerkmalAuswertung.ZaehleVorkommen(erinnerungsGedaechtnis, bezeichnung, kategorie, aktuellerDateiname);
        }

        private void AktualisiereEinheitlicheEreignisliste()
        {
            List<EreignisEintrag> neu = new List<EreignisEintrag>();

            // Personengebundene Ereignisse - Schreibtisch
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

            // Personengebundene Ereignisse - Archiv
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

            // Freie Ereignisse (Build 5.0)
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

        private void SortiereAllePersonen()
        {
            allePersonen = allePersonen
                .OrderBy(p => p.Nachname ?? "")
                .ThenBy(p => p.Vorname ?? "")
                .ToList();
        }

        // Zeigt in PersonenListe nur die Personen, die zum Suchtext passen
        private void AktualisierePersonenAnzeige()
        {
            Person vorherAusgewaehlt = PersonenListe.SelectedItem as Person;
            string suchtext = SucheTextBox == null ? "" : SucheTextBox.Text.Trim().ToLower();

            IEnumerable<Person> gefiltert = allePersonen;

            if (suchtext != "")
            {
                gefiltert = allePersonen.Where(p =>
                    (p.Vorname != null && p.Vorname.ToLower().Contains(suchtext)) ||
                    (p.Nachname != null && p.Nachname.ToLower().Contains(suchtext)));
            }

            PersonenListe.Items.Clear();

            foreach (Person person in gefiltert)
            {
                PersonenListe.Items.Add(person);
            }

            if (vorherAusgewaehlt != null && PersonenListe.Items.Contains(vorherAusgewaehlt))
            {
                PersonenListe.SelectedItem = vorherAusgewaehlt;
            }

            AnzahlText.Text = allePersonen.Count + " Erinnerungen";
        }

        private void Suche_TextChanged(object sender, TextChangedEventArgs e)
        {
            AktualisierePersonenAnzeige();
        }

       

        private void Speichern_Click(object sender, RoutedEventArgs e)
        {
            string vorname = VornameTextBox.Text.Trim();
            string nachname = NachnameTextBox.Text.Trim();

            if (vorname == "" && nachname == "")
            {
                James.Hinweis(James.BitteErstNamenEingeben);

                VornameTextBox.Focus();
                return;
            }

            Person gespeichertePerson;

            if (aktuellBearbeitetePerson != null)
            {
                // Vorhandene Person aktualisieren.
                aktuellBearbeitetePerson.Vorname = vorname;
                aktuellBearbeitetePerson.Nachname = nachname;
                aktuellBearbeitetePerson.Geburt = GeburtTextBox.Text.Trim();
                aktuellBearbeitetePerson.Ort = OrtTextBox.Text.Trim();
                aktuellBearbeitetePerson.Beziehung = ErstelleBeziehungAusEingabe();
                aktuellBearbeitetePerson.ModifiedAt = DateTime.Now;

                SortiereAllePersonen();
                AktualisierePersonenAnzeige();

                SpeichereDaten();

                ZeigeStatusMeldung(James.ErinnerungAktualisiert(aktuellBearbeitetePerson.ToString()));

                gespeichertePerson = aktuellBearbeitetePerson;
            }
            else
            {
                // Neue Person anlegen
                Person neuePerson = new Person
                {
                    Vorname = vorname,
                    Nachname = nachname,
                    Geburt = GeburtTextBox.Text.Trim(),
                    Ort = OrtTextBox.Text.Trim(),
                    Beziehung = ErstelleBeziehungAusEingabe()
                };

                allePersonen.Add(neuePerson);

                SortiereAllePersonen();
                AktualisierePersonenAnzeige();

                SpeichereDaten();

                ZeigeStatusMeldung(James.ErinnerungGespeichert(neuePerson.ToString()));

                gespeichertePerson = neuePerson;
            }

            // Ergonomie (Build 2.1a): Nach jedem erfolgreichen Speichern -
            // ob neu angelegt oder aktualisiert - wird das Formular
            // vollständig zurückgesetzt. Der Benutzer kann dadurch sofort
            // die nächste Person erfassen, ohne die vorherige zuerst
            // archivieren zu müssen. Da gleichzeitig aktuellBearbeitetePerson
            // auf null gesetzt wird, würde ein versehentlicher zweiter Klick
            // auf "Speichern" bei leeren Feldern nur den Hinweis "bitte
            // Namen eingeben" zeigen, statt Daten zu überschreiben.
            aktuellBearbeitetePerson = null;

            VornameTextBox.Clear();
            NachnameTextBox.Clear();
            GeburtTextBox.Clear();
            OrtTextBox.Clear();
            BeziehungRolleComboBox.Text = "";

            if (PersonenListe.SelectedItem != null)
            {
                PersonenListe.SelectedItem = null;
            }

            VornameTextBox.Focus();
        }

        private void Loeschen_Click(object sender, RoutedEventArgs e)
        {
            List<Person> ausgewaehltePersonen = PersonenListe.SelectedItems.Cast<Person>().ToList();

            if (ausgewaehltePersonen.Count == 0)
            {
                James.Hinweis(James.BittePersonenAuswaehlen);

                return;
            }

            string frage;

            if (ausgewaehltePersonen.Count == 1)
            {
                frage = James.FrageInPapierkorbEinzeln(ausgewaehltePersonen[0].ToString());
            }
            else
            {
                frage = James.FrageInPapierkorbMehrere(ausgewaehltePersonen.Count);
            }

            bool ergebnis = James.FrageJaNein(frage, James.TitelEntscheidung, MessageBoxImage.Warning);

            if (ergebnis)
            {
                foreach (Person person in ausgewaehltePersonen)
                {
                    allePersonen.Remove(person);
                    PapierkorbListe.Items.Add(person);

                    if (aktuellBearbeitetePerson == person)
                    {
                        aktuellBearbeitetePerson = null;
                    }
                }

                AktualisierePersonenAnzeige();
                SpeichereDaten();

                VornameTextBox.Clear();
                NachnameTextBox.Clear();
                GeburtTextBox.Clear();
                OrtTextBox.Clear();
                BeziehungRolleComboBox.Text = "";

                PersonFotoBild.Source = null;
                EreignisseListe.Items.Clear();
                EreignisAuswahlPanel.Visibility = Visibility.Collapsed;
                EreignisFotoBild.Source = null;

                if (ausgewaehltePersonen.Count == 1)
                {
                    ZeigeStatusMeldung(James.InPapierkorbGelegtEinzeln(ausgewaehltePersonen[0].ToString()));
                }
                else
                {
                    ZeigeStatusMeldung(James.InPapierkorbGelegtMehrere(ausgewaehltePersonen.Count));
                }
            }
        }

        private void Bearbeiten_Click(object sender, RoutedEventArgs e)
        {
            if (PersonenListe.SelectedItem == null)
            {
                James.Hinweis(James.BittePersonAuswaehlen);

                return;
            }

            Person person = PersonenListe.SelectedItem as Person;

            if (person == null)
            {
                return;
            }

            VornameTextBox.Text = person.Vorname;
            NachnameTextBox.Text = person.Nachname;
            GeburtTextBox.Text = person.Geburt;
            OrtTextBox.Text = person.Ort;

            aktuellBearbeitetePerson = person;

            ZeigeBeziehung(person);

            VornameTextBox.Focus();
        }

        private void Archivieren_Click(object sender, RoutedEventArgs e)
        {
            List<Person> ausgewaehltePersonen = PersonenListe.SelectedItems.Cast<Person>().ToList();

            if (ausgewaehltePersonen.Count == 0)
            {
                James.Hinweis(James.BittePersonenAuswaehlen);

                return;
            }

            foreach (Person person in ausgewaehltePersonen)
            {
                allePersonen.Remove(person);
                ArchivListe.Items.Add(person);

                if (aktuellBearbeitetePerson == person)
                {
                    aktuellBearbeitetePerson = null;
                }
            }

            AktualisierePersonenAnzeige();
            SpeichereDaten();

            VornameTextBox.Clear();
            NachnameTextBox.Clear();
            GeburtTextBox.Clear();
            OrtTextBox.Clear();
            BeziehungRolleComboBox.Text = "";

            PersonFotoBild.Source = null;
            EreignisseListe.Items.Clear();
            EreignisAuswahlPanel.Visibility = Visibility.Collapsed;
            EreignisFotoBild.Source = null;

            if (ausgewaehltePersonen.Count == 1)
            {
                ZeigeStatusMeldung(James.ImArchivAngekommen(ausgewaehltePersonen[0].ToString()));
            }
            else
            {
                ZeigeStatusMeldung(James.ImArchivAngekommenMehrere(ausgewaehltePersonen.Count));
            }
        }

        private void Wiederherstellen_Click(object sender, RoutedEventArgs e)
        {
            List<Person> ausgewaehltePersonen = PapierkorbListe.SelectedItems.Cast<Person>().ToList();

            if (ausgewaehltePersonen.Count == 0)
            {
                James.Hinweis(James.BittePapierkorbAuswaehlen);

                return;
            }

            foreach (Person person in ausgewaehltePersonen)
            {
                PapierkorbListe.Items.Remove(person);
                allePersonen.Add(person);
            }

            SortiereAllePersonen();
            AktualisierePersonenAnzeige();

            SpeichereDaten();

            if (ausgewaehltePersonen.Count == 1)
            {
                James.Hinweis(James.WiederhergestelltEinzeln(ausgewaehltePersonen[0].ToString()), James.TitelWiederhergestellt);
            }
            else
            {
                James.Hinweis(James.WiederhergestelltMehrere(ausgewaehltePersonen.Count), James.TitelWiederhergestellt);
            }
        }

        private void EndgueltigLoeschen_Click(object sender, RoutedEventArgs e)
        {
            List<Person> ausgewaehltePersonen = PapierkorbListe.SelectedItems.Cast<Person>().ToList();

            if (ausgewaehltePersonen.Count == 0)
            {
                James.Hinweis(James.BittePapierkorbAuswaehlen);

                return;
            }

            string frage;

            if (ausgewaehltePersonen.Count == 1)
            {
                frage = James.FrageEndgueltigLoeschenEinzeln(ausgewaehltePersonen[0].ToString());
            }
            else
            {
                frage = James.FrageEndgueltigLoeschenMehrere(ausgewaehltePersonen.Count);
            }

            bool ergebnis = James.FrageJaNein(frage, James.TitelEndgueltigeEntscheidung, MessageBoxImage.Warning);

            if (ergebnis)
            {
                foreach (Person person in ausgewaehltePersonen)
                {
                    PapierkorbListe.Items.Remove(person);
                }

                SpeichereDaten();
            }
        }
    }
}
