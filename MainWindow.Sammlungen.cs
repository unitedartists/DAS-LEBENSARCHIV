using System;
using System.Collections.Generic;
using System.IO;
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

            SammlungenListeSchreibtisch.ItemsSource = null;
            SammlungenListeSchreibtisch.ItemsSource = sammlungen;

            if (ausgewaehltSchreibtisch != null && sammlungen.Contains(ausgewaehltSchreibtisch))
            {
                SammlungenListeSchreibtisch.SelectedItem = ausgewaehltSchreibtisch;
            }
        }
    }
}
