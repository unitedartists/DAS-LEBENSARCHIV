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

            // A/Opa-REPARATURAUFTRAG (13.08.), Punkt 1: Der Zuordnungs-
            // Papierkorb wird beim Entfernen einer Erinnerung bereits korrekt
            // beschrieben und gespeichert (EntferneZuordnungenInPapierkorb) -
            // nur die Anzeige im Original-James fehlte, weil dieser Tab-
            // Wechsel bisher nicht behandelt wurde. Wiederverwendet die
            // bereits bestehende, getestete Methode - keine neue Papierkorb-
            // Logik. Ueber den Tab-Header erkannt statt ueber eine Index-
            // Konstante, da hier keine eigene Konstante fuer den Papierkorb-
            // Tab existiert.
            if ((HauptTabControl.SelectedItem as TabItem)?.Header?.ToString() == "Papierkorb")
            {
                AktualisiereZuordnungsPapierkorbAnzeige();
                AktualisiereGemeinsamePapierkorbUebersicht();
                return;
            }

            if (HauptTabControl.SelectedIndex == 0)
            {
                // A/Opa-REPARATURAUFTRAG (13.08.), Punkt 2: Dieses Ereignis
                // feuert ausschliesslich bei einem ECHTEN Tab-Wechsel - die
                // interne Navigation innerhalb des Schreibtisch-Tabs (die
                // Start-Buttons, ZurStartseite_Click) loest kein
                // SelectionChanged des TabControl aus und bleibt davon
                // vollstaendig unberuehrt. Landet man hier, wurde also
                // bewusst von einem ANDEREN Tab zurueck zum Schreibtisch
                // gewechselt - genau dann soll zuverlaessig die Startseite
                // erscheinen. Vorher blieb z.B. PersonenListeBereich
                // faelschlich weiterhin sichtbar, weil kein Unterbereich
                // seine Sichtbarkeit beim Verlassen des Tabs zuruecksetzte -
                // "keinBereichSichtbar" war dann faelschlich false. Betrifft
                // nur genau dieselben Bereiche, die vorher schon geprueft
                // wurden - keine zusaetzlichen, unbeteiligten Bereiche.
                // A/Opa-OPTIMIERUNGSAUFTRAG "Opa-freundliches James" (16.08.),
                // Teil B: PersonenFormularBereich fehlte hier - wird laut
                // MainWindow.Personen.cs (z.B. Wiederherstellen_Click,
                // HoleAusArchivZurueckAufSchreibtisch) IMMER gemeinsam mit
                // PersonenListeBereich sichtbar geschaltet. Blieb er stehen,
                // ueberlagerte das Personenformular weiterhin die Startseite -
                // genau das vom Nutzer beobachtete "ueberlagert"-Symptom.
                StartseiteBereich.Visibility = Visibility.Visible;
                PersonenFormularBereich.Visibility = Visibility.Collapsed;
                PersonenListeBereich.Visibility = Visibility.Collapsed;
                EreignisBereich.Visibility = Visibility.Collapsed;
                EreignismappeBereich.Visibility = Visibility.Collapsed;
                SammlungBereich.Visibility = Visibility.Collapsed;

                ZeigeStartseiteVorschlag();
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

        // A/Opa-BAUAUFTRAG "AM: GESAMTUMBAU ZU EINEM ARBEITSBEREICH" (16.08.):
        // OeffneArbeitsmappe baute bisher zusaetzlich die alte, komplett
        // getrennte Kachel-/Filter-/Paging-Anzeige (arbeitsmappeAlleDateien,
        // arbeitsmappeFilter, arbeitsmappeSeite, arbeitsmappeAusgewaehlt,
        // AktualisiereArbeitsmappenFilterButtons/AktualisiereArbeitsmappe)
        // auf - das ist jetzt die "zweite Such-/Ergebnisflaeche", die laut
        // Auftrag entfallen soll. James' einziger Arbeitsbereich ist jetzt
        // das gruene Fenster (AmDirekteAuswahlListe). arbeitsmappeAlleDateien
        // wird trotzdem weiterhin geladen - die (unveraenderte) automatische
        // Duplikat-Erkennung (PruefeUndZeigeDuplikateInArbeitsmappe) prueft
        // rein lesend den physischen Datei-Bestand, unabhaengig vom
        // Erinnerungsmodell, und ist nicht Teil dieses Auftrags.
        private void OeffneArbeitsmappe()
        {
            arbeitsmappeOeffnenZaehler++;
            ArbeitsmappeDebugText.Text = "🐞 OeffneArbeitsmappe() ausgeführt - Aufruf Nr. " + arbeitsmappeOeffnenZaehler + " um " + DateTime.Now.ToString("HH:mm:ss.fff");

            arbeitsmappeAlleDateien = LadeErinnerungsverzeichnisDateien();
            arbeitsmappeNeuesEreignisPerson = null;
            arbeitsmappeLetztesEreignisPerson = null;
            arbeitsmappeLetztesEreignis = null;
            ArbeitsmappeEreignisOeffnenButton.Visibility = Visibility.Collapsed;

            VersteckeAlleArbeitsmappenPanels();
            AktualisiereAmDirekteAuswahlListe();
            AktualisiereArbeitsmappenWerkzeuge();
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

        // A/Opa-BAUAUFTRAG "AM: GESAMTUMBAU ZU EINEM ARBEITSBEREICH" (16.08.):
        // treibt jetzt die komplette rechte Aktionsleiste anhand der EINEN
        // gemeinsamen Markierung (amMarkierteErinnerungIds, MainWindow.
        // ErinnerungsmodellZustand.cs) statt der alten, kachelspezifischen
        // arbeitsmappeAusgewaehlt-Auswahl. Wird von AktualisiereAmMarkierungs-
        // AbhaengigeAnzeige nach jeder Markierungsaenderung aufgerufen.
        private void AktualisiereArbeitsmappenWerkzeuge()
        {
            int anzahl = amMarkierteErinnerungIds.Count;

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

            // Sprint C, Etappe 1b-Baukasten (05.08.): "James merkt sich" -
            // übergangsweise Möglichkeit, direkt aus der Arbeitsmappe
            // heraus Stichwörter für markierte Bilder zu bestätigen, bis
            // James genug trainiert ist. Nutzt dieselbe Logik wie der
            // Werkzeuge-Button (siehe MainWindow.Sehzentrum.cs).
            // Sprint C, Etappe 1b-Optimierung (06.08.): "James erkennt..."
            // (vormals "James merkt sich..."), Text wechselt je nach
            // Anzahl markierter Bilder.
            ArbeitsmappeJamesErkenntButton.IsEnabled = anzahl > 0;
            ArbeitsmappeJamesErkenntButton.Content = new TextBlock
            {
                Text = anzahl == 1
                    ? "James erkennt auf diesem Bild ..."
                    : "James erkennt auf diesen Bildern ...",
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center
            };

            if (anzahl == 0)
            {
                VersteckeAlleArbeitsmappenPanels();
            }
        }

        // A/Opa-BAUAUFTRAG "AM: GESAMTUMBAU ZU EINEM ARBEITSBEREICH" (16.08.):
        // "Markierung aufheben" ist jetzt der EINZIGE Button, der die
        // Markierung loescht (ersetzt die entfernten, redundanten Buttons
        // "Ausgewählte aus der Arbeitsauswahl entfernen"/"Arbeitsauswahl
        // leeren" aus dem ehemals getrennten gruenen Bereich).
        private void ArbeitsmappeMarkierungAufheben_Click(object sender, RoutedEventArgs e)
        {
            amMarkierteErinnerungIds.Clear();
            AktualisiereAmDirekteAuswahlListe();
        }

        // A/Opa-BAUAUFTRAG "AM: GESAMTUMBAU" (16.08.): arbeitsmappeAusgewaehlt
        // (altes Kachel-System) wird nicht mehr befuellt - diese Methode ist
        // damit unbenutzt, wird aber NICHT geloescht, da nicht sicher
        // ausgeschlossen werden kann, dass sie aus einer anderen, hier nicht
        // vorliegenden Datei aufgerufen wird (z.B. Werkzeuge/Sehzentrum).
        private GefundeneDatei ArbeitsmappeEinzigAusgewaehlteDatei()
        {
            string pfad = arbeitsmappeAusgewaehlt.FirstOrDefault();

            if (pfad == null)
            {
                return null;
            }

            return arbeitsmappeAlleDateien.FirstOrDefault(d => d.VollstaendigerPfad == pfad);
        }

        // Optimierungsrunde (06.08.), A's wichtigster Punkt: "James merkt
        // sich..." wird zu "James erkennt auf diesem Bild/diesen
        // Bildern...", jetzt echte STAPELERKENNUNG statt einzeln
        // nacheinander abzufragen - James analysiert alle markierten
        // Bilder gemeinsam und zeigt eine Trefferstatistik je Stichwort
        // (z.B. "Traktor: 4 von 4"). Nutzt SehzentrumStapelErkennen in
        // MainWindow.Sehzentrum.cs (dieselbe Logik wie der
        // Werkzeuge-Button "Kategorie testen...", nur dort mit genau
        // einem Bild).
        //
        // A/Opa-BAUAUFTRAG "AM: GESAMTUMBAU" (16.08.): liest die markierten
        // Bilder jetzt aus dem neuen Modell (amMarkierteErinnerungIds ->
        // erinnerungsmodellErinnerungen) statt aus dem alten arbeitsmappe-
        // Ausgewaehlt/arbeitsmappeAlleDateien-Bestand, der seit dem Umbau
        // nicht mehr befuellt wird.
        private void ArbeitsmappeJamesErkennt_Click(object sender, RoutedEventArgs e)
        {
            LadeErinnerungsmodellFallsNoetig();

            List<string> bildPfade = amMarkierteErinnerungIds
                .Select(id => erinnerungsmodellErinnerungen.FirstOrDefault(er => er.Id == id))
                .Where(er => er != null && er.MedienTyp == MedienTyp.Bild
                    && er.Fundorte != null && er.Fundorte.Count > 0
                    && File.Exists(er.Fundorte[0].Pfad))
                .Select(er => er.Fundorte[0].Pfad)
                .ToList();

            if (bildPfade.Count == 0)
            {
                James.Hinweis("Das Sehzentrum kann bisher nur mit Bildern arbeiten - unter den markierten Erinnerungen ist keine Bilddatei dabei.");
                return;
            }

            SehzentrumStapelErkennen(bildPfade);
        }

    }
}
