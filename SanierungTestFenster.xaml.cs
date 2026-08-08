using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;

namespace DAS_LEBENSARCHIV
{
    // ============================================================
    // SANIERUNG BAUPHASE 1 - TESTBEREICH (08.08.)
    // ============================================================
    // Beweist am lebenden, aber vollständig isolierten Beispiel: eine
    // Erinnerung kann mehrere Fundorte UND mehrere Zuordnungen
    // gleichzeitig haben; das Entfernen einer Zuordnung lässt
    // Erinnerung, Fundorte und alle anderen Zuordnungen unangetastet -
    // und erzeugt zu keinem Zeitpunkt eine zusätzliche physische
    // Kopie (an keiner Stelle in dieser Datei wird File.Copy
    // aufgerufen). Alles ausschließlich im Arbeitsspeicher dieses
    // Fensters - keine Verbindung zu personen.json oder dem echten
    // Archiv, nichts wird gespeichert.
    public partial class SanierungTestFenster : Window
    {
        private Erinnerung aktuelleErinnerung;
        private readonly List<Zuordnung> zuordnungen = new List<Zuordnung>();

        public SanierungTestFenster()
        {
            InitializeComponent();
        }

        private static string BerechneHash(string pfad)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = File.OpenRead(pfad))
            {
                byte[] hash = sha256.ComputeHash(stream);
                return Convert.ToHexString(hash);
            }
        }

        private void NeueErinnerung_Click(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Testbild für die neue Erinnerung wählen",
                Filter = "Bilder (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|Alle Dateien (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            aktuelleErinnerung = new Erinnerung
            {
                Hashwert = BerechneHash(dialog.FileName)
            };

            aktuelleErinnerung.Fundorte.Add(new Fundort { Pfad = dialog.FileName });

            zuordnungen.Clear();

            AktualisiereAnzeige();

            BeweisText.Text = "Neue Test-Erinnerung angelegt. Noch kein Entfernungstest durchgeführt.";

            FundortHinzufuegenButton.IsEnabled = true;
            ZuordnungHinzufuegenButton.IsEnabled = true;
        }

        private void FundortHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            if (aktuelleErinnerung == null)
            {
                return;
            }

            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Weiteren Fundort wählen (z.B. dieselbe Datei an anderer Stelle, etwa eine Sicherungskopie)",
                Filter = "Bilder (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|Alle Dateien (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            // Bewusst KEIN File.Copy - ein Fundort ist ein VERWEIS auf
            // eine bereits existierende Datei, keine neue Kopie.
            aktuelleErinnerung.Fundorte.Add(new Fundort { Pfad = dialog.FileName });

            AktualisiereAnzeige();
        }

        private void ZuordnungHinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            if (aktuelleErinnerung == null)
            {
                return;
            }

            string zielBezeichnung = ZielBezeichnungTextBox.Text.Trim();

            if (zielBezeichnung == "")
            {
                James.Hinweis("Bitte zuerst eine Bezeichnung für das Ziel eingeben (z.B. einen Namen).");
                return;
            }

            ComboBoxItem ausgewaehlt = ZielTypComboBox.SelectedItem as ComboBoxItem;
            string ausgewaehlterText = ausgewaehlt != null ? ausgewaehlt.Content.ToString() : "Person";

            ZuordnungsZielTyp zielTyp = ausgewaehlterText == "Ereignis"
                ? ZuordnungsZielTyp.Ereignis
                : ausgewaehlterText == "Sammlung"
                    ? ZuordnungsZielTyp.Sammlung
                    : ZuordnungsZielTyp.Person;

            // Bewusst KEIN File.Copy, KEIN neuer Dateiname - eine
            // Zuordnung ist ausschließlich ein Datensatz-Verweis
            // (Erinnerung <-> Ziel), siehe A's Grundregel vom 08.08.
            zuordnungen.Add(new Zuordnung
            {
                ErinnerungId = aktuelleErinnerung.Id,
                ZielTyp = zielTyp,
                ZielBezeichnung = zielBezeichnung
            });

            ZielBezeichnungTextBox.Clear();

            AktualisiereAnzeige();
        }

        private void ZuordnungenListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ZuordnungEntfernenButton.IsEnabled = ZuordnungenListe.SelectedItem != null;
        }

        // Testet exakt die Papierkorb-Kontext-Regel: eine Zuordnung
        // entfernen, danach beweisen, dass Erinnerung, ihre Fundorte
        // und alle ÜBRIGEN Zuordnungen unverändert bestehen bleiben.
        private void ZuordnungEntfernen_Click(object sender, RoutedEventArgs e)
        {
            Zuordnung ausgewaehlt = ZuordnungenListe.SelectedItem as Zuordnung;

            if (ausgewaehlt == null || aktuelleErinnerung == null)
            {
                return;
            }

            int fundorteVorher = aktuelleErinnerung.Fundorte.Count;
            int zuordnungenVorher = zuordnungen.Count;
            Guid erinnerungIdVorher = aktuelleErinnerung.Id;
            string hashVorher = aktuelleErinnerung.Hashwert;
            string entfernteBezeichnung = ausgewaehlt.ToString();

            zuordnungen.Remove(ausgewaehlt);

            AktualisiereAnzeige();

            bool erinnerungUnveraendert = aktuelleErinnerung.Id == erinnerungIdVorher && aktuelleErinnerung.Hashwert == hashVorher;
            bool fundorteUnveraendert = aktuelleErinnerung.Fundorte.Count == fundorteVorher;
            bool nurEineZuordnungWeniger = zuordnungen.Count == zuordnungenVorher - 1;
            bool alleFundortDateienNochDa = aktuelleErinnerung.Fundorte.All(f => File.Exists(f.Pfad));

            BeweisText.Text =
                "Entfernt: \"" + entfernteBezeichnung + "\".\n\n" +
                "✓ Erinnerung unverändert (gleiche Id, gleicher Hash): " + (erinnerungUnveraendert ? "JA" : "NEIN") + "\n" +
                "✓ Alle Fundorte weiterhin vorhanden (" + aktuelleErinnerung.Fundorte.Count + " von " + fundorteVorher + "): " + (fundorteUnveraendert ? "JA" : "NEIN") + "\n" +
                "✓ Physische Dateien an allen Fundorten weiterhin auf der Festplatte: " + (alleFundortDateienNochDa ? "JA" : "NEIN") + "\n" +
                "✓ Genau eine Zuordnung weniger, Rest unverändert (" + zuordnungen.Count + " von " + zuordnungenVorher + "): " + (nurEineZuordnungWeniger ? "JA" : "NEIN") + "\n" +
                "✓ Keine zusätzliche Datei entstanden (dieser Testcode ruft an keiner Stelle File.Copy auf).";
        }

        private void AktualisiereAnzeige()
        {
            if (aktuelleErinnerung == null)
            {
                ErinnerungInfoText.Text = "Noch keine Test-Erinnerung angelegt.";
                FundorteListe.ItemsSource = null;
                ZuordnungenListe.ItemsSource = null;
                return;
            }

            ErinnerungInfoText.Text = "Erinnerung-Id: " + aktuelleErinnerung.Id + "\nHash (SHA-256): " + aktuelleErinnerung.Hashwert;

            FundorteListe.ItemsSource = null;
            FundorteListe.ItemsSource = aktuelleErinnerung.Fundorte.Select(f => f.Pfad).ToList();

            ZuordnungenListe.ItemsSource = null;
            ZuordnungenListe.ItemsSource = zuordnungen.ToList();
        }
    }
}
