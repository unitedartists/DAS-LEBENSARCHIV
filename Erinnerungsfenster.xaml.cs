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

    public partial class ErinnerungenFenster : Window
    {
        private readonly List<ErinnerungsInfo> erinnerungen;
        private readonly List<ErinnerungsInfo> gueltigeErinnerungen = new List<ErinnerungsInfo>();
        private int aktuellerIndex = -1;

        private int ersterTrefferIndex = -1;

        private string sortierKriterium = "Datum";
        private bool sortierAufsteigend = true;

        private static string zuletztGeoeffneterPfad = null;

        private static double zuletzterZoomFaktor = 1.0;

        private const double MindestZoom = 0.3;
        private const double MaximalZoom = 4.0;
        private const double ZoomSchritt = 0.1;

        private readonly Func<string, List<VisuellesMerkmal>> liesMerkmale;
        private readonly Action<string, List<VisuellesMerkmal>> speichereMerkmale;

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

            BildZoomTransform.ScaleX = zuletzterZoomFaktor;
            BildZoomTransform.ScaleY = zuletzterZoomFaktor;

            ZeigeUebersicht();

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

            SortierungComboBox.SelectedIndex = 0;
        }

        private void ZeigeUebersicht()
        {
            gueltigeErinnerungen.Clear();

            foreach (ErinnerungsInfo info in erinnerungen)
            {
                if (info == null || string.IsNullOrEmpty(info.Pfad) || !File.Exists(info.Pfad))
                {
                    continue;
                }

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

            SortiereGueltigeErinnerungen();

            RenderMiniaturen(SucheTextBox.Text);

            UebersichtScrollViewer.Visibility = Visibility.Visible;
            GrossansichtGrid.Visibility = Visibility.Collapsed;
        }

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

        private void RenderMiniaturen(string suchtext)
        {
            MiniaturenPanel.Children.Clear();

            string suchtextKlein = (suchtext ?? "").Trim().ToLowerInvariant();
            bool sucheAktiv = suchtextKlein != "";
            int treffer = 0;

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
                    continue;
                }

                int diesIndex = i;

                rahmen.MouseLeftButtonUp += (sender, e) => ZeigeGross(diesIndex);

                rahmen.MouseLeftButtonDown += (sender, e) =>
                {
                    if (e.ClickCount == 2)
                    {
                        ZeigeGross(diesIndex);
                    }
                };

                MiniaturenPanel.Children.Add(rahmen);
            }

            TrefferAnzahlText.Text = "Gefunden: " + treffer + " von " + gueltigeErinnerungen.Count + " Erinnerungen";
        }

        private void SucheTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RenderMiniaturen(SucheTextBox.Text);
        }

        private void SucheTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && ersterTrefferIndex >= 0)
            {
                ZeigeGross(ersterTrefferIndex);
            }
        }

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

            if (!LadeGrossesBildVonDatei(info.Pfad))
            {
                return;
            }

            aktuellerIndex = index;

            zuletztGeoeffneterPfad = info.Pfad;

            bool hatTitel = !string.IsNullOrWhiteSpace(info.Titel);
            GrossansichtTitelText.Text = hatTitel ? info.Titel : "";
            GrossansichtTitelText.Visibility = hatTitel ? Visibility.Visible : Visibility.Collapsed;

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

            bool hatPerson = !string.IsNullOrWhiteSpace(info.PersonName);
            GrossansichtPersonText.Text = hatPerson ? "👤 " + info.PersonName : "";
            GrossansichtPersonText.Visibility = hatPerson ? Visibility.Visible : Visibility.Collapsed;

            bool hatBeschreibung = !string.IsNullOrWhiteSpace(info.Beschreibung);
            GrossansichtBeschreibungText.Text = hatBeschreibung ? "📝 " + info.Beschreibung : "";
            GrossansichtBeschreibungText.Visibility = hatBeschreibung ? Visibility.Visible : Visibility.Collapsed;

            bool hatBeteiligte = info.Beteiligte != null && info.Beteiligte.Count > 0;
            GrossansichtBeteiligteText.Text = hatBeteiligte ? "👥 Auch dabei: " + string.Join(", ", info.Beteiligte) : "";
            GrossansichtBeteiligteText.Visibility = hatBeteiligte ? Visibility.Visible : Visibility.Collapsed;

            RenderMerkmale();

            VorherigeButton.IsEnabled = aktuellerIndex > 0;
            NaechsteButton.IsEnabled = aktuellerIndex < gueltigeErinnerungen.Count - 1;

            PositionsanzeigeText.Text = "Erinnerung " + (aktuellerIndex + 1) + " von " + gueltigeErinnerungen.Count;

            BekanntesWissenPanel.Visibility = Visibility.Collapsed;
            BekanntesWissenPanel.Children.Clear();

            UebersichtScrollViewer.Visibility = Visibility.Collapsed;
            GrossansichtGrid.Visibility = Visibility.Visible;
        }

        // Neue Funktion (Generaltest 2, Wunsch von Oma+Opa): lädt das
        // Großansicht-Bild über einen Speicherpuffer statt direkt über den
        // Dateipfad - das umgeht zuverlässig jeden Bild-Zwischenspeicher
        // von Windows/WPF. Ohne diesen Umweg zeigte sich bei manchen
        // Dateien nach dem Drehen weiterhin die alte, unveränderte Version.
        private bool LadeGrossesBildVonDatei(string pfad)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(pfad);

                BitmapImage bild = new BitmapImage();

                using (MemoryStream stream = new MemoryStream(bytes))
                {
                    bild.BeginInit();
                    bild.CacheOption = BitmapCacheOption.OnLoad;
                    bild.StreamSource = stream;
                    bild.EndInit();
                }

                bild.Freeze();

                GrossesBild.Source = bild;
                return true;
            }
            catch
            {
                return false;
            }
        }

        // ============================================================
        // NEUE FUNKTION (Generaltest 2, Wunsch von Oma+Opa): FOTO DREHEN
        // ============================================================
        // Dreht die Bilddatei tatsächlich dauerhaft (nicht nur die
        // Anzeige) - damit das Bild auch überall sonst (Arbeitsmappe,
        // später Lebensbuch) richtig herum erscheint. Verwendet
        // ausschließlich verlustfreie Drehung über RotateTransform beim
        // erneuten Speichern des Bildes.
        private void DreheAktuellesBild(int gradImUhrzeigersinn)
        {
            if (aktuellerIndex < 0 || aktuellerIndex >= gueltigeErinnerungen.Count)
            {
                return;
            }

            string pfad = gueltigeErinnerungen[aktuellerIndex].Pfad;

            if (!Bilddrehung.DreheUndSpeichere(pfad, gradImUhrzeigersinn))
            {
                James.Problem("Dieses Bild konnte leider nicht gedreht werden (möglicherweise ein nicht unterstütztes Dateiformat).");
                return;
            }

            LadeGrossesBildVonDatei(pfad);
        }

        private void BildLinksDrehen_Click(object sender, RoutedEventArgs e)
        {
            DreheAktuellesBild(-90);
        }

        private void BildRechtsDrehen_Click(object sender, RoutedEventArgs e)
        {
            DreheAktuellesBild(90);
        }

        private void Vorherige_Click(object sender, RoutedEventArgs e)
        {
            if (aktuellerIndex > 0)
            {
                ZeigeGross(aktuellerIndex - 1);
            }
        }

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
