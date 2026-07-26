using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow : Window
    {
        // ============================================================
        // NEUE FUNKTION (Generaltest 2, Wunsch von Oma+Opa): SAMMLUNG
        // ============================================================
        // Dritte Schublade neben Person und besonderem Ereignis, analog
        // zur bewährten Ereignis-Logik aufgebaut (siehe
        // MainWindow.BesondereEreignisse.cs), aber bewusst ohne Datum/
        // Ort/Jahreszeit, da eine Sammlung rein thematisch ist.

        private static string ErinnerungsOrdnerFuerSammlung(Sammlung sammlung)
        {
            return Path.Combine(ErinnerungenOrdnerPfad, "Sammlungen", sammlung.Id.ToString());
        }

        private void SammlungenListeSchreibtisch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Sammlung sammlung = SammlungenListeSchreibtisch.SelectedItem as Sammlung;

            aktuellBearbeiteteSammlung = sammlung;

            if (sammlung == null)
            {
                SammlungTitelSchreibtischTextBox.Clear();
                SammlungErinnerungenLinkText.Visibility = Visibility.Collapsed;
                SammlungSchreibtischArchivierenButton.Visibility = Visibility.Collapsed;
                return;
            }

            SammlungTitelSchreibtischTextBox.Text = sammlung.Titel;

            int anzahl = (!string.IsNullOrEmpty(sammlung.SammlungFotoDateiname) ? 1 : 0)
                + (sammlung.WeitereFotoDateinamen != null ? sammlung.WeitereFotoDateinamen.Count : 0);

            SammlungErinnerungenLinkText.Text = James.PersonErinnerungenLink(anzahl);
            SammlungErinnerungenLinkText.Tag = anzahl > 0 ? sammlung : null;
            SammlungErinnerungenLinkText.Visibility = Visibility.Visible;

            SammlungSchreibtischArchivierenButton.Visibility = Visibility.Visible;
        }

        private void SammlungSchreibtischSpeichern_Click(object sender, RoutedEventArgs e)
        {
            string titel = SammlungTitelSchreibtischTextBox.Text.Trim();

            if (titel == "")
            {
                James.Hinweis(James.BitteEreignisTitelEingeben);
                return;
            }

            if (aktuellBearbeiteteSammlung != null)
            {
                aktuellBearbeiteteSammlung.Titel = titel;
                aktuellBearbeiteteSammlung.ModifiedAt = DateTime.Now;

                SpeichereDaten();

                ZeigeStatusMeldung("„" + titel + "\u201c wurde aktualisiert.");

                aktuellBearbeiteteSammlung = null;
                SammlungTitelSchreibtischTextBox.Clear();
                SammlungErinnerungenLinkText.Visibility = Visibility.Collapsed;
                SammlungSchreibtischArchivierenButton.Visibility = Visibility.Collapsed;

                if (SammlungenListeSchreibtisch.SelectedItem != null)
                {
                    SammlungenListeSchreibtisch.SelectedItem = null;
                }

                AktualisiereSammlungenAnzeige();
                SammlungTitelSchreibtischTextBox.Focus();
            }
            else
            {
                Sammlung neueSammlung = new Sammlung { Titel = titel };

                sammlungen.Add(neueSammlung);

                SpeichereDaten();
                AktualisiereSammlungenAnzeige();

                SammlungTitelSchreibtischTextBox.Clear();

                ZeigeStatusMeldung("„" + titel + "\u201c wurde angelegt.");
            }
        }

        private void SammlungSchreibtischArchivieren_Click(object sender, RoutedEventArgs e)
        {
            Sammlung sammlung = SammlungenListeSchreibtisch.SelectedItem as Sammlung;

            if (sammlung == null)
            {
                return;
            }

            sammlungen.Remove(sammlung);
            sammlungenArchiv.Add(sammlung);

            SpeichereDaten();

            aktuellBearbeiteteSammlung = null;
            SammlungTitelSchreibtischTextBox.Clear();
            SammlungErinnerungenLinkText.Visibility = Visibility.Collapsed;
            SammlungSchreibtischArchivierenButton.Visibility = Visibility.Collapsed;

            AktualisiereSammlungenAnzeige();

            ZeigeStatusMeldung("„" + sammlung.Titel + "\u201c wurde archiviert.");
        }

        private void SammlungErinnerungenLinkText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            Sammlung sammlung = SammlungErinnerungenLinkText.Tag as Sammlung;

            if (sammlung == null)
            {
                return;
            }

            List<ErinnerungsInfo> erinnerungenListe = SammelErinnerungenFuerSammlung(sammlung);

            if (erinnerungenListe.Count == 0)
            {
                return;
            }

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelEreignis(sammlung.Titel), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal);
            fenster.Owner = this;
            fenster.Show();
        }

        private List<ErinnerungsInfo> SammelErinnerungenFuerSammlung(Sammlung sammlung)
        {
            List<ErinnerungsInfo> erinnerungenListe = new List<ErinnerungsInfo>();
            string ordner = ErinnerungsOrdnerFuerSammlung(sammlung);

            List<string> alleDateinamen = new List<string>();

            if (!string.IsNullOrEmpty(sammlung.SammlungFotoDateiname))
            {
                alleDateinamen.Add(sammlung.SammlungFotoDateiname);
            }

            if (sammlung.WeitereFotoDateinamen != null)
            {
                alleDateinamen.AddRange(sammlung.WeitereFotoDateinamen);
            }

            foreach (string dateiname in alleDateinamen)
            {
                erinnerungenListe.Add(new ErinnerungsInfo
                {
                    Pfad = Path.Combine(ordner, dateiname),
                    Titel = sammlung.Titel
                });
            }

            return erinnerungenListe;
        }

        private void AktualisiereSammlungenAnzeige()
        {
            object ausgewaehltSchreibtisch = SammlungenListeSchreibtisch.SelectedItem;
            object ausgewaehltArbeitsmappe = FreieSammlungenListe.SelectedItem;
            object ausgewaehltArchiv = ArchivSammlungenListe.SelectedItem;
            object ausgewaehltPapierkorb = SammlungenPapierkorbListe.SelectedItem;

            SammlungenListeSchreibtisch.ItemsSource = null;
            SammlungenListeSchreibtisch.ItemsSource = sammlungen;

            FreieSammlungenListe.ItemsSource = null;
            FreieSammlungenListe.ItemsSource = sammlungen;

            ArchivSammlungenListe.ItemsSource = null;
            ArchivSammlungenListe.ItemsSource = sammlungenArchiv;

            SammlungenPapierkorbListe.ItemsSource = null;
            SammlungenPapierkorbListe.ItemsSource = sammlungenPapierkorb;

            if (ausgewaehltSchreibtisch != null && sammlungen.Contains(ausgewaehltSchreibtisch))
            {
                SammlungenListeSchreibtisch.SelectedItem = ausgewaehltSchreibtisch;
            }

            if (ausgewaehltArbeitsmappe != null && sammlungen.Contains(ausgewaehltArbeitsmappe))
            {
                FreieSammlungenListe.SelectedItem = ausgewaehltArbeitsmappe;
            }

            if (ausgewaehltArchiv != null && sammlungenArchiv.Contains(ausgewaehltArchiv))
            {
                ArchivSammlungenListe.SelectedItem = ausgewaehltArchiv;
            }

            if (ausgewaehltPapierkorb != null && sammlungenPapierkorb.Contains(ausgewaehltPapierkorb))
            {
                SammlungenPapierkorbListe.SelectedItem = ausgewaehltPapierkorb;
            }
        }

        // ============================================================
        // NEUE FUNKTION: SAMMLUNG IN DER ARBEITSMAPPE
        // ============================================================

        private int VerbindeDateienMitSammlung(Sammlung sammlung, List<string> pfade, string zielOrdner)
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

                if (string.IsNullOrEmpty(sammlung.SammlungFotoDateiname))
                {
                    sammlung.SammlungFotoDateiname = neuerDateiname;
                }
                else
                {
                    if (sammlung.WeitereFotoDateinamen == null)
                    {
                        sammlung.WeitereFotoDateinamen = new List<string>();
                    }

                    sammlung.WeitereFotoDateinamen.Add(neuerDateiname);
                }

                arbeitsmappeAusgewaehlt.Remove(pfad);
                arbeitsmappeBereitsZugeordnet.Add(pfad);
                verbunden++;
            }

            if (verbunden > 0)
            {
                sammlung.ModifiedAt = DateTime.Now;
            }

            return verbunden;
        }

        private void VerknuepfeArbeitsmappenDateienMitSammlung(Sammlung sammlung, List<string> pfade)
        {
            if (sammlung == null || pfade == null || pfade.Count == 0)
            {
                return;
            }

            try
            {
                int verbunden = VerbindeDateienMitSammlung(sammlung, pfade, ErinnerungsOrdnerFuerSammlung(sammlung));

                if (verbunden > 0)
                {
                    SpeichereDaten();
                    SpeichereArbeitsmappeZugeordnet();
                    AktualisiereSammlungenAnzeige();
                }

                if (verbunden == 0)
                {
                    James.Hinweis(James.ArbeitsmappeNurBilder);
                }
                else if (verbunden == 1)
                {
                    ArbeitsmappeStatusText.Text = "Verbunden mit \u201e" + sammlung.Titel + "\u201c.";
                }
                else
                {
                    ArbeitsmappeStatusText.Text = verbunden + " Erinnerungen verbunden mit \u201e" + sammlung.Titel + "\u201c.";
                }
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimSpeichernFoto(ex.Message));
            }
        }

        private void ArbeitsmappeNeueSammlungAnlegen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            SammlungTitelTextBox.Clear();

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeNeueSammlungPanel.Visibility = Visibility.Visible;
            SammlungTitelTextBox.Focus();
        }

        private void SammlungSpeichernUndZuordnen_Click(object sender, RoutedEventArgs e)
        {
            string titel = SammlungTitelTextBox.Text.Trim();

            if (titel == "")
            {
                James.Hinweis(James.BitteEreignisTitelEingeben);
                return;
            }

            Sammlung bestehendeSammlung = sammlungen
                .Concat(sammlungenArchiv)
                .FirstOrDefault(s => string.Equals((s.Titel ?? "").Trim(), titel, StringComparison.OrdinalIgnoreCase));

            Sammlung zielSammlung;

            if (bestehendeSammlung != null && James.FrageJaNein(James.FrageEreignisBereitsVorhanden(titel), James.TitelEntscheidung))
            {
                zielSammlung = bestehendeSammlung;
            }
            else
            {
                zielSammlung = new Sammlung { Titel = titel };
                sammlungen.Add(zielSammlung);
            }

            SpeichereDaten();
            AktualisiereSammlungenAnzeige();

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();
            VerknuepfeArbeitsmappenDateienMitSammlung(zielSammlung, pfade);

            ArbeitsmappeNeueSammlungPanel.Visibility = Visibility.Collapsed;
            AktualisiereArbeitsmappe();
        }

        private void ArbeitsmappeSammlungZuordnen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeAusgewaehlt.Count == 0)
            {
                return;
            }

            if (sammlungen.Count == 0 && sammlungenArchiv.Count == 0)
            {
                James.Hinweis(James.BitteErstFreiesEreignisAnlegen);
                return;
            }

            List<Sammlung> alleAuswaehlbaren = sammlungen.Concat(sammlungenArchiv).ToList();

            SammlungComboBox.ItemsSource = alleAuswaehlbaren;
            SammlungComboBox.SelectedIndex = -1;

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeSammlungAuswahlPanel.Visibility = Visibility.Visible;
        }

        private void SammlungBestaetigen_Click(object sender, RoutedEventArgs e)
        {
            Sammlung sammlung = SammlungComboBox.SelectedItem as Sammlung;

            if (sammlung == null)
            {
                James.Hinweis(James.BitteEreignisAuswaehlen);
                return;
            }

            List<string> pfade = arbeitsmappeAusgewaehlt.ToList();
            VerknuepfeArbeitsmappenDateienMitSammlung(sammlung, pfade);

            ArbeitsmappeSammlungAuswahlPanel.Visibility = Visibility.Collapsed;
            AktualisiereArbeitsmappe();
        }

        private void FreieSammlungenListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool istAusgewaehlt = FreieSammlungenListe.SelectedItem != null;

            SammlungErinnerungenAnsehenButton.IsEnabled = istAusgewaehlt;
            SammlungArchivierenButton.IsEnabled = istAusgewaehlt;
            SammlungInPapierkorbButton.IsEnabled = istAusgewaehlt;
        }

        private void SammlungErinnerungenAnsehen_Click(object sender, RoutedEventArgs e)
        {
            Sammlung sammlung = FreieSammlungenListe.SelectedItem as Sammlung;

            if (sammlung == null)
            {
                return;
            }

            List<ErinnerungsInfo> erinnerungenListe = SammelErinnerungenFuerSammlung(sammlung);

            if (erinnerungenListe.Count == 0)
            {
                return;
            }

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelEreignis(sammlung.Titel), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal);
            fenster.Owner = this;
            fenster.Show();
        }

        private void SammlungArchivieren_Click(object sender, RoutedEventArgs e)
        {
            Sammlung sammlung = FreieSammlungenListe.SelectedItem as Sammlung;

            if (sammlung == null)
            {
                return;
            }

            sammlungen.Remove(sammlung);
            sammlungenArchiv.Add(sammlung);

            SpeichereDaten();
            AktualisiereSammlungenAnzeige();

            ArbeitsmappeStatusText.Text = "\u201e" + sammlung.Titel + "\u201c wurde archiviert.";
        }

        private void SammlungInPapierkorb_Click(object sender, RoutedEventArgs e)
        {
            Sammlung sammlung = FreieSammlungenListe.SelectedItem as Sammlung;

            if (sammlung == null)
            {
                return;
            }

            bool ergebnis = James.FrageJaNein(James.FrageInPapierkorbEinzeln(sammlung.Titel), James.TitelEntscheidung, MessageBoxImage.Warning);

            if (!ergebnis)
            {
                return;
            }

            sammlungen.Remove(sammlung);
            sammlungenPapierkorb.Add(sammlung);

            SpeichereDaten();
            AktualisiereSammlungenAnzeige();

            ArbeitsmappeStatusText.Text = "\u201e" + sammlung.Titel + "\u201c liegt jetzt im Papierkorb.";
        }

        // ============================================================
        // NEUE FUNKTION: SAMMLUNG IM ARCHIV
        // ============================================================

        private void ArchivSammlungenListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ArchivSammlungAktionPanel.Visibility = Visibility.Collapsed;
        }

        private void ArchivSammlungAktion_Click(object sender, RoutedEventArgs e)
        {
            if (ArchivSammlungenListe.SelectedItem == null)
            {
                James.Hinweis(James.BitteFreieEreignisseAuswaehlen);
                return;
            }

            ArchivSammlungAktionPanel.Visibility = ArchivSammlungAktionPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void ArchivSammlungErinnerungenAnsehen_Click(object sender, RoutedEventArgs e)
        {
            Sammlung sammlung = ArchivSammlungenListe.SelectedItem as Sammlung;

            if (sammlung == null)
            {
                return;
            }

            List<ErinnerungsInfo> erinnerungenListe = SammelErinnerungenFuerSammlung(sammlung);

            if (erinnerungenListe.Count == 0)
            {
                return;
            }

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelEreignis(sammlung.Titel), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal);
            fenster.Owner = this;
            fenster.Show();
        }

        private void ArchivSammlungWiederherstellen_Click(object sender, RoutedEventArgs e)
        {
            Sammlung sammlung = ArchivSammlungenListe.SelectedItem as Sammlung;

            if (sammlung == null)
            {
                return;
            }

            sammlungenArchiv.Remove(sammlung);
            sammlungen.Add(sammlung);

            SpeichereDaten();
            AktualisiereSammlungenAnzeige();
        }

        private void ArchivSammlungInPapierkorb_Click(object sender, RoutedEventArgs e)
        {
            List<Sammlung> ausgewaehlteSammlungen = ArchivSammlungenListe.SelectedItems.Cast<Sammlung>().ToList();

            if (ausgewaehlteSammlungen.Count == 0)
            {
                James.Hinweis(James.BitteFreieEreignisseAuswaehlen);
                return;
            }

            string frage = ausgewaehlteSammlungen.Count == 1
                ? James.FrageInPapierkorbEinzeln(ausgewaehlteSammlungen[0].Titel)
                : James.FrageInPapierkorbMehrere(ausgewaehlteSammlungen.Count);

            bool ergebnis = James.FrageJaNein(frage, James.TitelEntscheidung, MessageBoxImage.Warning);

            if (!ergebnis)
            {
                return;
            }

            foreach (Sammlung sammlung in ausgewaehlteSammlungen)
            {
                sammlungenArchiv.Remove(sammlung);
                sammlungenPapierkorb.Add(sammlung);
            }

            SpeichereDaten();
            AktualisiereSammlungenAnzeige();
        }

        // ============================================================
        // NEUE FUNKTION: SAMMLUNG IM PAPIERKORB
        // ============================================================

        private void SammlungenPapierkorbListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool istAusgewaehlt = SammlungenPapierkorbListe.SelectedItem != null;

            SammlungWiederherstellenButton.IsEnabled = istAusgewaehlt;
            SammlungEndgueltigLoeschenButton.IsEnabled = istAusgewaehlt;
        }

        private void SammlungWiederherstellen_Click(object sender, RoutedEventArgs e)
        {
            List<Sammlung> ausgewaehlteSammlungen = SammlungenPapierkorbListe.SelectedItems.Cast<Sammlung>().ToList();

            if (ausgewaehlteSammlungen.Count == 0)
            {
                James.Hinweis(James.BitteEreignisPapierkorbAuswaehlen);
                return;
            }

            foreach (Sammlung sammlung in ausgewaehlteSammlungen)
            {
                sammlungenPapierkorb.Remove(sammlung);
                sammlungen.Add(sammlung);
            }

            SpeichereDaten();
            AktualisiereSammlungenAnzeige();

            if (ausgewaehlteSammlungen.Count == 1)
            {
                James.Hinweis(James.WiederhergestelltEinzeln(ausgewaehlteSammlungen[0].Titel), James.TitelWiederhergestellt);
            }
            else
            {
                James.Hinweis(James.WiederhergestelltMehrere(ausgewaehlteSammlungen.Count), James.TitelWiederhergestellt);
            }
        }

        private void SammlungEndgueltigLoeschen_Click(object sender, RoutedEventArgs e)
        {
            List<Sammlung> ausgewaehlteSammlungen = SammlungenPapierkorbListe.SelectedItems.Cast<Sammlung>().ToList();

            if (ausgewaehlteSammlungen.Count == 0)
            {
                James.Hinweis(James.BitteEreignisPapierkorbAuswaehlen);
                return;
            }

            string frage = ausgewaehlteSammlungen.Count == 1
                ? James.FrageEndgueltigLoeschenEinzeln(ausgewaehlteSammlungen[0].Titel)
                : James.FrageEndgueltigLoeschenMehrere(ausgewaehlteSammlungen.Count);

            bool ergebnis = James.FrageJaNein(frage, James.TitelEndgueltigeEntscheidung, MessageBoxImage.Warning);

            if (ergebnis)
            {
                foreach (Sammlung sammlung in ausgewaehlteSammlungen)
                {
                    sammlungenPapierkorb.Remove(sammlung);
                }

                SpeichereDaten();
                AktualisiereSammlungenAnzeige();
            }
        }
    }
}
