using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Security.Cryptography;
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
    }
}
