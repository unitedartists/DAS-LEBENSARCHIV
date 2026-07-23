using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow
    {
        // ============================================================
        // BUILD 2.0: JAMES LERNT SEINEN BESITZER KENNEN
        // ============================================================
        // Alle Fragen sind freiwillig (überspringbar) und jederzeit
        // änderbar. Dieser Build sammelt und speichert die Antworten nur -
        // er wertet sie (noch) nicht aus. Keine KI, keine Analyse.

        private Einstellungen LadeEinstellungen()
        {
            try
            {
                if (File.Exists(EinstellungenPfad))
                {
                    string json = File.ReadAllText(EinstellungenPfad);
                    Einstellungen geladen = JsonSerializer.Deserialize<Einstellungen>(json);

                    if (geladen != null)
                    {
                        return geladen;
                    }
                }
            }
            catch
            {
                // Einstellungen nicht lesbar - dann eben mit leeren
                // Einstellungen weiterarbeiten (keine Fehlermeldung fuer
                // eine reine Komfortfunktion).
            }

            return new Einstellungen();
        }

        private void SpeichereEinstellungen(Einstellungen einstellungen)
        {
            try
            {
                Directory.CreateDirectory(OrdnerPfad);

                JsonSerializerOptions optionen = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(einstellungen, optionen);

                File.WriteAllText(EinstellungenPfad, json);
            }
            catch (System.Exception ex)
            {
                James.Problem(James.FehlerBeimSpeichernEinstellungen(ex.Message));
            }
        }

        // Zeigt die gespeicherten Einstellungen (falls vorhanden) in den
        // Auswahlfeldern auf dem Einstellungen-Reiter an.
        private void ZeigeEinstellungenImFormular()
        {
            Einstellungen einstellungen = LadeEinstellungen();

            SetzeComboBoxAufWert(EinstellungAnredeComboBox, einstellungen.Anrede);
            SetzeComboBoxAufWert(EinstellungArbeitsweiseComboBox, einstellungen.Arbeitsweise);
            SetzeComboBoxAufWert(EinstellungSchwerpunktComboBox, einstellungen.Schwerpunkt);
            SetzeComboBoxAufWert(EinstellungHinweisHaeufigkeitComboBox, einstellungen.HinweisHaeufigkeit);
            SetzeComboBoxAufWert(EinstellungSchrittgroesseComboBox, einstellungen.Schrittgroesse);
        }

        private static void SetzeComboBoxAufWert(ComboBox box, string wert)
        {
            if (!string.IsNullOrEmpty(wert))
            {
                foreach (object element in box.Items)
                {
                    ComboBoxItem eintrag = element as ComboBoxItem;

                    if (eintrag != null && eintrag.Content.ToString() == wert)
                    {
                        box.SelectedItem = eintrag;
                        return;
                    }
                }
            }

            box.SelectedIndex = 0;
        }

        private static string GelesenerComboBoxWert(ComboBox box)
        {
            ComboBoxItem ausgewaehlt = box.SelectedItem as ComboBoxItem;

            if (ausgewaehlt == null)
            {
                return null;
            }

            string text = ausgewaehlt.Content.ToString();

            return string.IsNullOrEmpty(text) ? null : text;
        }

        private void EinstellungenSpeichern_Click(object sender, RoutedEventArgs e)
        {
            Einstellungen einstellungen = new Einstellungen
            {
                Anrede = GelesenerComboBoxWert(EinstellungAnredeComboBox),
                Arbeitsweise = GelesenerComboBoxWert(EinstellungArbeitsweiseComboBox),
                Schwerpunkt = GelesenerComboBoxWert(EinstellungSchwerpunktComboBox),
                HinweisHaeufigkeit = GelesenerComboBoxWert(EinstellungHinweisHaeufigkeitComboBox),
                Schrittgroesse = GelesenerComboBoxWert(EinstellungSchrittgroesseComboBox)
            };

            SpeichereEinstellungen(einstellungen);

            EinstellungenStatusText.Text = James.EinstellungenGespeichert;
        }
    }
}