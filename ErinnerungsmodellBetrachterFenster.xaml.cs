using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DAS_LEBENSARCHIV
{
    // ============================================================
    // SANIERUNG - BETRACHTER + WEG C (09.08.)
    // ============================================================
    // Rein lesend gegenüber erinnerungsmodell.json (keine eigene
    // Schreiblogik) - das eigentliche Übernehmen in die AM-
    // Arbeitsauswahl erfolgt über den übergebenen Delegate, der in
    // MainWindow.ErinnerungsmodellZustand.cs lebt (kein Zugriff dieses
    // Fensters auf MainWindow-Interna nötig).
    public partial class ErinnerungsmodellBetrachterFenster : Window
    {
        private readonly List<Erinnerung> erinnerungen;
        private readonly List<Zuordnung> zuordnungen;
        private readonly List<Person> personenSchreibtisch;
        private readonly List<Person> personenArchiv;
        private readonly List<Ereignis> freieEreignisse;
        private readonly List<Ereignis> freieEreignisseArchiv;
        private readonly List<Sammlung> sammlungen;
        private readonly List<Sammlung> sammlungenArchiv;
        private readonly Action<List<Guid>> sendeZurArbeitsmappe;

        public ErinnerungsmodellBetrachterFenster(
            List<Erinnerung> erinnerungen,
            List<Zuordnung> zuordnungen,
            List<Person> personenSchreibtisch,
            List<Person> personenArchiv,
            List<Ereignis> freieEreignisse,
            List<Ereignis> freieEreignisseArchiv,
            List<Sammlung> sammlungen,
            List<Sammlung> sammlungenArchiv,
            Action<List<Guid>> sendeZurArbeitsmappe)
        {
            InitializeComponent();

            this.erinnerungen = erinnerungen ?? new List<Erinnerung>();
            this.zuordnungen = zuordnungen ?? new List<Zuordnung>();
            this.personenSchreibtisch = personenSchreibtisch ?? new List<Person>();
            this.personenArchiv = personenArchiv ?? new List<Person>();
            this.freieEreignisse = freieEreignisse ?? new List<Ereignis>();
            this.freieEreignisseArchiv = freieEreignisseArchiv ?? new List<Ereignis>();
            this.sammlungen = sammlungen ?? new List<Sammlung>();
            this.sammlungenArchiv = sammlungenArchiv ?? new List<Sammlung>();
            this.sendeZurArbeitsmappe = sendeZurArbeitsmappe;

            ZeigeGesamtUebersicht();

            // BUGFIX (09.08.): SelectedIndex bewusst NICHT in der XAML gesetzt
            // (historischer Fehler, siehe Kommentar in ErinnerungenFenster.xaml,
            // Build 4.2) - dort hätte das SelectionChanged schon WÄHREND
            // InitializeComponent() ausgelöst, bevor ZielObjektComboBox
            // überhaupt existiert -> NullReferenceException. Jetzt wird die
            // Auswahl erst hier gesetzt, nachdem InitializeComponent()
            // vollständig durchgelaufen und alle Steuerelemente sicher
            // aufgebaut sind - das löst automatisch ZielAuswahl_Changed aus
            // und befüllt damit auch ZielObjektComboBox und ZielErinnerungenListe.
            ZielTypComboBox.SelectedIndex = 0;
        }

        // ============================================================
        // GESAMTÜBERSICHT (Kontroll-/Testwerkzeug, A's Punkt 1)
        // ============================================================
        private void ZeigeGesamtUebersicht()
        {
            GesamtUebersichtText.Text = erinnerungen.Count + " Erinnerung(en) im neuen Modell, " + zuordnungen.Count + " Zuordnung(en) insgesamt:";

            GesamtUebersichtListe.Items.Clear();

            foreach (Erinnerung erinnerung in erinnerungen)
            {
                List<Zuordnung> eigeneZuordnungen = zuordnungen.Where(z => z.ErinnerungId == erinnerung.Id).ToList();

                string zuordnungsText = eigeneZuordnungen.Count == 0
                    ? "(keine Zuordnung)"
                    : string.Join(", ", eigeneZuordnungen.Select(z => z.ZielTyp + ": " + z.ZielBezeichnung));

                string fundorteText = erinnerung.Fundorte.Count + " Fundort(e)";

                GesamtUebersichtListe.Items.Add(ErstelleUebersichtsZeile(erinnerung, zuordnungsText, fundorteText));
            }
        }

        private static Border ErstelleUebersichtsZeile(Erinnerung erinnerung, string zuordnungsText, string fundorteText)
        {
            string pfad = erinnerung.Fundorte.Count > 0 ? erinnerung.Fundorte[0].Pfad : null;

            Border rahmen = new Border
            {
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(4)
            };

            Grid inhalt = new Grid();
            inhalt.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            inhalt.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border bildRahmen = new Border { Width = 60, Height = 60, Background = Brushes.WhiteSmoke };

            if (!string.IsNullOrEmpty(pfad) && File.Exists(pfad))
            {
                try
                {
                    BitmapImage bild = new BitmapImage();
                    bild.BeginInit();
                    bild.CacheOption = BitmapCacheOption.OnLoad;
                    bild.DecodePixelWidth = 100;
                    bild.UriSource = new Uri(pfad);
                    bild.EndInit();

                    bildRahmen.Child = new Image { Source = bild, Stretch = Stretch.Uniform };
                }
                catch
                {
                    // Fundort nicht ladbar - Kachel bleibt einfach ohne Bild.
                }
            }

            Grid.SetColumn(bildRahmen, 0);
            inhalt.Children.Add(bildRahmen);

            StackPanel textSpalte = new StackPanel { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            textSpalte.Children.Add(new TextBlock { Text = pfad != null ? Path.GetFileName(pfad) : erinnerung.Id.ToString(), FontWeight = FontWeights.Bold });
            textSpalte.Children.Add(new TextBlock { Text = fundorteText, Foreground = Brushes.Gray, FontSize = 11 });
            textSpalte.Children.Add(new TextBlock { Text = zuordnungsText, TextWrapping = TextWrapping.Wrap });

            Grid.SetColumn(textSpalte, 1);
            inhalt.Children.Add(textSpalte);

            rahmen.Child = inhalt;

            return rahmen;
        }

        // ============================================================
        // WEG C: ZIEL -> AM
        // ============================================================
        private void AktualisiereZielAuswahl()
        {
            // Zusätzliche Absicherung (09.08., analog zum bewährten Muster in
            // MainWindow.ErinnerungsmodellZustand.cs): falls diese Methode
            // doch einmal aufgerufen würde, bevor beide Steuerelemente
            // existieren, bricht sie hier sauber ab statt abzustürzen.
            if (ZielTypComboBox == null || ZielObjektComboBox == null)
            {
                return;
            }

            ComboBoxItem ausgewaehlterTyp = ZielTypComboBox.SelectedItem as ComboBoxItem;
            string typText = ausgewaehlterTyp != null ? ausgewaehlterTyp.Content.ToString() : "Person";

            ZielObjektComboBox.ItemsSource = null;

            if (typText == "Ereignis")
            {
                ZielObjektComboBox.ItemsSource = freieEreignisse.Concat(freieEreignisseArchiv).ToList();
            }
            else if (typText == "Sammlung")
            {
                ZielObjektComboBox.ItemsSource = sammlungen.Concat(sammlungenArchiv).ToList();
            }
            else
            {
                ZielObjektComboBox.ItemsSource = personenSchreibtisch.Concat(personenArchiv).ToList();
            }

            if (ZielObjektComboBox.Items.Count > 0)
            {
                ZielObjektComboBox.SelectedIndex = 0;
            }
        }

        private void ZielAuswahl_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (ReferenceEquals(sender, ZielTypComboBox))
            {
                AktualisiereZielAuswahl();
            }

            ZeigeZielErinnerungen();
        }

        private void ZeigeZielErinnerungen()
        {
            if (ZielErinnerungenListe == null)
            {
                return;
            }

            ZielErinnerungenListe.Items.Clear();
            ZurAmSchickenButton.IsEnabled = false;

            ComboBoxItem ausgewaehlterTyp = ZielTypComboBox.SelectedItem as ComboBoxItem;
            string typText = ausgewaehlterTyp != null ? ausgewaehlterTyp.Content.ToString() : "Person";

            Guid? zielId = null;

            if (typText == "Ereignis" && ZielObjektComboBox.SelectedItem is Ereignis ereignis)
            {
                zielId = ereignis.Id;
            }
            else if (typText == "Sammlung" && ZielObjektComboBox.SelectedItem is Sammlung sammlung)
            {
                zielId = sammlung.Id;
            }
            else if (ZielObjektComboBox.SelectedItem is Person person)
            {
                zielId = person.Id;
            }

            if (zielId == null)
            {
                return;
            }

            ZuordnungsZielTyp zielTyp = typText == "Ereignis" ? ZuordnungsZielTyp.Ereignis
                : typText == "Sammlung" ? ZuordnungsZielTyp.Sammlung
                : ZuordnungsZielTyp.Person;

            List<Guid> erinnerungIds = zuordnungen
                .Where(z => z.ZielTyp == zielTyp && z.ZielId == zielId.Value)
                .Select(z => z.ErinnerungId)
                .Distinct()
                .ToList();

            List<Erinnerung> passendeErinnerungen = erinnerungen.Where(er => erinnerungIds.Contains(er.Id)).ToList();

            foreach (Erinnerung erinnerung in passendeErinnerungen)
            {
                string dateiname = erinnerung.Fundorte.Count > 0 ? Path.GetFileName(erinnerung.Fundorte[0].Pfad) : erinnerung.Id.ToString();
                ZielErinnerungenListe.Items.Add(ErstelleAuswahlKachel(erinnerung, dateiname));
            }
        }

        private static Border ErstelleAuswahlKachel(Erinnerung erinnerung, string beschriftung)
        {
            string pfad = erinnerung.Fundorte.Count > 0 ? erinnerung.Fundorte[0].Pfad : null;

            Border rahmen = new Border
            {
                Width = 190,
                Height = 70,
                Margin = new Thickness(4),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Background = Brushes.WhiteSmoke,
                Tag = erinnerung
            };

            StackPanel inhalt = new StackPanel { Orientation = Orientation.Horizontal };

            Border bildRahmen = new Border { Width = 60, Height = 60, Margin = new Thickness(4), Background = Brushes.White };

            if (!string.IsNullOrEmpty(pfad) && File.Exists(pfad))
            {
                try
                {
                    BitmapImage bild = new BitmapImage();
                    bild.BeginInit();
                    bild.CacheOption = BitmapCacheOption.OnLoad;
                    bild.DecodePixelWidth = 100;
                    bild.UriSource = new Uri(pfad);
                    bild.EndInit();

                    bildRahmen.Child = new Image { Source = bild, Stretch = Stretch.Uniform };
                }
                catch
                {
                    // Fundort nicht ladbar - Kachel bleibt einfach ohne Bild.
                }
            }

            inhalt.Children.Add(bildRahmen);
            inhalt.Children.Add(new TextBlock { Text = beschriftung, TextWrapping = TextWrapping.Wrap, MaxWidth = 110, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 });

            rahmen.Child = inhalt;

            return rahmen;
        }

        private void ZielErinnerungenListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ZurAmSchickenButton.IsEnabled = ZielErinnerungenListe.SelectedItems.Count > 0;
        }

        // Übernimmt die markierten Erinnerungen per Id in die AM-
        // Arbeitsauswahl (über den Delegate, der in MainWindow lebt) -
        // keine neue Erinnerung, keine physische Kopie, kein zweiter
        // Erinnerungsbestand.
        private void ZurAmSchicken_Click(object sender, RoutedEventArgs e)
        {
            List<Guid> ausgewaehlteIds = ZielErinnerungenListe.SelectedItems
                .Cast<Border>()
                .Select(b => b.Tag as Erinnerung)
                .Where(er => er != null)
                .Select(er => er.Id)
                .ToList();

            if (ausgewaehlteIds.Count == 0)
            {
                return;
            }

            sendeZurArbeitsmappe?.Invoke(ausgewaehlteIds);

            Close();
        }
    }
}
