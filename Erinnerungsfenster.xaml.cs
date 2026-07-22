using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DAS_LEBENSARCHIV
{
    // ============================================================
    // BUILD 3.0: KONTEXT ZU EINER ERINNERUNG
    // ============================================================
    // Ein einzelner Eintrag für das Erinnerungsfenster - ein Dateipfad
    // plus die bereits vorhandenen, bekannten Informationen dazu. Alle
    // Textfelder dürfen leer sein (z.B. bei direkt einer Person
    // zugeordneten Erinnerungen ohne Ereignis) - das Fenster blendet
    // fehlende Angaben dann einfach aus.
    // ============================================================
    public class ErinnerungsInfo
    {
        public string Pfad { get; set; }
        public string Titel { get; set; }
        public string Datum { get; set; }
        public string Ort { get; set; }
        public string Beschreibung { get; set; }
        public string PersonName { get; set; }
        public List<string> Beteiligte { get; set; }
    }

    // ============================================================
    // BUILD 2.9/3.0/3.1/3.2: JAMES ZEIGT ERINNERUNGEN AN, NAVIGIERT UND
    // MERKT SICH DIE LETZTE POSITION (Etappe B.1 + B.2 + B.3 + B.4)
    // ============================================================
    // Ein bewusst einfaches, eigenständiges Fenster. Bekommt beim
    // Öffnen eine Überschrift und eine Liste von ErinnerungsInfo mit -
    // mehr Wissen über Personen/Ereignisse braucht dieses Fenster nicht,
    // das hält es unabhängig und einfach testbar.
    // ============================================================
    public partial class ErinnerungenFenster : Window
    {
        private readonly List<ErinnerungsInfo> erinnerungen;

        // Build 3.1: Nur die tatsächlich ladbaren Erinnerungen, in
        // genau der Reihenfolge, in der sie auch in der Übersicht
        // erscheinen. Das Vor-/Zurück-Blättern arbeitet ausschließlich
        // auf dieser Liste, damit Übersicht und Navigation immer
        // konsistent dieselbe Reihenfolge zeigen.
        private readonly List<ErinnerungsInfo> gueltigeErinnerungen = new List<ErinnerungsInfo>();
        private int aktuellerIndex = -1;

        // Build 4.1: Index des ersten zur aktuellen Suche passenden
        // Treffers in gueltigeErinnerungen (-1 = kein Treffer bzw. keine
        // aktive Suche). Wird von RenderMiniaturen aktuell gehalten und
        // von der Enter-Taste im Suchfeld verwendet.
        private int ersterTrefferIndex = -1;

        // Build 4.2: Sortierkriterium ("Datum", "Titel" oder "Person") und
        // Sortierrichtung. Bewusst keine Speicherung über die Sitzung
        // hinaus gefordert - jedes neu geöffnete Erinnerungsfenster startet
        // daher wieder mit dem Standard (Datum, aufsteigend).
        private string sortierKriterium = "Datum";
        private bool sortierAufsteigend = true;

        // Build 3.2: merkt sich die zuletzt geöffnete Erinnerung - bewusst
        // nur im Arbeitsspeicher (static, kein neues Datenfeld, keine
        // Datei), gilt daher nur für die laufende Sitzung des Programms.
        // "static", damit sie über mehrere Öffnungen des Fensters hinweg
        // erhalten bleibt (jede Öffnung erzeugt eine neue Fenster-Instanz).
        private static string zuletztGeoeffneterPfad = null;

        // Build 3.5: merkt sich den zuletzt verwendeten Zoomfaktor - genau
        // wie zuletztGeoeffneterPfad bewusst nur "static" (Arbeitsspeicher,
        // keine Datei, keine Datenbank) und dadurch automatisch nur für die
        // laufende Programmsitzung gültig. Nach einem Neustart des
        // Programms beginnt eine neue, frische Sitzung bei 1.0 (Standard).
        private static double zuletzterZoomFaktor = 1.0;

        private const double MindestZoom = 0.3;
        private const double MaximalZoom = 4.0;
        private const double ZoomSchritt = 0.1;

        // Etappe B (Build 2.9): James' visuelles Gedächtnis. Bewusst als
        // Funktionen statt eines direkten Verweises auf MainWindow
        // hereingereicht - das Erinnerungsfenster bleibt dadurch weiterhin
        // unabhängig und einfach testbar (siehe Kommentar oben).
        private readonly Func<string, List<VisuellesMerkmal>> liesMerkmale;
        private readonly Action<string, List<VisuellesMerkmal>> speichereMerkmale;

        // Build 3.1: James' Wissensbibliothek (MerkmalAuswertung) - liefert
        // die Anzahl weiterer Erinnerungen mit demselben bestätigten
        // Merkmal. Bewusst wieder als Funktion hereingereicht, wie schon
        // liesMerkmale/speichereMerkmale - das Fenster kennt weiterhin
        // keine Details über MainWindow oder das Erinnerungsgedächtnis.
        private readonly Func<string, string, string, int> zaehleVorkommenMerkmal;

        public ErinnerungenFenster(
            string titel,
            List<ErinnerungsInfo> erinnerungen,
            Func<string, List<VisuellesMerkmal>> liesMerkmale,
            Action<string, List<VisuellesMerkmal>> speichereMerkmale,
            Func<string, string, string, int> zaehleVorkommenMerkmal)
        {
            InitializeComponent();

            UeberschriftText.Text = titel;
            this.erinnerungen = erinnerungen ?? new List<ErinnerungsInfo>();
            this.liesMerkmale = liesMerkmale;
            this.speichereMerkmale = speichereMerkmale;
            this.zaehleVorkommenMerkmal = zaehleVorkommenMerkmal;

            // Build 3.5: den gemerkten Zoomfaktor sofort anwenden - er
            // bleibt danach unverändert bestehen, solange dieses Fenster
            // geöffnet ist (auch beim Blättern zur nächsten Erinnerung,
            // da die Transformation am Bildsteuerelement selbst hängt und
            // beim Wechsel des Bildinhalts nicht zurückgesetzt wird).
            BildZoomTransform.ScaleX = zuletzterZoomFaktor;
            BildZoomTransform.ScaleY = zuletzterZoomFaktor;

            ZeigeUebersicht();

            // Build 3.2: die zuletzt geöffnete Erinnerung automatisch
            // wieder anzeigen, falls vorhanden. Ist sie in dieser Liste
            // nicht (mehr) vorhanden, wird stattdessen die erste
            // verfügbare Erinnerung gezeigt - existiert überhaupt noch
            // keine gemerkte Erinnerung (erste Nutzung), bleibt es bei
            // der gewohnten Übersicht.
            if (!string.IsNullOrEmpty(zuletztGeoeffneterPfad))
            {
                int gefundenerIndex = gueltigeErinnerungen.FindIndex(eintrag => eintrag.Pfad == zuletztGeoeffneterPfad);

                if (gefundenerIndex >= 0)
                {
                    ZeigeGross(gefundenerIndex);
                }
                else if (gueltigeErinnerungen.Count > 0)
                {
                    ZeigeGross(0);
                }
            }

            // Build 4.2: die Sortierauswahl wird bewusst erst hier gesetzt -
            // zu diesem Zeitpunkt existieren garantiert alle Steuerelemente
            // (MiniaturenPanel, TrefferAnzahlText usw.), sodass das
            // dadurch ausgelöste SortierungComboBox_SelectionChanged
            // gefahrlos ausgeführt werden kann.
            SortierungComboBox.SelectedIndex = 0;
        }

        // Baut die gültige Liste aller Erinnerungen auf (unverändert seit
        // Build 3.1 - wird für Navigation, Positions- und Zoomspeicherung
        // benötigt und bleibt dafür unabhängig von der Suche immer
        // vollständig). Fehlende oder nicht ladbare Dateien werden
        // übersprungen, statt einen Fehler zu zeigen.
        private void ZeigeUebersicht()
        {
            gueltigeErinnerungen.Clear();

            foreach (ErinnerungsInfo info in erinnerungen)
            {
                if (info == null || string.IsNullOrEmpty(info.Pfad) || !File.Exists(info.Pfad))
                {
                    continue;
                }

                // Kurzer Ladeversuch nur zur Prüfung, dass die Datei
                // tatsächlich ein anzeigbares Bild ist - dieselbe Prüfung
                // wie schon vor Build 4.0.
                try
                {
                    BitmapImage testbild = new BitmapImage();
                    testbild.BeginInit();
                    testbild.CacheOption = BitmapCacheOption.OnLoad;
                    testbild.DecodePixelWidth = 200;
                    testbild.UriSource = new Uri(info.Pfad);
                    testbild.EndInit();
                }
                catch
                {
                    continue;
                }

                gueltigeErinnerungen.Add(info);
            }

            // Build 4.2: Sortierung anwenden, bevor die Vorschaubilder
            // gezeichnet werden - dadurch entsprechen die Indizes in
            // gueltigeErinnerungen von Anfang an der aktuell gewählten
            // Reihenfolge (wichtig für Navigation, Fortschrittsanzeige
            // und den ersten Suchtreffer).
            SortiereGueltigeErinnerungen();

            // Build 4.0: die Vorschaubilder werden über RenderMiniaturen
            // erzeugt, damit dieselbe Methode auch bei jeder Sucheingabe
            // (Live-Filter) erneut aufgerufen werden kann, ohne die
            // gültige Liste (und damit Navigation/Index) anzutasten.
            RenderMiniaturen(SucheTextBox.Text);

            UebersichtScrollViewer.Visibility = Visibility.Visible;
            GrossansichtGrid.Visibility = Visibility.Collapsed;
        }

        // Build 4.2: sortiert gueltigeErinnerungen nach dem aktuell
        // gewählten Kriterium und in der aktuell gewählten Richtung.
        // Datum/Titel/Person sind vorhandene, freie Textfelder (keine
        // Datenstrukturänderung) - die Sortierung erfolgt deshalb als
        // einfacher Textvergleich. Fehlt eine Angabe (z.B. Datum bei
        // einer direkt einer Person zugeordneten Erinnerung ohne
        // Ereignis), wird sie wie ein leerer Text behandelt und
        // erscheint dadurch beim Aufsteigend-Sortieren zuerst.
        private void SortiereGueltigeErinnerungen()
        {
            Comparison<ErinnerungsInfo> vergleich;

            if (sortierKriterium == "Titel")
            {
                vergleich = (a, b) => string.Compare(a.Titel ?? "", b.Titel ?? "", StringComparison.CurrentCultureIgnoreCase);
            }
            else if (sortierKriterium == "Person")
            {
                vergleich = (a, b) => string.Compare(a.PersonName ?? "", b.PersonName ?? "", StringComparison.CurrentCultureIgnoreCase);
            }
            else
            {
                vergleich = (a, b) => string.Compare(a.Datum ?? "", b.Datum ?? "", StringComparison.CurrentCultureIgnoreCase);
            }

            gueltigeErinnerungen.Sort(vergleich);

            if (!sortierAufsteigend)
            {
                gueltigeErinnerungen.Reverse();
            }
        }

        // Sortiert neu und baut die Übersicht sofort mit der aktuellen
        // Suche neu auf - gemeinsamer Weg für beide Sortier-Steuerelemente.
        private void SortiereUndAktualisiere()
        {
            SortiereGueltigeErinnerungen();
            RenderMiniaturen(SucheTextBox.Text);
        }

        private void SortierungComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem ausgewaehlt = SortierungComboBox.SelectedItem as ComboBoxItem;

            if (ausgewaehlt == null)
            {
                return;
            }

            sortierKriterium = ausgewaehlt.Content.ToString();
            SortiereUndAktualisiere();
        }

        private void SortierRichtungButton_Click(object sender, RoutedEventArgs e)
        {
            sortierAufsteigend = !sortierAufsteigend;
            SortierRichtungButton.Content = sortierAufsteigend ? "Aufsteigend ▲" : "Absteigend ▼";
            SortiereUndAktualisiere();
        }

        // Build 4.0: rendert die Vorschaubilder-Kacheln, gefiltert nach
        // Suchtext (Titel/Ereignisbezeichnung oder Personenname). Arbeitet
        // ausschließlich auf gueltigeErinnerungen und deren bereits
        // feststehenden Indizes - die Suche verändert dadurch niemals die
        // Reihenfolge oder Nummerierung für die Navigation.
        private void RenderMiniaturen(string suchtext)
        {
            MiniaturenPanel.Children.Clear();

            string suchtextKlein = (suchtext ?? "").Trim().ToLowerInvariant();
            bool sucheAktiv = suchtextKlein != "";
            int treffer = 0;

            // Build 4.1: der erste zur aktuellen Suche passende Treffer
            // wird gemerkt (für Enter-Taste) und sichtbar hervorgehoben.
            // Ohne aktive Suche gibt es keinen "ersten Treffer" in diesem
            // Sinne - dann bleibt der Index -1 und Enter tut nichts.
            ersterTrefferIndex = -1;

            for (int i = 0; i < gueltigeErinnerungen.Count; i++)
            {
                ErinnerungsInfo info = gueltigeErinnerungen[i];

                bool passtZurSuche = !sucheAktiv
                    || (info.Titel != null && info.Titel.ToLowerInvariant().Contains(suchtextKlein))
                    || (info.PersonName != null && info.PersonName.ToLowerInvariant().Contains(suchtextKlein));

                if (!passtZurSuche)
                {
                    continue;
                }

                treffer++;

                if (sucheAktiv && ersterTrefferIndex == -1)
                {
                    ersterTrefferIndex = i;
                }

                bool istErsterTreffer = sucheAktiv && i == ersterTrefferIndex;

                // Build 3.2: die zuletzt geöffnete Erinnerung wird in der
                // Übersicht sichtbar markiert (kräftigerer, grüner Rahmen).
                // Build 4.1: während einer aktiven Suche hat die blaue
                // Markierung des ersten Treffers Vorrang vor dem grünen
                // "zuletzt geöffnet"-Rahmen, damit eindeutig erkennbar
                // bleibt, welche Erinnerung die Enter-Taste öffnen würde.
                bool istZuletztGeoeffnet = info.Pfad == zuletztGeoeffneterPfad;

                Brush rahmenFarbe;
                double rahmenDicke;

                if (istErsterTreffer)
                {
                    rahmenFarbe = Brushes.RoyalBlue;
                    rahmenDicke = 3;
                }
                else if (istZuletztGeoeffnet)
                {
                    rahmenFarbe = Brushes.SeaGreen;
                    rahmenDicke = 3;
                }
                else
                {
                    rahmenFarbe = Brushes.LightGray;
                    rahmenDicke = 1;
                }

                Border rahmen = new Border
                {
                    Width = 130,
                    Height = 130,
                    Margin = new Thickness(6),
                    BorderBrush = rahmenFarbe,
                    BorderThickness = new Thickness(rahmenDicke),
                    Background = Brushes.WhiteSmoke,
                    Cursor = Cursors.Hand
                };

                try
                {
                    BitmapImage bild = new BitmapImage();
                    bild.BeginInit();
                    bild.CacheOption = BitmapCacheOption.OnLoad;
                    bild.DecodePixelWidth = 200;
                    bild.UriSource = new Uri(info.Pfad);
                    bild.EndInit();

                    rahmen.Child = new Image
                    {
                        Source = bild,
                        Stretch = Stretch.Uniform
                    };
                }
                catch
                {
                    // Vorschaubild konnte nicht geladen werden - Kachel
                    // einfach ohne Bild belassen, kein Fehlerdialog fuer
                    // eine reine Anzeigefunktion.
                    continue;
                }

                // Build 3.1: der Index entspricht exakt der Position in
                // gueltigeErinnerungen - unabhängig davon, ob gerade
                // gefiltert wird oder nicht.
                int diesIndex = i;

                // Build 2.9: Einzelklick öffnet die Großansicht - bleibt
                // unverändert bestehen (siehe Build 3.4 unten).
                rahmen.MouseLeftButtonUp += (sender, e) => ZeigeGross(diesIndex);

                // Build 3.4: zusätzlich eine ausdrückliche Doppelklick-
                // Erkennung (Border besitzt kein eigenes MouseDoubleClick-
                // Ereignis wie z.B. ein Button - daher über ClickCount an
                // MouseLeftButtonDown geprüft). Öffnet dieselbe Erinnerung,
                // auf die doppelgeklickt wurde.
                rahmen.MouseLeftButtonDown += (sender, e) =>
                {
                    if (e.ClickCount == 2)
                    {
                        ZeigeGross(diesIndex);
                    }
                };

                MiniaturenPanel.Children.Add(rahmen);
            }

            // Build 4.0: Trefferanzahl sofort mitaktualisieren.
            TrefferAnzahlText.Text = "Gefunden: " + treffer + " von " + gueltigeErinnerungen.Count + " Erinnerungen";
        }

        // Build 4.0: Live-Filter - jede Eingabe rendert die Übersicht
        // sofort neu, ohne die gültige Liste selbst zu verändern.
        private void SucheTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RenderMiniaturen(SucheTextBox.Text);
        }

        // Build 4.1: Enter öffnet sofort den aktuell markierten ersten
        // Treffer in der Großansicht - ohne Treffer geschieht nichts.
        private void SucheTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ersterTrefferIndex >= 0)
            {
                ZeigeGross(ersterTrefferIndex);
            }
        }

        // Zeigt die Erinnerung an Position "index" der gültigen Liste groß
        // an, darunter die vorhandenen Informationen dazu (Build 3.0).
        // Aktualisiert außerdem die Vor-/Zurück-Schaltflächen (Build 3.1).
        private void ZeigeGross(int index)
        {
            if (index < 0 || index >= gueltigeErinnerungen.Count)
            {
                return;
            }

            ErinnerungsInfo info = gueltigeErinnerungen[index];

            if (!File.Exists(info.Pfad))
            {
                return;
            }

            try
            {
                BitmapImage bild = new BitmapImage();
                bild.BeginInit();
                bild.CacheOption = BitmapCacheOption.OnLoad;
                bild.UriSource = new Uri(info.Pfad);
                bild.EndInit();

                GrossesBild.Source = bild;
            }
            catch
            {
                return;
            }

            aktuellerIndex = index;

            // Build 3.2: diese Erinnerung als "zuletzt geöffnet" merken -
            // erst nachdem Bild und Datei erfolgreich geladen werden
            // konnten (kein Merken eines fehlerhaften Standes).
            zuletztGeoeffneterPfad = info.Pfad;

            // Titel
            bool hatTitel = !string.IsNullOrWhiteSpace(info.Titel);
            GrossansichtTitelText.Text = hatTitel ? info.Titel : "";
            GrossansichtTitelText.Visibility = hatTitel ? Visibility.Visible : Visibility.Collapsed;

            // Datum + Ort in einer Zeile
            List<string> datumUndOrt = new List<string>();

            if (!string.IsNullOrWhiteSpace(info.Datum))
            {
                datumUndOrt.Add("📅 " + info.Datum);
            }

            if (!string.IsNullOrWhiteSpace(info.Ort))
            {
                datumUndOrt.Add("📍 " + info.Ort);
            }

            GrossansichtDatumOrtText.Text = string.Join("    ", datumUndOrt);
            GrossansichtDatumOrtText.Visibility = datumUndOrt.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            // Zugeordnete Person
            bool hatPerson = !string.IsNullOrWhiteSpace(info.PersonName);
            GrossansichtPersonText.Text = hatPerson ? "👤 " + info.PersonName : "";
            GrossansichtPersonText.Visibility = hatPerson ? Visibility.Visible : Visibility.Collapsed;

            // Beschreibung/Notiz
            bool hatBeschreibung = !string.IsNullOrWhiteSpace(info.Beschreibung);
            GrossansichtBeschreibungText.Text = hatBeschreibung ? "📝 " + info.Beschreibung : "";
            GrossansichtBeschreibungText.Visibility = hatBeschreibung ? Visibility.Visible : Visibility.Collapsed;

            // Beteiligte (weitere zugeordnete Personen)
            bool hatBeteiligte = info.Beteiligte != null && info.Beteiligte.Count > 0;
            GrossansichtBeteiligteText.Text = hatBeteiligte ? "👥 Auch dabei: " + string.Join(", ", info.Beteiligte) : "";
            GrossansichtBeteiligteText.Visibility = hatBeteiligte ? Visibility.Visible : Visibility.Collapsed;

            // Etappe B (Build 2.9): James' visuelles Gedächtnis.
            RenderMerkmale();

            // Build 3.1: am Anfang/Ende der Liste werden die Schaltflächen
            // schlicht deaktiviert - kein Umbruch zum jeweils anderen Ende,
            // keine Fehlermeldung.
            VorherigeButton.IsEnabled = aktuellerIndex > 0;
            NaechsteButton.IsEnabled = aktuellerIndex < gueltigeErinnerungen.Count - 1;

            // Build 3.3: Positionsanzeige "Erinnerung X von Y" - X ist die
            // Position (1-basiert, für Menschen verständlich), Y die
            // Gesamtzahl der tatsächlich anzeigbaren Erinnerungen in
            // dieser Liste (funktioniert unverändert für Personen wie für
            // Ereignisse, da beide dieselbe gueltigeErinnerungen-Liste
            // verwenden).
            PositionsanzeigeText.Text = "Erinnerung " + (aktuellerIndex + 1) + " von " + gueltigeErinnerungen.Count;

            // Build 3.1 (Etappe B.3): das Panel "James, was weißt du
            // bereits?" wird beim Wechsel zu einer anderen Erinnerung
            // wieder ausgeblendet - der Benutzer muss die Frage für jede
            // Erinnerung erneut stellen, kein automatisches Anzeigen.
            BekanntesWissenPanel.Visibility = Visibility.Collapsed;
            BekanntesWissenPanel.Children.Clear();

            UebersichtScrollViewer.Visibility = Visibility.Collapsed;
            GrossansichtGrid.Visibility = Visibility.Visible;
        }

        private void Vorherige_Click(object sender, RoutedEventArgs e)
        {
            if (aktuellerIndex > 0)
            {
                ZeigeGross(aktuellerIndex - 1);
            }
        }

        // ============================================================
        // ETAPPE B (Build 2.9 + 3.0): JAMES' VISUELLES GEDÄCHTNIS
        // ============================================================
        // Noch keine KI, keine automatischen Vorschläge - der Benutzer
        // trägt Merkmale ausschließlich selbst ein und entfernt sie
        // ausschließlich selbst (James' erstes Gesetz, siehe
        // MainWindow.xaml.cs). Merkmale werden über den (bereits heute
        // eindeutigen) Dateinamen der aktuell angezeigten Erinnerung
        // gelesen/gespeichert - unabhängig von Person/Ereignis.
        //
        // Build 3.0: das Hinzufügen eines neuen Merkmals verlangt jetzt
        // zusätzlich eine Kategorie und geschieht deshalb nicht mehr über
        // das einfache Inline-Textfeld aus Build 2.9, sondern über den
        // neuen Dialog "James etwas beibringen" (JamesLerntFenster).
        // Entfernen bleibt weiterhin direkt über die Chips hier möglich.
        // ============================================================

        private string AktuellerDateiname()
        {
            if (aktuellerIndex < 0 || aktuellerIndex >= gueltigeErinnerungen.Count)
            {
                return null;
            }

            return Path.GetFileName(gueltigeErinnerungen[aktuellerIndex].Pfad);
        }

        private void RenderMerkmale()
        {
            MerkmalePanel.Children.Clear();

            string dateiname = AktuellerDateiname();

            if (dateiname == null || liesMerkmale == null)
            {
                return;
            }

            List<VisuellesMerkmal> merkmale = liesMerkmale(dateiname);

            foreach (VisuellesMerkmal merkmal in merkmale)
            {
                MerkmalePanel.Children.Add(ErzeugeMerkmalChip(merkmal));
            }
        }

        // Ein "Chip" - Bezeichnung plus kleiner Entfernen-Button, ganz
        // bewusst schlicht gehalten (nur Standard-WPF-Steuerelemente,
        // wie im gesamten Fenster).
        private Border ErzeugeMerkmalChip(VisuellesMerkmal merkmal)
        {
            StackPanel inhalt = new StackPanel { Orientation = Orientation.Horizontal };

            inhalt.Children.Add(new TextBlock
            {
                Text = merkmal.Bezeichnung,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });

            Button entfernenButton = new Button
            {
                Content = "✕",
                Width = 20,
                Height = 20,
                Padding = new Thickness(0),
                FontSize = 10,
                Tag = merkmal.Bezeichnung
            };
            entfernenButton.Click += MerkmalEntfernen_Click;
            inhalt.Children.Add(entfernenButton);

            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xF0, 0xE8)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x9C, 0xC2, 0x9C)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 3, 6, 3),
                Margin = new Thickness(0, 0, 6, 6),
                Child = inhalt
            };
        }

        // Build 3.0: öffnet den neuen Dialog. Übergibt die aktuelle
        // Merkmal-Liste dieser Erinnerung (der Dialog ergänzt sie direkt)
        // sowie eine kleine Speichern-Funktion, die genau diese Liste
        // zurückschreibt - dieselbe Übergabe-Idee wie beim Fenster selbst
        // (siehe liesMerkmale/speichereMerkmale oben), damit der Dialog
        // ebenfalls unabhängig und einfach testbar bleibt.
        private void JamesEtwasBeibringen_Click(object sender, RoutedEventArgs e)
        {
            string dateiname = AktuellerDateiname();

            if (dateiname == null || liesMerkmale == null || speichereMerkmale == null)
            {
                return;
            }

            List<VisuellesMerkmal> merkmale = liesMerkmale(dateiname);

            JamesLerntFenster dialog = new JamesLerntFenster(merkmale, () => speichereMerkmale(dateiname, merkmale));
            dialog.Owner = this;
            dialog.ShowDialog();

            RenderMerkmale();
        }

        // Build 3.1 (Etappe B.3): "James erinnert sich" - zeigt
        // ausschließlich bereits bestätigtes Wissen zu dieser Erinnerung
        // an. Keine Bilderkennung, keine Vermutungen, keine KI - reine
        // Auswertung der bereits vorhandenen, bestätigten Merkmale über
        // MerkmalAuswertung.
        private void JamesWasWeisstDuBereits_Click(object sender, RoutedEventArgs e)
        {
            string dateiname = AktuellerDateiname();

            if (dateiname == null || liesMerkmale == null)
            {
                return;
            }

            List<VisuellesMerkmal> merkmale = liesMerkmale(dateiname);
            List<MerkmalGruppe> gruppen = MerkmalAuswertung.GruppiereNachKategorie(merkmale);

            BekanntesWissenPanel.Children.Clear();

            if (gruppen.Count == 0)
            {
                BekanntesWissenPanel.Children.Add(new TextBlock
                {
                    Text = "Zu dieser Erinnerung weiß ich noch nichts.",
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.Gray
                });
            }
            else
            {
                BekanntesWissenPanel.Children.Add(new TextBlock
                {
                    FontWeight = FontWeights.Bold,
                    Text = "Zu dieser Erinnerung weiß ich bereits:",
                    Margin = new Thickness(0, 0, 0, 8)
                });

                foreach (MerkmalGruppe gruppe in gruppen)
                {
                    BekanntesWissenPanel.Children.Add(new TextBlock
                    {
                        FontWeight = FontWeights.Bold,
                        Text = gruppe.Kategorie,
                        Margin = new Thickness(0, 8, 0, 2)
                    });

                    foreach (string bezeichnung in gruppe.Bezeichnungen)
                    {
                        int vorkommen = zaehleVorkommenMerkmal != null
                            ? zaehleVorkommenMerkmal(bezeichnung, gruppe.Kategorie, dateiname)
                            : 0;

                        string zeile = "• " + bezeichnung;

                        if (vorkommen > 0)
                        {
                            zeile += "  (kommt außerdem in " + vorkommen + " weiteren Erinnerungen vor)";
                        }

                        BekanntesWissenPanel.Children.Add(new TextBlock
                        {
                            Text = zeile,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(8, 0, 0, 2)
                        });
                    }
                }
            }

            BekanntesWissenPanel.Visibility = Visibility.Visible;
        }

        private void MerkmalEntfernen_Click(object sender, RoutedEventArgs e)
        {
            string dateiname = AktuellerDateiname();
            string zuEntfernen = ((Button)sender).Tag as string;

            if (dateiname == null || zuEntfernen == null || liesMerkmale == null || speichereMerkmale == null)
            {
                return;
            }

            List<VisuellesMerkmal> merkmale = liesMerkmale(dateiname);
            merkmale.RemoveAll(m => m.Bezeichnung == zuEntfernen);
            speichereMerkmale(dateiname, merkmale);

            RenderMerkmale();
        }

        private void Naechste_Click(object sender, RoutedEventArgs e)
        {
            if (aktuellerIndex < gueltigeErinnerungen.Count - 1)
            {
                ZeigeGross(aktuellerIndex + 1);
            }
        }

        private void ZurueckZurUebersicht_Click(object sender, RoutedEventArgs e)
        {
            UebersichtScrollViewer.Visibility = Visibility.Visible;
            GrossansichtGrid.Visibility = Visibility.Collapsed;
        }

        // Build 3.5: Zoomen mit dem Mausrad - in sinnvollen Schritten,
        // begrenzt auf Mindest- und Maximalzoom. Der neue Faktor wird
        // sofort gemerkt (zuletzterZoomFaktor), damit er beim Blättern zur
        // nächsten Erinnerung sowie beim erneuten Öffnen des Fensters
        // innerhalb derselben Programmsitzung erhalten bleibt.
        private void GrossesBild_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double neuerFaktor = BildZoomTransform.ScaleX + (e.Delta > 0 ? ZoomSchritt : -ZoomSchritt);
            neuerFaktor = Math.Max(MindestZoom, Math.Min(MaximalZoom, neuerFaktor));

            BildZoomTransform.ScaleX = neuerFaktor;
            BildZoomTransform.ScaleY = neuerFaktor;

            zuletzterZoomFaktor = neuerFaktor;

            e.Handled = true;
        }
    }
}