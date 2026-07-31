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

        private void AktualisiereAsservatenkammerAnzeige()
        {
            object ausgewaehlt = AsservatenkammerListe.SelectedItem;

            AsservatenkammerListe.ItemsSource = null;
            AsservatenkammerListe.ItemsSource = asservatenkammer;

            if (ausgewaehlt != null && asservatenkammer.Contains(ausgewaehlt))
            {
                AsservatenkammerListe.SelectedItem = ausgewaehlt;
            }
        }

        private void AsservatenkammerListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool istAusgewaehlt = AsservatenkammerListe.SelectedItem != null;

            AsservatenkammerAnsehenButton.IsEnabled = istAusgewaehlt;
            AsservatenkammerZurueckholenButton.IsEnabled = istAusgewaehlt;
            AsservatenkammerEndgueltigLoeschenButton.IsEnabled = istAusgewaehlt;
        }

        private void AsservatenkammerAnsehen_Click(object sender, RoutedEventArgs e)
        {
            AsservatenEintrag eintrag = AsservatenkammerListe.SelectedItem as AsservatenEintrag;

            if (eintrag == null || !File.Exists(eintrag.AsservatenPfad))
            {
                return;
            }

            try
            {
                ProcessStartInfo start = new ProcessStartInfo
                {
                    FileName = eintrag.AsservatenPfad,
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
            List<AsservatenEintrag> ausgewaehlte = AsservatenkammerListe.SelectedItems.Cast<AsservatenEintrag>().ToList();

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
            List<AsservatenEintrag> ausgewaehlte = AsservatenkammerListe.SelectedItems.Cast<AsservatenEintrag>().ToList();

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

            IProgress<int> fortschritt = new Progress<int>(anzahl =>
            {
                ArbeitsmappeDuplikateHinweisText.Text = "James verschiebt doppelte Dateien: " + anzahl + " von " + zuVerschieben.Count + " ...";
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

                    if (verschoben % 200 == 0)
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
                AktualisiereArbeitsmappe();
                ArbeitsmappeStatusText.Text = verschoben + " doppelte Datei(en) wurden in die Asservatenkammer verschoben.";
            }

            ArbeitsmappeDuplikateHinweisText.Text = "";
            ArbeitsmappeDuplikateVerschiebenButton.Visibility = Visibility.Collapsed;
            ArbeitsmappeDuplikateVerschiebenButton.IsEnabled = true;
        }
        // Punkt 3 (Optimierung nach Test 2): eigenständiger Button - verschiebt
        // NUR die eigens dafür markierten Erinnerungen (arbeitsmappeAusgewaehlt)
        // in die Asservatenkammer, unabhängig von jeder Person-/Ereignis-/
        // Sammlung-Zuordnung. Läuft nach demselben bewährten Muster wie das
        // automatische Verschieben erkannter Duplikate.
        private async void ArbeitsmappeMarkierteInAsservatenkammer_Click(object sender, RoutedEventArgs e)
        {
            List<GefundeneDatei> zuVerschieben = arbeitsmappeAlleDateien
                .Where(d => arbeitsmappeAusgewaehlt.Contains(d.VollstaendigerPfad))
                .ToList();

            if (zuVerschieben.Count == 0)
            {
                return;
            }

            ArbeitsmappeMarkierteInAsservatenkammerButton.IsEnabled = false;

            IProgress<int> fortschritt = new Progress<int>(anzahl =>
            {
                ArbeitsmappeAsservatenkammerStatusText.Foreground = Brushes.Black;
                ArbeitsmappeAsservatenkammerStatusText.Text = "James verschiebt: " + anzahl + " von " + zuVerschieben.Count + " ...";
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

                    if (verschoben % 200 == 0)
                    {
                        fortschritt.Report(verschoben);
                    }
                }

                EntferneMehrereAusErinnerungsverzeichnis(entferntePfade);
            });

            foreach (string pfad in entferntePfade)
            {
                arbeitsmappeAusgewaehlt.Remove(pfad);
            }

            if (verschoben > 0)
            {
                SpeichereDaten();
                arbeitsmappeAlleDateien = LadeErinnerungsverzeichnisDateien();
                AktualisiereAsservatenkammerAnzeige();
                ArbeitsmappeAsservatenkammerStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                ArbeitsmappeAsservatenkammerStatusText.Text = verschoben == 1
                    ? "1 markierte Erinnerung wurde in die Asservatenkammer verschoben."
                    : verschoben + " markierte Erinnerungen wurden in die Asservatenkammer verschoben.";
            }

            AktualisiereArbeitsmappe();
            ArbeitsmappeMarkierteInAsservatenkammerButton.IsEnabled = arbeitsmappeAusgewaehlt.Count > 0;
        }
    }
}
