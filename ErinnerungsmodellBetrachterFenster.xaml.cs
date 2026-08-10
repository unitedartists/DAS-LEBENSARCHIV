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
    // ARBEITSMOTOR (09.08.) - erste zusammenhängende Bauphase
    // ============================================================
    // Rein lesend gegenüber personen.json (keine eigene Schreiblogik
    // dorthin) - Zuordnungen, Testimport und das Übernehmen in die
    // AM-Arbeitsauswahl laufen über Delegates, die in MainWindow.
    // ErinnerungsmodellZustand.cs leben. Dieses Fenster kennt weder
    // AM-Interna noch Datei-I/O-Details des Testimports - reine
    // Anzeige-/Auswahl-Logik.
    public partial class ErinnerungsmodellBetrachterFenster : Window
    {
        private readonly List<Erinnerung> erinnerungen;
        private readonly List<Zuordnung> zuordnungen;
        private readonly List<Zuordnung> zuordnungenPapierkorb;
        private readonly List<Person> personenSchreibtisch;
        private readonly List<Person> personenArchiv;
        private readonly List<Ereignis> freieEreignisse;
        private readonly List<Ereignis> freieEreignisseArchiv;
        private readonly List<Sammlung> sammlungen;
        private readonly List<Sammlung> sammlungenArchiv;
        private readonly Func<string, List<VisuellesMerkmal>> liesMerkmale;
        private readonly Action testimportDateiStarten;
        private readonly Action testimportOrdnerStarten;
        private readonly Func<string> sehzentrumBestandPruefen;
        private readonly Action<List<Guid>, ZuordnungsZielTyp, Guid> entferneAusZielInPapierkorb;
        private readonly Action<Zuordnung> stelleZuordnungWieder;
        private readonly Action<Zuordnung> loescheZuordnungEndgueltig;
        private readonly Action<List<Guid>> sendeSucheZurArbeitsmappe;
        private readonly Action<List<Guid>> sendeZielZurArbeitsmappe;

        public ErinnerungsmodellBetrachterFenster(
            List<Erinnerung> erinnerungen,
            List<Zuordnung> zuordnungen,
            List<Zuordnung> zuordnungenPapierkorb,
            List<Person> personenSchreibtisch,
            List<Person> personenArchiv,
            List<Ereignis> freieEreignisse,
            List<Ereignis> freieEreignisseArchiv,
            List<Sammlung> sammlungen,
            List<Sammlung> sammlungenArchiv,
            Func<string, List<VisuellesMerkmal>> liesMerkmale,
            Action testimportDateiStarten,
            Action testimportOrdnerStarten,
            Func<string> sehzentrumBestandPruefen,
            Action<List<Guid>, ZuordnungsZielTyp, Guid> entferneAusZielInPapierkorb,
            Action<Zuordnung> stelleZuordnungWieder,
            Action<Zuordnung> loescheZuordnungEndgueltig,
            Action<List<Guid>> sendeSucheZurArbeitsmappe,
            Action<List<Guid>> sendeZielZurArbeitsmappe)
        {
            InitializeComponent();

            this.erinnerungen = erinnerungen ?? new List<Erinnerung>();
            this.zuordnungen = zuordnungen ?? new List<Zuordnung>();
            this.zuordnungenPapierkorb = zuordnungenPapierkorb ?? new List<Zuordnung>();
            this.personenSchreibtisch = personenSchreibtisch ?? new List<Person>();
            this.personenArchiv = personenArchiv ?? new List<Person>();
            this.freieEreignisse = freieEreignisse ?? new List<Ereignis>();
            this.freieEreignisseArchiv = freieEreignisseArchiv ?? new List<Ereignis>();
            this.sammlungen = sammlungen ?? new List<Sammlung>();
            this.sammlungenArchiv = sammlungenArchiv ?? new List<Sammlung>();
            this.liesMerkmale = liesMerkmale;
            this.testimportDateiStarten = testimportDateiStarten;
            this.testimportOrdnerStarten = testimportOrdnerStarten;
            this.sehzentrumBestandPruefen = sehzentrumBestandPruefen;
            this.entferneAusZielInPapierkorb = entferneAusZielInPapierkorb;
            this.stelleZuordnungWieder = stelleZuordnungWieder;
            this.loescheZuordnungEndgueltig = loescheZuordnungEndgueltig;
            this.sendeSucheZurArbeitsmappe = sendeSucheZurArbeitsmappe;
            this.sendeZielZurArbeitsmappe = sendeZielZurArbeitsmappe;

            ZeigePapierkorb();

            // BUGFIX-MUSTER (09.08., siehe historischer Fehler): SelectedIndex
            // bewusst NICHT in der XAML, sondern erst hier gesetzt, nachdem
            // InitializeComponent() vollständig durchgelaufen ist - löst
            // danach automatisch die passenden Aktualisierungen über die
            // SelectionChanged-Handler aus (AktualisiereErgebnisListe bzw.
            // AktualisiereZielAuswahl+ZeigeZielErinnerungen).
            SortierungComboBox.SelectedIndex = 0;
            ZielTypComboBox.SelectedIndex = 0;
        }

        // ============================================================
        // SUCHE + SORTIERUNG + MARKIEREN (Arbeitsmotor-Kernstück)
        // ============================================================
        private void SucheOderSortierung_Changed(object sender, RoutedEventArgs e)
        {
            AktualisiereErgebnisListe();
        }

        private bool ErinnerungPasstZurSuche(Erinnerung erinnerung, string suchtext)
        {
            if (erinnerung.Fundorte != null && erinnerung.Fundorte.Any(f => (f.Pfad ?? "").ToLowerInvariant().Contains(suchtext)))
            {
                return true;
            }

            List<Zuordnung> eigeneZuordnungen = zuordnungen.Where(z => z.ErinnerungId == erinnerung.Id).ToList();

            if (eigeneZuordnungen.Any(z => !string.IsNullOrEmpty(z.ZielBezeichnung) && z.ZielBezeichnung.ToLowerInvariant().Contains(suchtext)))
            {
                return true;
            }

            // Sehzentrum-Merkmale (A's Punkt 8): eine Erinnerung, deren
            // Fundort-Dateiname James bereits bekannt ist, wird auch über
            // ihre gelernten Bildmerkmale gefunden (z.B. "Hund").
            if (liesMerkmale != null && erinnerung.Fundorte != null && erinnerung.Fundorte.Count > 0)
            {
                string dateiname = Path.GetFileName(erinnerung.Fundorte[0].Pfad);
                List<VisuellesMerkmal> merkmale = liesMerkmale(dateiname);

                if (merkmale != null && merkmale.Any(m => !string.IsNullOrEmpty(m.Bezeichnung) && m.Bezeichnung.ToLowerInvariant().Contains(suchtext)))
                {
                    return true;
                }
            }

            return false;
        }

        private List<Erinnerung> SucheUndSortiere()
        {
            string suchtext = (SucheTextBox.Text ?? "").Trim().ToLowerInvariant();

            IEnumerable<Erinnerung> treffer = erinnerungen;

            if (suchtext != "")
            {
                treffer = treffer.Where(er => ErinnerungPasstZurSuche(er, suchtext));
            }

            ComboBoxItem sortItem = SortierungComboBox.SelectedItem as ComboBoxItem;
            string sortText = sortItem != null ? sortItem.Content.ToString() : "Datum (neueste zuerst)";

            treffer = sortText.StartsWith("Alphabetisch")
                ? treffer.OrderBy(er => er.Fundorte.Count > 0 ? Path.GetFileName(er.Fundorte[0].Pfad) : "", StringComparer.OrdinalIgnoreCase)
                : treffer.OrderByDescending(er => er.Erstellungsdatum ?? er.CreatedAt);

            return treffer.ToList();
        }

        private void AktualisiereErgebnisListe()
        {
            if (ErgebnisListe == null)
            {
                return;
            }

            List<Erinnerung> treffer = SucheUndSortiere();

            ErgebnisText.Text = treffer.Count + " von " + erinnerungen.Count + " Erinnerung(en) gefunden:";

            ErgebnisListe.Items.Clear();

            foreach (Erinnerung erinnerung in treffer)
            {
                List<Zuordnung> eigeneZuordnungen = zuordnungen.Where(z => z.ErinnerungId == erinnerung.Id).ToList();

                string zuordnungsText = eigeneZuordnungen.Count == 0
                    ? "(keine Zuordnung)"
                    : string.Join(", ", eigeneZuordnungen.Select(z => z.ZielTyp + ": " + z.ZielBezeichnung));

                string dateiname = erinnerung.Fundorte.Count > 0 ? Path.GetFileName(erinnerung.Fundorte[0].Pfad) : erinnerung.Id.ToString();

                ErgebnisListe.Items.Add(ErstelleKachel(erinnerung, dateiname + "\n" + zuordnungsText));
            }

            AusgewaehlteZurAmButton.IsEnabled = false;
        }

        private void ErgebnisListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AusgewaehlteZurAmButton.IsEnabled = ErgebnisListe.SelectedItems.Count > 0;
        }

        // Übergibt die markierten Erinnerungen an die AM-Arbeitsauswahl -
        // dieselbe zentrale Logik wie Weg A/B/C (siehe MainWindow.
        // ErinnerungsmodellZustand.cs), hier mit Herkunft "Suche".
        private void AusgewaehlteZurAm_Click(object sender, RoutedEventArgs e)
        {
            List<Guid> ausgewaehlteIds = ErgebnisListe.SelectedItems
                .Cast<Border>()
                .Select(b => b.Tag as Erinnerung)
                .Where(er => er != null)
                .Select(er => er.Id)
                .ToList();

            if (ausgewaehlteIds.Count == 0)
            {
                return;
            }

            sendeSucheZurArbeitsmappe?.Invoke(ausgewaehlteIds);

            Close();
        }

        // ============================================================
        // TESTIMPORT (A's Punkt 7) - Ordnerauswahl/Vorschau/Bestätigung
        // liegen in MainWindow.ErinnerungsmodellZustand.cs (dort auch
        // der Datei-I/O-Zugriff); dieses Fenster stößt nur an und
        // aktualisiert danach seine eigene Anzeige.
        // ============================================================
        private void TestimportDatei_Click(object sender, RoutedEventArgs e)
        {
            testimportDateiStarten?.Invoke();

            // BUGFIX (10.08., Sanierungsplan Punkt 5): Suchfeld nach dem
            // Import leeren - sonst kann ein aktiver Suchfilter eine gerade
            // importierte Erinnerung unsichtbar machen.
            SucheTextBox.Text = "";

            AktualisiereErgebnisListe();
        }

        private void TestimportOrdner_Click(object sender, RoutedEventArgs e)
        {
            testimportOrdnerStarten?.Invoke();

            SucheTextBox.Text = "";

            AktualisiereErgebnisListe();
        }

        // ============================================================
        // GEMEINSAME KACHEL-DARSTELLUNG
        // ============================================================
        private static Border ErstelleKachel(Erinnerung erinnerung, string beschriftung)
        {
            string pfad = erinnerung.Fundorte != null && erinnerung.Fundorte.Count > 0 ? erinnerung.Fundorte[0].Pfad : null;

            Border rahmen = new Border
            {
                Width = 155,
                Height = 70,
                Margin = new Thickness(3),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Background = Brushes.WhiteSmoke,
                Tag = erinnerung
            };

            StackPanel inhalt = new StackPanel { Orientation = Orientation.Horizontal };

            Border bildRahmen = new Border { Width = 54, Height = 54, Margin = new Thickness(3), Background = Brushes.White };

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
                    bildRahmen.Child = ErstelleTypSymbol(erinnerung.MedienTyp);
                }
            }
            else
            {
                bildRahmen.Child = ErstelleTypSymbol(erinnerung.MedienTyp);
            }

            inhalt.Children.Add(bildRahmen);
            inhalt.Children.Add(new TextBlock { Text = beschriftung, TextWrapping = TextWrapping.Wrap, MaxWidth = 90, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 });

            rahmen.Child = inhalt;

            return rahmen;
        }

        private static TextBlock ErstelleTypSymbol(MedienTyp typ)
        {
            string symbol = typ switch
            {
                MedienTyp.Pdf => "📄",
                MedienTyp.Dokument => "📝",
                MedienTyp.Video => "🎬",
                _ => "🖼️"
            };

            return new TextBlock { Text = symbol, FontSize = 22, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        }

        // ============================================================
        // WEG C: ZIEL -> AM (unverändert aus der vorigen Bauphase)
        // ============================================================
        private void AktualisiereZielAuswahl()
        {
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

        private ZuordnungsZielTyp aktuellerZielTyp;
        private Guid? aktuelleZielId;

        private void ZeigeZielErinnerungen()
        {
            if (ZielErinnerungenListe == null)
            {
                return;
            }

            ZielErinnerungenListe.Items.Clear();
            ZurAmSchickenButton.IsEnabled = false;
            AusZielEntfernenButton.IsEnabled = false;

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
                aktuelleZielId = null;
                return;
            }

            aktuellerZielTyp = typText == "Ereignis" ? ZuordnungsZielTyp.Ereignis
                : typText == "Sammlung" ? ZuordnungsZielTyp.Sammlung
                : ZuordnungsZielTyp.Person;
            aktuelleZielId = zielId;

            List<Guid> erinnerungIds = zuordnungen
                .Where(z => z.ZielTyp == aktuellerZielTyp && z.ZielId == zielId.Value)
                .Select(z => z.ErinnerungId)
                .Distinct()
                .ToList();

            List<Erinnerung> passendeErinnerungen = erinnerungen.Where(er => erinnerungIds.Contains(er.Id)).ToList();

            foreach (Erinnerung erinnerung in passendeErinnerungen)
            {
                string dateiname = erinnerung.Fundorte.Count > 0 ? Path.GetFileName(erinnerung.Fundorte[0].Pfad) : erinnerung.Id.ToString();
                ZielErinnerungenListe.Items.Add(ErstelleKachel(erinnerung, dateiname));
            }
        }

        private void ZielErinnerungenListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int anzahl = ZielErinnerungenListe.SelectedItems.Count;
            ZurAmSchickenButton.IsEnabled = anzahl > 0;
            AusZielEntfernenButton.IsEnabled = anzahl > 0;
        }

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

            sendeZielZurArbeitsmappe?.Invoke(ausgewaehlteIds);

            Close();
        }

        // ============================================================
        // ZUORDNUNGS-PAPIERKORB (10.08., A/Opa-Integrationsauftrag Punkt 12+13)
        // ============================================================
        // "Aus diesem Ziel entfernen" verschiebt NUR die Zuordnung zum
        // aktuell angezeigten Ziel in den Papierkorb - die Erinnerung
        // selbst, ihre Fundorte und alle anderen Zuordnungen bleiben
        // unangetastet (Papierkorb-Kontext-Regel, wie beim alten Modell).
        private void AusZielEntfernen_Click(object sender, RoutedEventArgs e)
        {
            if (aktuelleZielId == null)
            {
                return;
            }

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

            bool ergebnis = James.FrageJaNein(
                ausgewaehlteIds.Count + " markierte Erinnerung(en) aus diesem Ziel entfernen?\n\n" +
                "Die Erinnerung(en) selbst und alle anderen Zuordnungen bleiben bestehen - die Zuordnung landet im Zuordnungs-Papierkorb und kann von dort wiederhergestellt werden.",
                James.TitelEntscheidung);

            if (!ergebnis)
            {
                return;
            }

            entferneAusZielInPapierkorb?.Invoke(ausgewaehlteIds, aktuellerZielTyp, aktuelleZielId.Value);

            ZeigeZielErinnerungen();
            ZeigePapierkorb();
        }

        private void ZeigePapierkorb()
        {
            if (PapierkorbListe == null)
            {
                return;
            }

            PapierkorbText.Text = zuordnungenPapierkorb.Count == 0
                ? "Der Zuordnungs-Papierkorb ist leer."
                : zuordnungenPapierkorb.Count + " entfernte Zuordnung(en):";

            PapierkorbListe.Items.Clear();

            foreach (Zuordnung zuordnung in zuordnungenPapierkorb)
            {
                Erinnerung erinnerung = erinnerungen.FirstOrDefault(er => er.Id == zuordnung.ErinnerungId);

                if (erinnerung == null)
                {
                    continue;
                }

                string beschriftung = (erinnerung.Fundorte.Count > 0 ? Path.GetFileName(erinnerung.Fundorte[0].Pfad) : erinnerung.Id.ToString())
                    + "\nwar: " + zuordnung.ZielTyp + ": " + zuordnung.ZielBezeichnung;

                Border kachel = ErstelleKachel(erinnerung, beschriftung);
                kachel.Tag = zuordnung;
                PapierkorbListe.Items.Add(kachel);
            }

            PapierkorbWiederherstellenButton.IsEnabled = false;
            PapierkorbEndgueltigLoeschenButton.IsEnabled = false;
        }

        private void PapierkorbListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int anzahl = PapierkorbListe.SelectedItems.Count;
            PapierkorbWiederherstellenButton.IsEnabled = anzahl > 0;
            PapierkorbEndgueltigLoeschenButton.IsEnabled = anzahl > 0;
        }

        private void PapierkorbWiederherstellen_Click(object sender, RoutedEventArgs e)
        {
            List<Zuordnung> ausgewaehlt = PapierkorbListe.SelectedItems.Cast<Border>().Select(b => b.Tag as Zuordnung).Where(z => z != null).ToList();

            foreach (Zuordnung zuordnung in ausgewaehlt)
            {
                stelleZuordnungWieder?.Invoke(zuordnung);
            }

            ZeigePapierkorb();
            ZeigeZielErinnerungen();
        }

        private void PapierkorbEndgueltigLoeschen_Click(object sender, RoutedEventArgs e)
        {
            List<Zuordnung> ausgewaehlt = PapierkorbListe.SelectedItems.Cast<Border>().Select(b => b.Tag as Zuordnung).Where(z => z != null).ToList();

            if (ausgewaehlt.Count == 0)
            {
                return;
            }

            bool ergebnis = James.FrageJaNein(
                ausgewaehlt.Count + " Zuordnung(en) endgültig aus dem Papierkorb entfernen?\n\n" +
                "Das betrifft ausschließlich diese Zuordnungs-Datensätze - die Erinnerungen selbst und ihre physischen Dateien bleiben davon vollständig unberührt.",
                James.TitelEndgueltigeEntscheidung);

            if (!ergebnis)
            {
                return;
            }

            foreach (Zuordnung zuordnung in ausgewaehlt)
            {
                loescheZuordnungEndgueltig?.Invoke(zuordnung);
            }

            ZeigePapierkorb();
        }

        // ============================================================
        // SEHZENTRUM-DATENBESTAND-DIAGNOSE (10.08., Sanierungsplan Punkt 4)
        // ============================================================
        private void SehzentrumPruefen_Click(object sender, RoutedEventArgs e)
        {
            string ergebnis = sehzentrumBestandPruefen?.Invoke() ?? "Diagnose nicht verfügbar.";
            James.Hinweis(ergebnis, "Sehzentrum-Datenbestand");
        }
    }
}
