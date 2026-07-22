using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DAS_LEBENSARCHIV
{
    // ============================================================
    // BUILD 3.0: JAMES BEGINNT ZU LERNEN (Etappe B.2)
    // ============================================================
    // James' erstes Gesetz gilt unverändert weiter (siehe
    // MainWindow.xaml.cs): James darf Erinnerungen ergänzen, aber
    // niemals selbstständig verändern oder überschreiben. Alle
    // Merkmale werden ausschließlich vom Benutzer eingegeben - noch
    // keine KI, keine automatischen Vorschläge.
    //
    // Bewusst unabhängig gehalten (wie ErinnerungenFenster): bekommt
    // die aktuelle Merkmal-Liste direkt übergeben (Referenztyp - jede
    // Ergänzung hier wirkt sich sofort auf dieselbe Liste beim
    // Aufrufer aus) sowie eine einzige kleine Speichern-Funktion, die
    // nach jeder Ergänzung aufgerufen wird.
    // ============================================================
    public partial class JamesLerntFenster : Window
    {
        // Build 3.0: Liste darf später erweitert werden (siehe
        // Auftrag des Architekten) - deshalb ganz bewusst eine
        // einfache Liste statt einer festen Aufzählung (enum).
        private static readonly string[] Kategorien =
        {
            "Person",
            "Tier",
            "Ort",
            "Gebäude",
            "Fahrzeug",
            "Pflanze",
            "Ereignis",
            "Gegenstand",
            "Sonstiges"
        };

        private readonly List<VisuellesMerkmal> merkmale;
        private readonly Action speichern;

        public JamesLerntFenster(List<VisuellesMerkmal> merkmale, Action speichern)
        {
            InitializeComponent();

            this.merkmale = merkmale ?? new List<VisuellesMerkmal>();
            this.speichern = speichern;

            KategorieComboBox.ItemsSource = Kategorien;
            KategorieComboBox.SelectedIndex = 0;

            RenderBekannteMerkmale();
        }

        private void RenderBekannteMerkmale()
        {
            BekannteMerkmalePanel.Children.Clear();

            foreach (VisuellesMerkmal merkmal in merkmale)
            {
                BekannteMerkmalePanel.Children.Add(new TextBlock
                {
                    Text = merkmal.Bezeichnung + " (" + merkmal.Kategorie + ")",
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }

            KeineMerkmaleText.Visibility = merkmale.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void NeuesMerkmalTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                FuegeMerkmalHinzu();
            }
        }

        private void Hinzufuegen_Click(object sender, RoutedEventArgs e)
        {
            FuegeMerkmalHinzu();
        }

        private void FuegeMerkmalHinzu()
        {
            string bezeichnung = NeuesMerkmalTextBox.Text.Trim();
            string kategorie = KategorieComboBox.SelectedItem as string;

            if (bezeichnung == "" || kategorie == null)
            {
                return;
            }

            // Keine doppelten Merkmale (Groß-/Kleinschreibung wird dabei
            // nicht unterschieden) - dieselbe Regel wie schon in Build 2.9.
            bool bereitsVorhanden = merkmale.Exists(m => string.Equals(m.Bezeichnung, bezeichnung, StringComparison.OrdinalIgnoreCase));

            if (!bereitsVorhanden)
            {
                merkmale.Add(new VisuellesMerkmal
                {
                    Bezeichnung = bezeichnung,
                    Kategorie = kategorie
                });

                speichern?.Invoke();
                RenderBekannteMerkmale();
            }

            NeuesMerkmalTextBox.Clear();
            NeuesMerkmalTextBox.Focus();
        }
    }
}
