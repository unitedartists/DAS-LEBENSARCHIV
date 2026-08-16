using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow : Window
    {
        // ============================================================
        // NEUE FUNKTION (Generaltest 2, Wunsch von Oma+Opa): ASSERVATENKAMMER
        // ============================================================
        // James verschiebt hierhin automatisch erkannte, exakte Duplikate -
        // niemals löschen, immer nur verschieben. Der Benutzer entscheidet
        // hier in Ruhe, ob eine Datei endgültig gelöscht oder auf den
        // Schreibtisch zurückgeholt wird. Bewusst nur exakte (byte-für-byte
        // identische) Duplikate - die Erkennung "ähnlicher", nicht ganz
        // identischer Aufnahmen ist ein eigenes, späteres Vorhaben.

        // ============================================================
        // OPTIMIERUNGSRUNDE (06.08.), NACHBESSERUNG: SEITENWEISE KACHELN
        // ============================================================
        // Ersetzt die vorherige (fehleranfällige) einspaltige Liste bzw.
        // das Kachel-WrapPanel-Experiment, das die Anwendung bei vielen
        // Einträgen aufgehängt hat (siehe TÜV-Bericht 06.08., Priorität 2).
        // Nutzt jetzt dieselbe bewährte Technik wie die Arbeitsmappe: nur
        // eine kleine, feste Anzahl Kacheln (AsservatenkammerProSeite) wird
        // je Seite tatsächlich aufgebaut - dadurch ist ein Hängen bei
        // vielen tausend AK-Einträgen technisch ausgeschlossen, und das
        // Fenster wird trotzdem mit Zeilen UND Spalten voll ausgenutzt
        // (Wunsch des Nutzers).
        private const int AsservatenkammerProSeite = 24;

        private int asservatenkammerSeite = 1;

        // Auswahl über den Asservatenkammer-Pfad (eindeutiger Schlüssel,
        // wie arbeitsmappeAusgewaehlt in der Arbeitsmappe) statt über
        // ListBox.SelectedItems - dieselbe bewährte Mechanik, nur ohne
        // ListBox.
        private readonly HashSet<string> asservatenkammerAusgewaehlt = new HashSet<string>();

        private void AktualisiereAsservatenkammerAnzeige()
        {
            int gesamtSeiten = Math.Max(1, (int)Math.Ceiling(asservatenkammer.Count / (double)AsservatenkammerProSeite));

            if (asservatenkammerSeite > gesamtSeiten)
            {
                asservatenkammerSeite = gesamtSeiten;
            }

            if (asservatenkammerSeite < 1)
            {
                asservatenkammerSeite = 1;
            }

            List<AsservatenEintrag> seite = asservatenkammer
                .Skip((asservatenkammerSeite - 1) * AsservatenkammerProSeite)
                .Take(AsservatenkammerProSeite)
                .ToList();

            AsservatenkammerKachelnPanel.Children.Clear();

            foreach (AsservatenEintrag eintrag in seite)
            {
                AsservatenkammerKachelnPanel.Children.Add(ErstelleAsservatenkammerKachel(eintrag));
            }

            AsservatenkammerSeiteText.Text = asservatenkammer.Count == 0
                ? "Die Asservatenkammer ist leer."
                : "Seite " + asservatenkammerSeite + " von " + gesamtSeiten + " (" + asservatenkammer.Count + " insgesamt)";

            AsservatenkammerVorherigeSeiteButton.IsEnabled = asservatenkammerSeite > 1;

            AktualisiereAsservatenkammerWerkzeuge();
        }

        private void AsservatenkammerVorherigeSeite_Click(object sender, RoutedEventArgs e)
        {
            asservatenkammerSeite--;
            AktualisiereAsservatenkammerAnzeige();
        }

        private static TextBlock ErstelleAsservatenkammerSymbol(string symbol)
        {
            return new TextBlock
            {
                Text = symbol,
                FontSize = 30,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static readonly string[] AsservatenkammerBilddateiendungen = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };

        private Border ErstelleAsservatenkammerKachel(AsservatenEintrag eintrag)
        {
            Border rahmen = new Border
            {
                Width = 190,
                Height = 210,
                Margin = new Thickness(6),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Background = Brushes.White,
                CornerRadius = new CornerRadius(4)
            };

            StackPanel inhalt = new StackPanel { Margin = new Thickness(8) };

            CheckBox auswahlBox = new CheckBox
            {
                IsChecked = asservatenkammerAusgewaehlt.Contains(eintrag.AsservatenPfad),
                Margin = new Thickness(0, 0, 0, 6)
            };

            auswahlBox.Checked += (sender, e) =>
            {
                asservatenkammerAusgewaehlt.Add(eintrag.AsservatenPfad);
                AktualisiereAsservatenkammerWerkzeuge();
            };

            auswahlBox.Unchecked += (sender, e) =>
            {
                asservatenkammerAusgewaehlt.Remove(eintrag.AsservatenPfad);
                AktualisiereAsservatenkammerWerkzeuge();
            };

            inhalt.Children.Add(auswahlBox);

            Border bildRahmen = new Border
            {
                Width = 170,
                Height = 120,
                Background = Brushes.WhiteSmoke,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            bool dateiVorhanden = File.Exists(eintrag.AsservatenPfad);
            string endung = dateiVorhanden ? Path.GetExtension(eintrag.AsservatenPfad).ToLowerInvariant() : "";
            bool istBild = dateiVorhanden && AsservatenkammerBilddateiendungen.Contains(endung);

            if (istBild)
            {
                try
                {
                    BitmapImage bild = new BitmapImage();
                    bild.BeginInit();
                    bild.CacheOption = BitmapCacheOption.OnLoad;
                    bild.DecodePixelWidth = 170;
                    bild.UriSource = new Uri(eintrag.AsservatenPfad);
                    bild.EndInit();

                    bildRahmen.Child = new Image
                    {
                        Source = bild,
                        Stretch = Stretch.Uniform
                    };
                }
                catch
                {
                    bildRahmen.Child = ErstelleAsservatenkammerSymbol("🖼️");
                }
            }
            else
            {
                bildRahmen.Child = ErstelleAsservatenkammerSymbol(dateiVorhanden ? "📦" : "❓");
            }

            inhalt.Children.Add(bildRahmen);

            TextBlock nameText = new TextBlock
            {
                Text = eintrag.Dateiname,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxHeight = 34,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 4, 0, 0)
            };

            inhalt.Children.Add(nameText);

            TextBlock grundText = new TextBlock
            {
                Text = eintrag.Grund,
                FontSize = 10,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 2, 0, 0)
            };

            inhalt.Children.Add(grundText);

            rahmen.Child = inhalt;

            return rahmen;
        }

        private void AktualisiereAsservatenkammerWerkzeuge()
        {
            int anzahl = asservatenkammerAusgewaehlt.Count;

            // "Ansehen" ergibt nur bei genau einer ausgewählten Datei Sinn.
            AsservatenkammerAnsehenButton.IsEnabled = anzahl == 1;
            AsservatenkammerZurueckholenButton.IsEnabled = anzahl > 0;
            AsservatenkammerEndgueltigLoeschenButton.IsEnabled = anzahl > 0;
        }

        private void AsservatenkammerAnsehen_Click(object sender, RoutedEventArgs e)
        {
            if (asservatenkammerAusgewaehlt.Count != 1)
            {
                return;
            }

            string pfad = asservatenkammerAusgewaehlt.First();

            if (!File.Exists(pfad))
            {
                return;
            }

            try
            {
                ProcessStartInfo start = new ProcessStartInfo
                {
                    FileName = pfad,
                    UseShellExecute = true
                };

                Process.Start(start);
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimOeffnenDerErinnerung(ex.Message));
            }
        }

        private void AsservatenkammerZurueckholen_Click(object sender, RoutedEventArgs e)
        {
            List<AsservatenEintrag> ausgewaehlte = asservatenkammer
                .Where(x => asservatenkammerAusgewaehlt.Contains(x.AsservatenPfad))
                .ToList();

            if (ausgewaehlte.Count == 0)
            {
                return;
            }

            int zurueckgeholt = 0;

            foreach (AsservatenEintrag eintrag in ausgewaehlte)
            {
                if (!File.Exists(eintrag.AsservatenPfad))
                {
                    continue;
                }

                try
                {
                    string zielOrdner = Path.GetDirectoryName(eintrag.UrspruenglicherPfad);

                    if (!string.IsNullOrEmpty(zielOrdner) && !Directory.Exists(zielOrdner))
                    {
                        Directory.CreateDirectory(zielOrdner);
                    }

                    string zielPfad = eintrag.UrspruenglicherPfad;

                    if (File.Exists(zielPfad))
                    {
                        zielPfad = Path.Combine(zielOrdner ?? AsservatenkammerOrdnerPfad, Guid.NewGuid().ToString() + Path.GetExtension(eintrag.Dateiname));
                    }

                    File.Move(eintrag.AsservatenPfad, zielPfad);

                    FuegeZuErinnerungsverzeichnisHinzu(new GefundeneDatei
                    {
                        Dateiname = Path.GetFileName(zielPfad),
                        VollstaendigerPfad = zielPfad,
                        Dateityp = eintrag.Dateityp,
                        Hashwert = eintrag.Hashwert,
                        Geaendert = DateTime.Now,
                        GroesseInBytes = new FileInfo(zielPfad).Length
                    });

                    asservatenkammer.Remove(eintrag);
                    asservatenkammerAusgewaehlt.Remove(eintrag.AsservatenPfad);
                    zurueckgeholt++;
                }
                catch
                {
                }
            }

            if (zurueckgeholt > 0)
            {
                SpeichereDaten();
                AktualisiereAsservatenkammerAnzeige();
                ZeigeStatusMeldung(zurueckgeholt + " Datei(en) auf den Schreibtisch zurückgeholt.");
            }
        }

        private void AsservatenkammerEndgueltigLoeschen_Click(object sender, RoutedEventArgs e)
        {
            List<AsservatenEintrag> ausgewaehlte = asservatenkammer
                .Where(x => asservatenkammerAusgewaehlt.Contains(x.AsservatenPfad))
                .ToList();

            if (ausgewaehlte.Count == 0)
            {
                return;
            }

            string frage = ausgewaehlte.Count == 1
                ? James.FrageEndgueltigLoeschenEinzeln(ausgewaehlte[0].Dateiname)
                : James.FrageEndgueltigLoeschenMehrere(ausgewaehlte.Count);

            bool ergebnis = James.FrageJaNein(frage, James.TitelEndgueltigeEntscheidung, MessageBoxImage.Warning);

            if (!ergebnis)
            {
                return;
            }

            int geloescht = 0;

            foreach (AsservatenEintrag eintrag in ausgewaehlte)
            {
                try
                {
                    if (File.Exists(eintrag.AsservatenPfad))
                    {
                        File.Delete(eintrag.AsservatenPfad);
                    }

                    asservatenkammer.Remove(eintrag);
                    asservatenkammerAusgewaehlt.Remove(eintrag.AsservatenPfad);
                    geloescht++;
                }
                catch
                {
                }
            }

            if (geloescht > 0)
            {
                SpeichereDaten();
                AktualisiereAsservatenkammerAnzeige();
            }
        }

        // Fügt eine Datei wieder ins gespeicherte Erinnerungsverzeichnis ein
        // (z.B. nach dem Zurückholen aus der Asservatenkammer), damit sie in
        // der Arbeitsmappe wieder auftaucht.
        private void FuegeZuErinnerungsverzeichnisHinzu(GefundeneDatei datei)
        {
            ErinnerungsVerzeichnis verzeichnis;

            if (File.Exists(ErinnerungsVerzeichnisPfad))
            {
                try
                {
                    string json = File.ReadAllText(ErinnerungsVerzeichnisPfad);
                    verzeichnis = JsonSerializer.Deserialize<ErinnerungsVerzeichnis>(json)
                        ?? new ErinnerungsVerzeichnis { Dateien = new List<GefundeneDatei>() };
                }
                catch
                {
                    verzeichnis = new ErinnerungsVerzeichnis { Dateien = new List<GefundeneDatei>() };
                }
            }
            else
            {
                verzeichnis = new ErinnerungsVerzeichnis { Dateien = new List<GefundeneDatei>() };
            }

            if (verzeichnis.Dateien == null)
            {
                verzeichnis.Dateien = new List<GefundeneDatei>();
            }

            verzeichnis.Dateien.Add(datei);
            verzeichnis.ErstelltAm = DateTime.Now;

            try
            {
                JsonSerializerOptions optionen = new JsonSerializerOptions { WriteIndented = true };
                string neuerJson = JsonSerializer.Serialize(verzeichnis, optionen);
                File.WriteAllText(ErinnerungsVerzeichnisPfad, neuerJson);
            }
            catch
            {
            }
        }

        // Entfernt eine Datei aus dem gespeicherten Erinnerungsverzeichnis
        // (z.B. wenn sie in die Asservatenkammer verschoben wurde), damit
        // sie in der Arbeitsmappe nicht mehr auftaucht.
        private void EntferneAusErinnerungsverzeichnis(string vollstaendigerPfad)
        {
            EntferneMehrereAusErinnerungsverzeichnis(new HashSet<string> { vollstaendigerPfad });
        }

        // Neue Funktion (Generaltest 2, Wunsch von Oma+Opa): entfernt viele
        // Dateien auf einmal aus dem Erinnerungsverzeichnis - eine einzige
        // Lese-/Schreiboperation statt einer pro Datei. Bei sehr großen
        // Erinnerungsverzeichnissen (100.000+ Einträge) wäre eine
        // Einzelbehandlung pro Datei viel zu langsam gewesen.
        private void EntferneMehrereAusErinnerungsverzeichnis(HashSet<string> pfade)
        {
            if (pfade == null || pfade.Count == 0 || !File.Exists(ErinnerungsVerzeichnisPfad))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(ErinnerungsVerzeichnisPfad);
                ErinnerungsVerzeichnis verzeichnis = JsonSerializer.Deserialize<ErinnerungsVerzeichnis>(json);

                if (verzeichnis == null || verzeichnis.Dateien == null)
                {
                    return;
                }

                verzeichnis.Dateien.RemoveAll(d => pfade.Contains(d.VollstaendigerPfad));

                JsonSerializerOptions optionen = new JsonSerializerOptions { WriteIndented = true };
                string neuerJson = JsonSerializer.Serialize(verzeichnis, optionen);
                File.WriteAllText(ErinnerungsVerzeichnisPfad, neuerJson);
            }
            catch
            {
            }
        }

        // Verschiebt eine einzelne gefundene Datei physisch in die
        // Asservatenkammer und merkt sich den Vorgang - niemals löschen,
        // immer nur beiseitelegen. Kümmert sich bewusst NICHT mehr selbst
        // um das Erinnerungsverzeichnis (siehe EntferneMehrereAusErinnerungsverzeichnis) -
        // bei vielen Dateien auf einmal ruft der Aufrufer diese Bereinigung
        // einmal gebündelt auf, statt einmal pro Datei.
        private bool VerschiebeInAsservatenkammer(GefundeneDatei datei, string grund)
        {
            try
            {
                Directory.CreateDirectory(AsservatenkammerOrdnerPfad);

                string zielDateiname = Guid.NewGuid().ToString() + Path.GetExtension(datei.VollstaendigerPfad);
                string zielPfad = Path.Combine(AsservatenkammerOrdnerPfad, zielDateiname);

                File.Move(datei.VollstaendigerPfad, zielPfad);

                asservatenkammer.Add(new AsservatenEintrag
                {
                    Dateiname = datei.Dateiname,
                    UrspruenglicherPfad = datei.VollstaendigerPfad,
                    AsservatenPfad = zielPfad,
                    Dateityp = datei.Dateityp,
                    Hashwert = datei.Hashwert,
                    Grund = grund
                });

                return true;
            }
            catch
            {
                return false;
            }
        }

        // Neue Funktion (Generaltest 2, Wunsch von Oma+Opa): prüft beim
        // Öffnen der Arbeitsmappe automatisch, ob (nahezu) identische
        // Dateien darunter sind, und bietet an, sie in einem Arbeitsgang
        // in die Asservatenkammer zu verschieben - der Benutzer entscheidet,
        // James verschiebt nichts von sich aus.
        private void PruefeUndZeigeDuplikateInArbeitsmappe()
        {
            Dictionary<string, List<GefundeneDatei>> gruppen = ErmittleDuplikatGruppen();

            int anzahlUeberzaehlig = gruppen.Values.Sum(liste => liste.Count - 1);

            if (anzahlUeberzaehlig > 0)
            {
                ArbeitsmappeDuplikateHinweisText.Text = "James hat " + anzahlUeberzaehlig + " (nahezu) identische Datei(en) gefunden.";
                ArbeitsmappeDuplikateVerschiebenButton.Visibility = Visibility.Visible;
            }
            else
            {
                ArbeitsmappeDuplikateHinweisText.Text = "";
                ArbeitsmappeDuplikateVerschiebenButton.Visibility = Visibility.Collapsed;
            }
        }

        private Dictionary<string, List<GefundeneDatei>> ErmittleDuplikatGruppen()
        {
            // Wichtig: KEIN File.Exists() an dieser Stelle - bei sehr großen
            // Erinnerungsverzeichnissen (100.000+ Dateien) würde das die
            // Oberfläche spürbar blockieren, da für jede einzelne Datei ein
            // Festplattenzugriff nötig wäre. Die Gruppierung arbeitet rein
            // im Arbeitsspeicher anhand des bereits beim Rundgang berechneten
            // Hashwerts. Ob eine Datei tatsächlich noch existiert, wird erst
            // beim tatsächlichen Verschieben der (wenigen) betroffenen
            // Dateien geprüft - siehe VerschiebeInAsservatenkammer.
            return arbeitsmappeAlleDateien
                .Where(d => !string.IsNullOrEmpty(d.Hashwert))
                .GroupBy(d => d.Hashwert)
                .Where(g => g.Count() > 1)
                .ToDictionary(g => g.Key, g => g.ToList());
        }

        private async void ArbeitsmappeDuplikateVerschieben_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, List<GefundeneDatei>> gruppen = ErmittleDuplikatGruppen();

            List<GefundeneDatei> zuVerschieben = new List<GefundeneDatei>();

            foreach (List<GefundeneDatei> gruppe in gruppen.Values)
            {
                // Die erste Datei jeder Gruppe bleibt auf dem Schreibtisch,
                // alle weiteren (exakt identischen) wandern in die
                // Asservatenkammer.
                for (int i = 1; i < gruppe.Count; i++)
                {
                    zuVerschieben.Add(gruppe[i]);
                }
            }

            if (zuVerschieben.Count == 0)
            {
                return;
            }

            // Neue Funktion (Generaltest 2, Wunsch von Oma+Opa): läuft jetzt
            // im Hintergrund mit Fortschrittsanzeige, statt die Oberfläche
            // bei sehr vielen Duplikaten (zehntausende Dateien) einzufrieren.
            ArbeitsmappeDuplikateVerschiebenButton.IsEnabled = false;
            ArbeitsmappeDuplikateFortschrittsleiste.Visibility = Visibility.Visible;
            ArbeitsmappeDuplikateFortschrittsleiste.Value = 0;

            IProgress<int> fortschritt = new Progress<int>(anzahl =>
            {
                int prozent = (int)(100.0 * anzahl / zuVerschieben.Count);
                ArbeitsmappeDuplikateFortschrittsleiste.Value = prozent;
                ArbeitsmappeDuplikateHinweisText.Text = "James räumt auf: " + prozent + " % erledigt (" + anzahl + " von " + zuVerschieben.Count + " Duplikaten verschoben) ...";
            });

            HashSet<string> entferntePfade = new HashSet<string>();
            int verschoben = 0;

            await Task.Run(() =>
            {
                for (int i = 0; i < zuVerschieben.Count; i++)
                {
                    if (VerschiebeInAsservatenkammer(zuVerschieben[i], "Duplikat"))
                    {
                        entferntePfade.Add(zuVerschieben[i].VollstaendigerPfad);
                        verschoben++;
                    }

                    // Optimierung (04.08.): deutlich häufiger aktualisieren
                    // (statt nur alle 200) - so ist jederzeit erkennbar,
                    // dass James noch aktiv arbeitet, nicht "eingefroren" ist.
                    if (verschoben % 10 == 0 || verschoben == zuVerschieben.Count)
                    {
                        fortschritt.Report(verschoben);
                    }
                }

                // Erinnerungsverzeichnis in einem Rutsch bereinigen -
                // schneller als pro Datei einzeln lesen und schreiben.
                EntferneMehrereAusErinnerungsverzeichnis(entferntePfade);
            });

            if (verschoben > 0)
            {
                SpeichereDaten();
                arbeitsmappeAlleDateien = LadeErinnerungsverzeichnisDateien();
                AktualisiereAsservatenkammerAnzeige();
                // A/Opa-BAUAUFTRAG "AM: GESAMTUMBAU" (16.08.): AktualisiereArbeitsmappe
                // (altes Kachel-System) ist entfallen - das gruene Fenster wird
                // stattdessen aktualisiert, damit automatisch verschobene
                // Duplikate dort sofort korrekt erscheinen.
                AktualisiereAmDirekteAuswahlListe();

                int fehlgeschlagenBeimVerschieben = zuVerschieben.Count - verschoben;

                ArbeitsmappeStatusText.Text = verschoben + " doppelte Datei(en) wurden in die Asservatenkammer verschoben." +
                    (fehlgeschlagenBeimVerschieben > 0
                        ? " " + fehlgeschlagenBeimVerschieben + " weitere konnten NICHT verschoben werden - vermutlich sind die zugehörigen Originaldateien nicht mehr auf der Festplatte vorhanden."
                        : "");
            }
            else
            {
                // BUGFIX (05.08.): Bisher wurde hier - auch wenn KEINE
                // einzige Datei verschoben werden konnte (z.B. weil alle
                // gefundenen "Duplikate" in Wirklichkeit nicht mehr
                // existierende Dateien waren, etwa .cda-CD-Titel-Verweise) -
                // gar nichts gemeldet. Der Button verschwand einfach
                // lautlos, ohne dass der Benutzer erfuhr, was passiert ist.
                ArbeitsmappeStatusText.Text = "Keine der " + zuVerschieben.Count +
                    " gefundenen (nahezu) identischen Datei(en) konnte verschoben werden - vermutlich sind die zugehörigen Originaldateien nicht mehr auf der Festplatte vorhanden.";
            }

            ArbeitsmappeDuplikateFortschrittsleiste.Visibility = Visibility.Collapsed;
            ArbeitsmappeDuplikateVerschiebenButton.IsEnabled = true;

            // Zeigt den Button erneut/weiterhin an, falls nach diesem
            // Durchgang noch (unverschobene) Duplikat-Gruppen übrig sind -
            // vorher wurde er bedingungslos versteckt, auch wenn gar nichts
            // verschoben wurde.
            PruefeUndZeigeDuplikateInArbeitsmappe();
        }
        // Punkt 3 (Optimierung nach Test 2): eigenständiger Button - verschiebt
        // NUR die eigens dafür markierten Erinnerungen in die
        // Asservatenkammer, unabhängig von jeder Person-/Ereignis-/
        // Sammlung-Zuordnung. Läuft nach demselben bewährten Muster wie das
        // automatische Verschieben erkannter Duplikate.
        //
        // A/Opa-BAUAUFTRAG "AM: GESAMTUMBAU ZU EINEM ARBEITSBEREICH" (16.08.):
        // liest die Markierung jetzt aus der EINEN gemeinsamen amMarkierte-
        // ErinnerungIds (MainWindow.ErinnerungsmodellZustand.cs) statt aus
        // dem alten, seit dem Umbau nicht mehr befuellten arbeitsmappe-
        // Ausgewaehlt/arbeitsmappeAlleDateien-System, und baut daraus die
        // GefundeneDatei-Objekte, die VerschiebeInAsservatenkammer
        // (unveraendert) erwartet. WICHTIG (offener Punkt, siehe
        // Abschlussbericht): die zugehoerige Erinnerung wird NICHT aus
        // erinnerungsmodell.json entfernt (keine Datenbereinigung laut
        // Auftrag) - sie bleibt mit ihren bisherigen Zuordnungen bestehen,
        // zeigt ab sofort aber ueberall (Person/Ereignis/Sammlung/
        // Papierkorb/AM) den bereits bestehenden "Fundort nicht
        // gefunden"-Platzhalter, genau wie bei jeder anderen fehlenden Datei.
        private async void ArbeitsmappeMarkierteInAsservatenkammer_Click(object sender, RoutedEventArgs e)
        {
            LadeErinnerungsmodellFallsNoetig();

            List<Erinnerung> markierteErinnerungen = amMarkierteErinnerungIds
                .Select(id => erinnerungsmodellErinnerungen.FirstOrDefault(er => er.Id == id))
                .Where(er => er != null && er.Fundorte != null && er.Fundorte.Count > 0 && File.Exists(er.Fundorte[0].Pfad))
                .ToList();

            if (markierteErinnerungen.Count == 0)
            {
                James.Hinweis("Keine der markierten Erinnerungen hat eine (noch vorhandene) Datei, die verschoben werden könnte.");
                return;
            }

            List<GefundeneDatei> zuVerschieben = markierteErinnerungen
                .Select(er => new GefundeneDatei
                {
                    Dateiname = Path.GetFileName(er.Fundorte[0].Pfad),
                    VollstaendigerPfad = er.Fundorte[0].Pfad,
                    Dateityp = ErmittleAltenDateityp(er.MedienTyp),
                    Hashwert = er.Hashwert
                })
                .ToList();

            ArbeitsmappeMarkierteInAsservatenkammerButton.IsEnabled = false;

            IProgress<int> fortschritt = new Progress<int>(anzahl =>
            {
                int prozent = (int)(100.0 * anzahl / zuVerschieben.Count);
                ArbeitsmappeAsservatenkammerStatusText.Foreground = Brushes.Black;
                ArbeitsmappeAsservatenkammerStatusText.Text = "James verschiebt: " + prozent + " % erledigt (" + anzahl + " von " + zuVerschieben.Count + ") ...";
            });

            HashSet<string> entferntePfade = new HashSet<string>();
            int verschoben = 0;

            await Task.Run(() =>
            {
                for (int i = 0; i < zuVerschieben.Count; i++)
                {
                    if (VerschiebeInAsservatenkammer(zuVerschieben[i], "Vom Benutzer markiert"))
                    {
                        entferntePfade.Add(zuVerschieben[i].VollstaendigerPfad);
                        verschoben++;
                    }

                    if (verschoben % 10 == 0 || verschoben == zuVerschieben.Count)
                    {
                        fortschritt.Report(verschoben);
                    }
                }

                // Rein vorsorglich (das Erinnerungsverzeichnis ist ein
                // aelterer, separater Katalog) - entfernt die verschobenen
                // Pfade dort, falls sie zufaellig auch dort noch gefuehrt
                // werden. erinnerungsmodell.json selbst bleibt bewusst
                // unangetastet (keine Datenbereinigung laut Auftrag).
                EntferneMehrereAusErinnerungsverzeichnis(entferntePfade);
            });

            // A/Opa-BAUAUFTRAG "AM: GESAMTUMBAU" (16.08.): erfolgreich
            // verschobene Erinnerungen werden aus der Markierung entfernt -
            // anders als bei "Neue Zuordnung anlegen"/"Markierte in den
            // Papierkorb" (dort bleibt die Markierung fuer weitere Aktionen
            // im selben Arbeitsgang bewusst bestehen), ist eine asservierte
            // Erinnerung nicht mehr sinnvoll weiter bearbeitbar - ihre Datei
            // ist aus dem aktiven Bestand entfernt.
            foreach (Erinnerung erinnerung in markierteErinnerungen)
            {
                if (erinnerung.Fundorte.Count > 0 && entferntePfade.Contains(erinnerung.Fundorte[0].Pfad))
                {
                    amMarkierteErinnerungIds.Remove(erinnerung.Id);
                }
            }

            if (verschoben > 0)
            {
                SpeichereDaten();
                arbeitsmappeAlleDateien = LadeErinnerungsverzeichnisDateien();
                AktualisiereAsservatenkammerAnzeige();

                int fehlgeschlagenBeimVerschieben = zuVerschieben.Count - verschoben;

                ArbeitsmappeAsservatenkammerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                ArbeitsmappeAsservatenkammerStatusText.Text = (verschoben == 1
                    ? "1 markierte Erinnerung wurde in die Asservatenkammer verschoben."
                    : verschoben + " markierte Erinnerungen wurden in die Asservatenkammer verschoben.") +
                    (fehlgeschlagenBeimVerschieben > 0
                        ? " " + fehlgeschlagenBeimVerschieben + " weitere konnten NICHT verschoben werden - vermutlich sind die zugehörigen Originaldateien nicht mehr auf der Festplatte vorhanden."
                        : "");
            }
            else
            {
                ArbeitsmappeAsservatenkammerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0x00, 0x20));
                ArbeitsmappeAsservatenkammerStatusText.Text = "Keine der " + zuVerschieben.Count +
                    " markierten Datei(en) konnte verschoben werden - vermutlich sind die zugehörigen Originaldateien nicht mehr auf der Festplatte vorhanden.";
            }

            AktualisiereAmDirekteAuswahlListe();
            ArbeitsmappeMarkierteInAsservatenkammerButton.IsEnabled = amMarkierteErinnerungIds.Count > 0;
        }

        // A/Opa-BAUAUFTRAG "AM: GESAMTUMBAU" (16.08.): kleine Bruecke
        // zwischen dem neuen Erinnerung.MedienTyp-Enum und dem alten,
        // string-basierten GefundeneDatei.Dateityp, den VerschiebeInAsservaten-
        // kammer/ErstelleAsservatenkammerKachel weiterhin verwenden - keine
        // Aenderung an diesen bereits bestehenden, getesteten Methoden noetig.
        private static string ErmittleAltenDateityp(MedienTyp medienTyp)
        {
            switch (medienTyp)
            {
                case MedienTyp.Bild: return "Bilder";
                case MedienTyp.Video: return "Videos";
                case MedienTyp.Pdf: return "PDF";
                case MedienTyp.Dokument: return "Dokumente";
                default: return "Sonstige";
            }
        }
    }
}
