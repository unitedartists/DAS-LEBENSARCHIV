using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow : Window
    {
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

        private async void ErinnerungenAufraeumen_Click(object sender, RoutedEventArgs e)
        {
            // Neue Funktion (Generaltest 2, Wunsch von Oma+Opa): keine
            // Rückfrage, keine Vorschau mehr - James verschiebt erkannte
            // (exakte) Duplikate direkt und automatisch in die
            // Asservatenkammer. Läuft im Hintergrund mit Fortschrittsanzeige,
            // damit die Oberfläche bei sehr vielen Duplikaten (zehntausende
            // Dateien) nicht einfriert.
            if (arbeitsmappeAlleDateien == null || arbeitsmappeAlleDateien.Count == 0)
            {
                arbeitsmappeAlleDateien = LadeErinnerungsverzeichnisDateien();
            }

            if (arbeitsmappeAlleDateien.Count == 0)
            {
                James.Hinweis(James.KeinErinnerungsverzeichnisGefunden);
                return;
            }

            Dictionary<string, List<GefundeneDatei>> gruppen = ErmittleDuplikatGruppen();

            List<GefundeneDatei> zuVerschieben = new List<GefundeneDatei>();

            foreach (List<GefundeneDatei> gruppe in gruppen.Values)
            {
                for (int i = 1; i < gruppe.Count; i++)
                {
                    zuVerschieben.Add(gruppe[i]);
                }
            }

            if (zuVerschieben.Count == 0)
            {
                DoppelgaengerErgebnisPanel.Visibility = Visibility.Visible;
                DoppelgaengerListe.Items.Clear();
                DoppelgaengerDetailsText.Text = "";
                DoppelgaengerStatusText.Text = James.KeineDoppelgaengerGefunden;
                return;
            }

            ErinnerungenAufraeumenButton.IsEnabled = false;

            IProgress<int> fortschritt = new Progress<int>(anzahl =>
            {
                int prozent = (int)(100.0 * anzahl / zuVerschieben.Count);
                DoppelgaengerFortschrittsleiste.Visibility = Visibility.Visible;
                DoppelgaengerFortschrittsleiste.Value = prozent;
                DoppelgaengerStatusText.Text = "James räumt auf: " + prozent + " % erledigt (" + anzahl + " von " + zuVerschieben.Count + " Duplikaten verschoben) ...";
            });

            HashSet<string> entferntePfade = new HashSet<string>();
            int verschoben = 0;

            DoppelgaengerErgebnisPanel.Visibility = Visibility.Visible;
            DoppelgaengerListe.Items.Clear();
            DoppelgaengerDetailsText.Text = "";

            await Task.Run(() =>
            {
                foreach (GefundeneDatei datei in zuVerschieben)
                {
                    if (VerschiebeInAsservatenkammer(datei, "Duplikat"))
                    {
                        entferntePfade.Add(datei.VollstaendigerPfad);
                        verschoben++;
                    }

                    if (verschoben % 10 == 0 || verschoben == zuVerschieben.Count)
                    {
                        fortschritt.Report(verschoben);
                    }
                }

                EntferneMehrereAusErinnerungsverzeichnis(entferntePfade);
            });

            if (verschoben > 0)
            {
                SpeichereDaten();
                arbeitsmappeAlleDateien = LadeErinnerungsverzeichnisDateien();
                AktualisiereAsservatenkammerAnzeige();
                DoppelgaengerStatusText.Text = verschoben + " doppelte Datei(en) wurden automatisch in die Asservatenkammer verschoben.";
            }
            else
            {
                DoppelgaengerStatusText.Text = James.KeineDoppelgaengerGefunden;
            }

            DoppelgaengerFortschrittsleiste.Visibility = Visibility.Collapsed;
            ErinnerungenAufraeumenButton.IsEnabled = true;
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

            // Neue Funktion (Generaltest 2, Wunsch von Oma+Opa): bereits
            // durchsuchte Ordner im Baum sichtbar kennzeichnen, damit der
            // Benutzer erkennt, was James schon "inventarisiert" hat -
            // unabhängig davon, was James sich sonst noch dazu merkt.
            Ordnergedaechtnis gedaechtnisFuerMarkierung = LadeOrdnergedaechtnis();

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
                    Name = MarkiereFallsBereitsDurchsucht(beschriftung, laufwerk.Name, gedaechtnisFuerMarkierung),
                    VollstaendigerPfad = laufwerk.Name
                };

                wurzelKnoten.Kinder.Add(ErzeugePlatzhalterKnoten());

                ordnerBaumWurzelKnoten.Add(wurzelKnoten);
            }

            OrdnerAuswahlPanel.Visibility = Visibility.Visible;
            WerkzeugeStatusText.Text = ErstelleOrdnergedaechtnisBegruessung();
        }

        // Neue Funktion (Generaltest 2, Wunsch von Oma+Opa): stellt einem
        // Ordnernamen ein Häkchen voran, wenn James diesen Ordner laut
        // Ordnergedächtnis schon einmal durchsucht hat - rein visuell,
        // ändert nichts an den gespeicherten Daten selbst.
        private static string MarkiereFallsBereitsDurchsucht(string name, string pfad, Ordnergedaechtnis gedaechtnis)
        {
            bool bereitsDurchsucht = gedaechtnis.Ordner.Any(o => o.Pfad == pfad);
            return bereitsDurchsucht ? "✓ " + name : name;
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

            Ordnergedaechtnis gedaechtnisFuerMarkierung = LadeOrdnergedaechtnis();

            try
            {
                foreach (string unterordnerPfad in Directory.EnumerateDirectories(knoten.VollstaendigerPfad))
                {
                    try
                    {
                        DirectoryInfo info = new DirectoryInfo(unterordnerPfad);

                        OrdnerKnoten unterKnoten = new OrdnerKnoten
                        {
                            Name = MarkiereFallsBereitsDurchsucht(info.Name, info.FullName, gedaechtnisFuerMarkierung),
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

            // Optimierungsrunde (06.08.), Punkt 3: der Button "Alle Funde
            // auf den Schreibtisch" soll erst erscheinen, wenn tatsächlich
            // ein frisches Ergebnis vorliegt - während des Rundgangs bleibt
            // er verborgen.
            WerkzeugeAlleFundeAufSchreibtischButton.Visibility = Visibility.Collapsed;

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

            int gesamtAnzahlNachDiesemRundgang = 0;

            try
            {
                Directory.CreateDirectory(OrdnerPfad);

                // Neue Funktion (Generaltest 2, Wunsch von Oma+Opa): Rundgang-
                // Ergebnisse werden ab jetzt gesammelt statt überschrieben.
                // Bereits bekannte Dateien (gleicher vollständiger Pfad)
                // werden durch den frischen Stand ersetzt, alles andere aus
                // früheren Rundgängen bleibt erhalten. Das ist die
                // notwendige Voraussetzung dafür, dass bereits durchsuchte
                // Ordner beim nächsten Mal übersprungen werden können, ohne
                // ihre Ergebnisse zu verlieren.
                List<GefundeneDatei> bestehendeDateien = new List<GefundeneDatei>();

                if (File.Exists(ErinnerungsVerzeichnisPfad))
                {
                    try
                    {
                        string bestehenderJson = File.ReadAllText(ErinnerungsVerzeichnisPfad);
                        ErinnerungsVerzeichnis bestehendesVerzeichnis = JsonSerializer.Deserialize<ErinnerungsVerzeichnis>(bestehenderJson);

                        if (bestehendesVerzeichnis != null && bestehendesVerzeichnis.Dateien != null)
                        {
                            bestehendeDateien = bestehendesVerzeichnis.Dateien;
                        }
                    }
                    catch
                    {
                    }
                }

                Dictionary<string, GefundeneDatei> zusammengefasst = new Dictionary<string, GefundeneDatei>();

                foreach (GefundeneDatei datei in bestehendeDateien)
                {
                    if (datei.VollstaendigerPfad != null)
                    {
                        zusammengefasst[datei.VollstaendigerPfad] = datei;
                    }
                }

                foreach (GefundeneDatei datei in gefundeneDateien)
                {
                    zusammengefasst[datei.VollstaendigerPfad] = datei;
                }

                ErinnerungsVerzeichnis verzeichnis = new ErinnerungsVerzeichnis
                {
                    ErstelltAm = DateTime.Now,
                    Dateien = zusammengefasst.Values.ToList()
                };

                JsonSerializerOptions optionen = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(verzeichnis, optionen);

                File.WriteAllText(ErinnerungsVerzeichnisPfad, json);

                // Die Zusammenfassung bezieht sich jetzt auf den
                // gesamten bekannten Bestand, nicht nur auf diesen
                // einzelnen Rundgang - bleibt dadurch auch nach einem
                // Neustart über ZeigeGespeicherteZusammenfassung() gültig.
                Dictionary<string, int> zaehlerGesamt = zusammengefasst.Values
                    .GroupBy(d => d.Dateityp)
                    .ToDictionary(g => g.Key, g => g.Count());

                gesamtAnzahlNachDiesemRundgang = zusammengefasst.Count;

                WerkzeugeStatusText.Text = James.RundgangZusammenfassung(wurdeAbgebrochen, zaehlerGesamt);
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

            // Optimierungsrunde (06.08.), Punkt 3: Nach einem erfolgreichen
            // Rundgang mit Ergebnissen bietet James an, direkt zur
            // Arbeitsmappe zu wechseln - rein anzeigend, keine
            // Dateioperation. Die Arbeitsmappe zeigt ohnehin bereits den
            // gesamten bekannten Bestand (siehe OeffneArbeitsmappe), dieser
            // Button macht den natürlichen nächsten Schritt nur sichtbar.
            if (gesamtAnzahlNachDiesemRundgang > 0)
            {
                WerkzeugeAlleFundeAufSchreibtischButton.Visibility = Visibility.Visible;
            }

            RundgangStartenButton.IsEnabled = true;
            ComputerKennenlernenButton.IsEnabled = true;
            AbbrechenRundgangButton.Visibility = Visibility.Collapsed;
        }

        // Optimierungsrunde (06.08.), Punkt 3: schaltet einfach zur
        // Arbeitsmappe um - kein Kopieren, kein Verschieben. Die
        // Arbeitsmappe lädt beim Öffnen automatisch den gesamten bekannten
        // Bestand aus dem Erinnerungsverzeichnis (OeffneArbeitsmappe),
        // wiederverwendet also bereits vorhandene Technik statt etwas
        // Neues zu bauen. "Markierte Funde auf Schreibtisch" (nur einen
        // Teil der Funde übernehmen) ist bewusst noch NICHT umgesetzt -
        // dafür fehlt bislang eine anklickbare Fundliste direkt nach dem
        // Rundgang; das wäre eine neue Oberfläche, kein Wiederverwenden
        // von Bestehendem, und wurde deshalb zurückgestellt.
        private void WerkzeugeAlleFundeAufSchreibtisch_Click(object sender, RoutedEventArgs e)
        {
            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;
        }

        // Neue Funktion (Generaltest 2, Wunsch von Oma+Opa): berechnet die
        // "X Bilder, X Videos, ..."-Übersicht aus dem bereits gespeicherten
        // Erinnerungsverzeichnis - bleibt dadurch auch nach einem Neustart
        // von James sichtbar, ganz ohne neuen Rundgang.
        private void ZeigeGespeicherteZusammenfassung()
        {
            if (!File.Exists(ErinnerungsVerzeichnisPfad))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(ErinnerungsVerzeichnisPfad);
                ErinnerungsVerzeichnis verzeichnis = JsonSerializer.Deserialize<ErinnerungsVerzeichnis>(json);

                if (verzeichnis == null || verzeichnis.Dateien == null || verzeichnis.Dateien.Count == 0)
                {
                    return;
                }

                Dictionary<string, int> zaehlerProTyp = verzeichnis.Dateien
                    .GroupBy(d => d.Dateityp)
                    .ToDictionary(g => g.Key, g => g.Count());

                WerkzeugeStatusText.Text = James.RundgangZusammenfassung(false, zaehlerProTyp);

                // Auch beim Neustart erscheint der Button, wenn bereits
                // Funde vorliegen - nicht nur direkt nach einem frischen
                // Rundgang.
                WerkzeugeAlleFundeAufSchreibtischButton.Visibility = Visibility.Visible;
            }
            catch
            {
            }
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
    }
}
