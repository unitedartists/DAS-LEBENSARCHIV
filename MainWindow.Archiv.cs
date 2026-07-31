using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow
    {
        // ============================================================
        // ARCHITEKTUR: ARCHIV-SPEICHERORT WÄHLEN UND UMZIEHEN (31.07.,
        // gemeinsam mit dem Architekten festgelegt, mit Ergänzung des
        // Architekten zum zweistufigen, neustart-basierten Ablauf)
        // ============================================================
        // Ablauf, wie mit dem Architekten abgestimmt:
        // Phase 1: Dateien kopieren und jede einzelne prüfen.
        // Phase 2: Neuer Speicherort wird aktiviert, James muss dazu
        //          einmal neu gestartet werden.
        // Danach:  Der Benutzer prüft in Ruhe (Personen/Ereignisse/
        //          Sammlungen/Bilder/Asservatenkammer), ob alles da ist.
        //          ERST DANACH kann er den alten Speicherort über einen
        //          eigenen Button freiwillig löschen - nie automatisch,
        //          nie direkt nach dem Kopieren.

        // Optimierungswunsch (31.07.): Abbrechen-Möglichkeit während des
        // Kopierens, z.B. falls versehentlich ein viel zu großer Ordner
        // gewählt wurde.
        private CancellationTokenSource archivUmzugAbbruchQuelle;

        private void ZeigeAktuellenArchivSpeicherort()
        {
            if (ArchivSpeicherortAktuellerPfadText == null)
            {
                return;
            }

            ArchivSpeicherortAktuellerPfadText.Text = "Aktueller Speicherort: " + OrdnerPfad;

            // Wunsch des Architekten: nach einem Neustart prüfen, ob noch
            // ein alter Speicherort auf seine Löschung wartet, und - falls
            // ja - das Angebot dazu anzeigen (nicht vorher, nicht automatisch).
            ArchivStandortKonfiguration konfiguration = LadeArchivStandortKonfiguration();

            if (!string.IsNullOrWhiteSpace(konfiguration.AlterPfadZumLoeschen)
                && Directory.Exists(konfiguration.AlterPfadZumLoeschen))
            {
                ArchivAltenOrdnerPanel.Visibility = Visibility.Visible;
                ArchivAltenOrdnerPfadText.Text = konfiguration.AlterPfadZumLoeschen;
            }
            else
            {
                ArchivAltenOrdnerPanel.Visibility = Visibility.Collapsed;
            }
        }

        private void ArchivSpeicherortOeffnen_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(OrdnerPfad);

                ProcessStartInfo start = new ProcessStartInfo
                {
                    FileName = OrdnerPfad,
                    UseShellExecute = true
                };

                Process.Start(start);
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimOeffnenDerErinnerung(ex.Message));
            }
        }

        private void ArchivSpeicherortAendern_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFolderDialog dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Neuen Archiv-Speicherort wählen",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            string neuerPfad = dialog.FolderName;

            if (string.IsNullOrWhiteSpace(neuerPfad))
            {
                return;
            }

            string alterPfad = OrdnerPfad;

            if (string.Equals(Path.GetFullPath(neuerPfad).TrimEnd('\\'), Path.GetFullPath(alterPfad).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
            {
                ArchivUmzugStatusText.Foreground = Brushes.Black;
                ArchivUmzugStatusText.Text = "Das ist bereits der aktuelle Speicherort.";
                return;
            }

            // James legt selbständig einen eigenen Unterordner an, damit der
            // Benutzer nur den obersten Ordner wählen muss (Wunsch des
            // Architekten) - z.B. gewählt: H:\, tatsächlicher Archivordner:
            // H:\Lebensarchiv.
            string zielArchivOrdner = Path.GetFileName(neuerPfad.TrimEnd('\\')) == "Lebensarchiv"
                ? neuerPfad
                : Path.Combine(neuerPfad, "Lebensarchiv");

            StarteArchivUmzugPhase1(alterPfad, zielArchivOrdner);
        }

        // Phase 1 (Wunsch des Architekten): nur kopieren und prüfen - der
        // neue Speicherort wird hier noch NICHT aktiv genutzt.
        private async void StarteArchivUmzugPhase1(string alterPfad, string neuerPfad)
        {
            ArchivSpeicherortAendernButton.IsEnabled = false;
            ArchivNeustartPanel.Visibility = Visibility.Collapsed;
            ArchivUmzugStatusText.Foreground = Brushes.Black;
            ArchivUmzugStatusText.Text = "James kopiert und prüft die Dateien, bitte kurz warten ...";

            IProgress<string> fortschritt = new Progress<string>(text =>
            {
                ArchivUmzugStatusText.Text = text;
            });

            ArchivUmzugErgebnis ergebnis;

            archivUmzugAbbruchQuelle = new CancellationTokenSource();
            ArchivUmzugAbbrechenButton.Visibility = Visibility.Visible;

            try
            {
                ergebnis = await Task.Run(() => KopiereUndPruefeArchiv(alterPfad, neuerPfad, fortschritt, archivUmzugAbbruchQuelle.Token));
            }
            catch (OperationCanceledException)
            {
                ArchivUmzugAbbrechenButton.Visibility = Visibility.Collapsed;
                ArchivUmzugStatusText.Foreground = Brushes.Black;
                ArchivUmzugStatusText.Text = "Kopieren abgebrochen. Der alte Speicherort ist unverändert und wird weiter verwendet.\n\n" +
                    "Der neue, noch unvollständige Ordner wurde NICHT automatisch gelöscht:\n" + neuerPfad +
                    "\nSie können ihn bei Bedarf selbst im Explorer löschen, oder es später erneut versuchen.";
                ArchivSpeicherortAendernButton.IsEnabled = true;
                archivUmzugAbbruchQuelle = null;
                return;
            }
            catch (Exception ex)
            {
                ArchivUmzugAbbrechenButton.Visibility = Visibility.Collapsed;
                ArchivUmzugStatusText.Foreground = Brushes.Firebrick;
                ArchivUmzugStatusText.Text = "Beim Umzug ist ein Fehler aufgetreten: " + ex.Message + " - der alte Speicherort wurde nicht verändert.";
                ArchivSpeicherortAendernButton.IsEnabled = true;
                archivUmzugAbbruchQuelle = null;
                return;
            }

            ArchivUmzugAbbrechenButton.Visibility = Visibility.Collapsed;
            archivUmzugAbbruchQuelle = null;

            if (ergebnis.FehlgeschlageneDateien.Count > 0)
            {
                ArchivUmzugStatusText.Foreground = Brushes.Firebrick;
                ArchivUmzugStatusText.Text = ergebnis.GeprueftErfolgreich + " von " + ergebnis.GesamtDateien +
                    " Dateien wurden erfolgreich kopiert und geprüft, aber " + ergebnis.FehlgeschlageneDateien.Count +
                    " Datei(en) konnten nicht sicher kopiert werden. Der alte Speicherort wurde zur Sicherheit NICHT verändert, und James verwendet ihn unverändert weiter.";
                ArchivSpeicherortAendernButton.IsEnabled = true;
                return;
            }

            // Phase 1 abgeschlossen: alles kopiert und geprüft. Der alte
            // Speicherort bleibt bis auf Weiteres vollständig erhalten -
            // James braucht jetzt erst einen Neustart, um wirklich auf den
            // neuen Speicherort zu wechseln (Wunsch des Architekten).
            ArchivStandortKonfiguration konfiguration = new ArchivStandortKonfiguration
            {
                ArchivPfad = neuerPfad,
                AlterPfadZumLoeschen = alterPfad
            };
            SpeichereArchivStandortKonfiguration(konfiguration);

            ArchivUmzugStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
            ArchivUmzugStatusText.Text = ergebnis.GeprueftErfolgreich + " von " + ergebnis.GesamtDateien +
                " Dateien wurden erfolgreich kopiert und geprüft.\n\nJames muss jetzt einmal neu gestartet werden, damit er den neuen Speicherort tatsächlich verwendet. Der alte Speicherort bleibt dabei vollständig erhalten.";

            ArchivNeustartPanel.Visibility = Visibility.Visible;
            ArchivSpeicherortAendernButton.IsEnabled = true;
        }

        private ArchivUmzugErgebnis KopiereUndPruefeArchiv(string alterPfad, string neuerPfad, IProgress<string> fortschritt, CancellationToken abbruchToken)
        {
            ArchivUmzugErgebnis ergebnis = new ArchivUmzugErgebnis();

            Directory.CreateDirectory(neuerPfad);

            if (!Directory.Exists(alterPfad))
            {
                // Es gibt noch gar nichts zum Umziehen (ganz frische
                // Installation) - der neue, leere Ordner reicht bereits aus.
                return ergebnis;
            }

            List<string> alleQuellDateien = Directory.GetFiles(alterPfad, "*", SearchOption.AllDirectories).ToList();
            ergebnis.GesamtDateien = alleQuellDateien.Count;

            int erledigt = 0;

            foreach (string quellDatei in alleQuellDateien)
            {
                abbruchToken.ThrowIfCancellationRequested();

                string relativerPfad = Path.GetRelativePath(alterPfad, quellDatei);
                string zielDatei = Path.Combine(neuerPfad, relativerPfad);

                try
                {
                    string zielOrdner = Path.GetDirectoryName(zielDatei);

                    if (!string.IsNullOrEmpty(zielOrdner))
                    {
                        Directory.CreateDirectory(zielOrdner);
                    }

                    File.Copy(quellDatei, zielDatei, overwrite: true);

                    FileInfo quellInfo = new FileInfo(quellDatei);
                    FileInfo zielInfo = new FileInfo(zielDatei);

                    if (zielInfo.Exists && zielInfo.Length == quellInfo.Length)
                    {
                        ergebnis.GeprueftErfolgreich++;
                    }
                    else
                    {
                        ergebnis.FehlgeschlageneDateien.Add(relativerPfad);
                    }
                }
                catch
                {
                    ergebnis.FehlgeschlageneDateien.Add(relativerPfad);
                }

                erledigt++;

                if (erledigt % 25 == 0 || erledigt == ergebnis.GesamtDateien)
                {
                    fortschritt.Report("James kopiert und prüft: " + erledigt + " von " + ergebnis.GesamtDateien + " Dateien ...");
                }
            }

            return ergebnis;
        }

        // Optimierungswunsch (31.07.): Kopieren jederzeit abbrechen können,
        // z.B. falls versehentlich ein viel zu großer Ordner gewählt wurde.
        private void ArchivUmzugAbbrechen_Click(object sender, RoutedEventArgs e)
        {
            archivUmzugAbbruchQuelle?.Cancel();
        }

        // Wunsch des Architekten: eigener, klar sichtbarer Neustart-Schritt
        // zwischen Kopieren und der Möglichkeit, den alten Ordner zu löschen.
        private void ArchivJetztNeuStarten_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string exePfad = Process.GetCurrentProcess().MainModule.FileName;
                Process.Start(exePfad);
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimOeffnenDerErinnerung(ex.Message));
                return;
            }

            Application.Current.Shutdown();
        }

        // Nach dem Neustart, sobald der Benutzer in Ruhe geprüft hat, dass
        // am neuen Speicherort alles vorhanden ist: alten Ordner löschen.
        private void ArchivAltenOrdnerLeeren_Click(object sender, RoutedEventArgs e)
        {
            ArchivStandortKonfiguration konfiguration = LadeArchivStandortKonfiguration();
            string alterPfad = konfiguration.AlterPfadZumLoeschen;

            if (string.IsNullOrEmpty(alterPfad) || !Directory.Exists(alterPfad))
            {
                ArchivAltenOrdnerPanel.Visibility = Visibility.Collapsed;
                return;
            }

            try
            {
                foreach (string datei in Directory.GetFiles(alterPfad, "*", SearchOption.AllDirectories))
                {
                    // Der winzige Zeiger (archivstandort.json) darf nicht
                    // mitgelöscht werden, falls er zufällig im selben Ordner
                    // liegt (Rückwärtskompatibilität mit dem bisherigen Ort).
                    if (string.Equals(datei, ArchivStandortZeigerPfad, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    File.Delete(datei);
                }

                foreach (string unterordner in Directory.GetDirectories(alterPfad))
                {
                    try
                    {
                        Directory.Delete(unterordner, recursive: true);
                    }
                    catch
                    {
                        // Einzelner Unterordner konnte nicht gelöscht werden
                        // (z.B. noch geöffnete Datei) - nicht schlimm, der Rest
                        // wird trotzdem geleert.
                    }
                }

                konfiguration.AlterPfadZumLoeschen = null;
                SpeichereArchivStandortKonfiguration(konfiguration);

                ArchivUmzugStatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                ArchivUmzugStatusText.Text = "Der alte Speicherort wurde geleert.";
            }
            catch (Exception ex)
            {
                ArchivUmzugStatusText.Foreground = Brushes.Firebrick;
                ArchivUmzugStatusText.Text = "Der alte Speicherort konnte nicht vollständig geleert werden: " + ex.Message +
                    " - Sie können das auch später jederzeit von Hand im Explorer nachholen.";
            }

            ArchivAltenOrdnerPanel.Visibility = Visibility.Collapsed;
        }

        private void ArchivAltenOrdnerBehalten_Click(object sender, RoutedEventArgs e)
        {
            // Benutzer möchte (noch) nicht löschen - Eintrag bleibt bestehen,
            // das Angebot erscheint beim nächsten Programmstart einfach wieder.
            ArchivUmzugStatusText.Foreground = Brushes.Black;
            ArchivUmzugStatusText.Text = "In Ordnung - der alte Speicherort bleibt vorerst bestehen. Sie können ihn jederzeit später hier oder von Hand im Explorer löschen.";
            ArchivAltenOrdnerPanel.Visibility = Visibility.Collapsed;
        }
    }

    public class ArchivUmzugErgebnis
    {
        public int GesamtDateien { get; set; }
        public int GeprueftErfolgreich { get; set; }
        public List<string> FehlgeschlageneDateien { get; set; } = new List<string>();
    }
}
