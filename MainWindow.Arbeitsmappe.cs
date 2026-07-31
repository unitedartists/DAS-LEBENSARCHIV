using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow : Window
    {
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
                return;
            }

            if (HauptTabControl.SelectedIndex == 0)
            {
                bool keinBereichSichtbar =
                    StartseiteBereich.Visibility != Visibility.Visible &&
                    PersonenListeBereich.Visibility != Visibility.Visible &&
                    EreignisBereich.Visibility != Visibility.Visible &&
                    EreignismappeBereich.Visibility != Visibility.Visible &&
                    SammlungBereich.Visibility != Visibility.Visible;

                if (keinBereichSichtbar)
                {
                    StartseiteBereich.Visibility = Visibility.Visible;
                    ZeigeStartseiteVorschlag();
                }
            }
        }

        private void ArbeitsmappeOeffnenButton_Click(object sender, RoutedEventArgs e)
        {
            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;
        }

        // Komfortfunktion: grüner Link "Zurück zur Startseite" in der Arbeitsmappe
        private void ArbeitsmappeZurueckZurStartseite_Click(object sender, MouseButtonEventArgs e)
        {
            HauptTabControl.SelectedIndex = 0;
            ZurStartseite_Click(sender, e);
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
            PruefeUndZeigeDuplikateInArbeitsmappe();
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

            // Punkt 3 (Optimierung nach Test 2): bereits zugeordnete
            // Erinnerungen verschwinden automatisch aus James' Vorlage -
            // hier bleiben nur noch nicht zugeordnete Erinnerungen übrig.
            ergebnis = ergebnis.Where(d => !arbeitsmappeBereitsZugeordnet.Contains(d.VollstaendigerPfad));

            // Punkt 3: chronologische Anzeige (nach Aufnahme-/Änderungsdatum).
            return ergebnis.OrderBy(d => d.Geaendert).ToList();
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

            // Punkt 2 (Optimierung nach Test 2): garantiert, dass die erste
            // Zeile sofort sichtbar ist, ohne dass erst manuell gescrollt
            // werden muss.
            ArbeitsmappeKachelnScrollViewer.ScrollToTop();

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

        // Komfortfunktion: direkt zu einer bestimmten Seite springen
        private void ArbeitsmappeSeiteGehen_Click(object sender, RoutedEventArgs e)
        {
            int gewuenschteSeite;

            if (int.TryParse(ArbeitsmappeSeiteZahlTextBox.Text.Trim(), out gewuenschteSeite))
            {
                arbeitsmappeSeite = gewuenschteSeite;
                AktualisiereArbeitsmappe();
            }
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
                Width = 210,
                Height = 260,
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
                Width = 190,
                Height = 135,
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
                    bild.DecodePixelWidth = 320;
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

            // Punkt 1 (Optimierung nach Test 2): bei Dokumenten, PDF, Video
            // und Audio reicht ein Icon allein nicht - der Benutzer muss
            // erkennen können, welche konkrete Erinnerung sich dahinter
            // verbirgt. Zeigt daher zusätzlich die gespeicherte Bezeichnung
            // (den Dateinamen) an. Bei Bildern nicht nötig, da man den
            // Inhalt bereits an der Vorschau erkennt.
            if (datei.Dateityp != "Bilder")
            {
                TextBlock dateinameText = new TextBlock
                {
                    Text = datei.Dateiname,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxHeight = 34,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.Black,
                    Margin = new Thickness(0, 4, 0, 0)
                };

                inhalt.Children.Add(dateinameText);
            }

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

            ArbeitsmappeNeueSammlungButton.IsEnabled = anzahl > 0;
            ArbeitsmappeSammlungZuordnenButton.IsEnabled = anzahl > 0;

            // Punkt 3 (Optimierung nach Test 2): eigenständiger Button,
            // unabhängig von Person/Ereignis/Sammlung.
            ArbeitsmappeMarkierteInAsservatenkammerButton.IsEnabled = anzahl > 0;

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

    }
}
