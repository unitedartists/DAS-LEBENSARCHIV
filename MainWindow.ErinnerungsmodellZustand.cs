using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow
    {
        private List<Erinnerung> erinnerungsmodellErinnerungen;
        private List<Zuordnung> erinnerungsmodellZuordnungen;
        private List<Zuordnung> erinnerungsmodellZuordnungenPapierkorb;
        private bool erinnerungsmodellGeladen;

        // A/Opa-BAUAUFTRAG "AM: GESAMTUMBAU ZU EINEM ARBEITSBEREICH" (16.08.):
        // ersetzt die bisherige Zweiteilung "Direktsuchergebnis" (AmDirekte-
        // AuswahlListe) vs. "Arbeitsauswahl" (vormals amArbeitsauswahl, eine
        // eigene zweite Liste). Es gibt jetzt nur noch EIN gruenes Fenster
        // (AmDirekteAuswahlListe) und EIN Markierungs-Set. amMarkierteErinnerungIds
        // ist die alleinige Quelle der Wahrheit dafuer, was gerade "markiert"
        // ist - unabhaengig davon, ob die betroffene Erinnerung gerade sichtbar
        // ist (z.B. durch einen Suchbegriff herausgefiltert). Dadurch uebersteht
        // eine Markierung auch eine zwischenzeitliche Sucheingabe. Wird sowohl
        // von der AM-eigenen Ziel-Auswahl ("Neue Zuordnung anlegen"/"Markierte
        // in den Papierkorb") als auch von der rechten Aktionsleiste (Person/
        // Ereignis/Sammlung/Asservatenkammer) als "markiert" gelesen.
        private readonly HashSet<Guid> amMarkierteErinnerungIds = new HashSet<Guid>();

        // A/Opa-BAUAUFTRAG "JAMES-SUCHE KLARER UND OPA-FREUNDLICHER" (16.08.):
        // Vertrauens-Reihenfolge A-D, wie von A/Opa vorgegeben - A ist am
        // vertrauenswuerdigsten ("James hat es wirklich erkannt"), D am
        // schwaechsten (reiner Text-Zufallstreffer im Pfad).
        private enum SuchTrefferQuelle
        {
            SehzentrumBegriff,
            BestaetigtesStichwort,
            Zuordnungsname,
            Dateipfad
        }

        // Verhindert, dass das programmatische Wiederherstellen der Auswahl
        // (nach einem Listen-Neuaufbau) selbst wieder ein SelectionChanged
        // ausloest, das amMarkierteErinnerungIds faelschlich veraendern wuerde.
        private bool amUnterdrueckeAmSelectionEvent = false;

        private string ErinnerungsmodellDateiPfad => Path.Combine(OrdnerPfad, "erinnerungsmodell.json");

        private void LadeErinnerungsmodellFallsNoetig()
        {
            if (erinnerungsmodellGeladen)
            {
                return;
            }

            erinnerungsmodellErinnerungen = new List<Erinnerung>();
            erinnerungsmodellZuordnungen = new List<Zuordnung>();
            erinnerungsmodellZuordnungenPapierkorb = new List<Zuordnung>();

            try
            {
                if (File.Exists(ErinnerungsmodellDateiPfad))
                {
                    string json = File.ReadAllText(ErinnerungsmodellDateiPfad);
                    ArchivErinnerungsDaten daten = JsonSerializer.Deserialize<ArchivErinnerungsDaten>(json);

                    if (daten != null)
                    {
                        if (daten.Erinnerungen != null)
                        {
                            erinnerungsmodellErinnerungen = daten.Erinnerungen;
                        }

                        if (daten.Zuordnungen != null)
                        {
                            erinnerungsmodellZuordnungen = daten.Zuordnungen;
                        }

                        if (daten.ZuordnungenPapierkorb != null)
                        {
                            erinnerungsmodellZuordnungenPapierkorb = daten.ZuordnungenPapierkorb;
                        }
                    }
                }
            }
            catch
            {
            }

            erinnerungsmodellGeladen = true;
        }

        private List<Erinnerung> ErmittleErinnerungenFuerZiel(ZuordnungsZielTyp zielTyp, Guid zielId)
        {
            LadeErinnerungsmodellFallsNoetig();

            List<Guid> ids = erinnerungsmodellZuordnungen
                .Where(z => z.ZielTyp == zielTyp && z.ZielId == zielId)
                .Select(z => z.ErinnerungId)
                .Distinct()
                .ToList();

            return erinnerungsmodellErinnerungen.Where(er => ids.Contains(er.Id)).ToList();
        }

        private void ErgaenzeUmNeuesModell(List<ErinnerungsInfo> bestehend, ZuordnungsZielTyp zielTyp, Guid zielId)
        {
            HashSet<string> bekanntePfade = bestehend
                .Where(info => !string.IsNullOrEmpty(info.Pfad))
                .Select(info => info.Pfad.ToLowerInvariant())
                .ToHashSet();

            foreach (Erinnerung erinnerung in ErmittleErinnerungenFuerZiel(zielTyp, zielId))
            {
                string pfad = erinnerung.Fundorte?
                    .Select(f => f.Pfad)
                    .FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));

                if (pfad == null || bekanntePfade.Contains(pfad.ToLowerInvariant()))
                {
                    continue;
                }

                bestehend.Add(new ErinnerungsInfo
                {
                    Pfad = pfad,
                    Titel = "(neu zugeordnet)"
                });

                bekanntePfade.Add(pfad.ToLowerInvariant());
            }
        }

        // A/Opa-REPARATURAUFTRAG "AM TEST 3" (16.08.): TEST-3-BEFUND-URSACHE.
        // Die Kachel (ErstelleErinnerungsKachel) ist ein OPAKER Border mit
        // fester Groesse, der den gesamten ListBoxItem-Bereich ausfuellt.
        // WPFs eingebaute Markierungs-Hervorhebung wird dabei standardmaessig
        // HINTER dem Kachel-Inhalt gezeichnet - der opake Border deckt sie
        // vollstaendig zu, zusaetzlich verblasst WPF sie weiter sobald die
        // Liste den Fokus verliert (z.B. Klick in die Personen-Auswahlliste
        // rechts). Die Markierung ging dabei nie technisch verloren (die
        // Zuordnung erfolgte ja auch korrekt) - sie war schlicht nie sichtbar.
        // Fix: eigenes, deutliches optisches Markierungs-Zeichen direkt auf
        // der Kachel selbst (dicker gruener Rahmen + helle gruene Faerbung),
        // unabhaengig von WPFs eingebauter (hier unsichtbarer) Selektions-
        // Hervorhebung.
        private static void SetzeAmKachelMarkierungsOptik(Border kachel, bool istMarkiert)
        {
            if (istMarkiert)
            {
                kachel.BorderBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0x7D, 0x32));
                kachel.BorderThickness = new Thickness(4);
                kachel.Background = new SolidColorBrush(Color.FromRgb(0xDC, 0xF0, 0xD8));
            }
            else
            {
                kachel.BorderBrush = Brushes.LightGray;
                kachel.BorderThickness = new Thickness(1);
                kachel.Background = Brushes.WhiteSmoke;
            }
        }

        // A/Opa-OPTIMIERUNGSAUFTRAG "Opa-freundliches James" (16.08.), Teil C:
        // deutlich groessere, bild-dominante Kacheln (Bild oben, gross;
        // Beschriftung ergaenzend darunter, klein) statt der bisherigen
        // kleinen 54x54-Vorschau neben dem Text. Bewusst in dieser EINEN
        // gemeinsamen Methode geaendert statt einer AM-spezifischen zweiten
        // Kachel-Variante - kommt dadurch einheitlich AM, James-Suche und
        // dem Zuordnungs-Papierkorb zugute (Teil H: einheitliche Bedienung/
        // Darstellung). Die vorhandene WrapPanel-/Scroll-Logik bleibt
        // unveraendert - mehr Spalten/Reihen ergeben sich automatisch aus
        // der verfuegbaren Breite.
        private static Border ErstelleErinnerungsKachel(Erinnerung erinnerung, string beschriftung)
        {
            string pfad = erinnerung.Fundorte != null && erinnerung.Fundorte.Count > 0 ? erinnerung.Fundorte[0].Pfad : null;

            Border rahmen = new Border
            {
                Width = 210,
                Height = 210,
                Margin = new Thickness(5),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Background = Brushes.WhiteSmoke,
                Tag = erinnerung
            };

            StackPanel inhalt = new StackPanel { Orientation = Orientation.Vertical };

            Border bildRahmen = new Border
            {
                Width = 190,
                Height = 160,
                Margin = new Thickness(3),
                Background = Brushes.White
            };

            if (!string.IsNullOrEmpty(pfad) && File.Exists(pfad))
            {
                try
                {
                    BitmapImage bild = new BitmapImage();
                    bild.BeginInit();
                    bild.CacheOption = BitmapCacheOption.OnLoad;
                    bild.DecodePixelWidth = 380;
                    bild.UriSource = new Uri(pfad);
                    bild.EndInit();

                    bildRahmen.Child = new Image { Source = bild, Stretch = Stretch.Uniform };
                }
                catch
                {
                    bildRahmen.Child = new TextBlock { Text = "🖼️", FontSize = 40, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                }
            }
            else
            {
                bildRahmen.Child = new TextBlock { Text = "⚠️", FontSize = 32, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, ToolTip = "Fundort nicht gefunden" };
            }

            inhalt.Children.Add(bildRahmen);

            inhalt.Children.Add(new TextBlock
            {
                Text = beschriftung,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 195,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0),
                FontSize = 11
            });

            rahmen.Child = inhalt;

            return rahmen;
        }

        // A/Opa-BAUAUFTRAG "AM: GESAMTUMBAU" (16.08.): landet nicht mehr in
        // einer eigenen zweiten Liste, sondern markiert die betroffenen
        // Erinnerungen direkt im EINEN gruenen Fenster (amMarkierteErinnerungIds)
        // und wechselt dorthin. Der Suchtext wird geleert, damit die
        // geschickten Erinnerungen garantiert sichtbar sind (nicht durch
        // einen stehengebliebenen Suchbegriff herausgefiltert werden).
        private void SendeMarkierteZurArbeitsmappe(List<string> pfade)
        {
            LadeErinnerungsmodellFallsNoetig();

            int gefunden = 0;
            int nichtGefunden = 0;

            foreach (string pfad in pfade)
            {
                Erinnerung erinnerung = erinnerungsmodellErinnerungen.FirstOrDefault(er =>
                    er.Fundorte != null && er.Fundorte.Any(f => string.Equals(f.Pfad, pfad, StringComparison.OrdinalIgnoreCase)));

                if (erinnerung == null)
                {
                    nichtGefunden++;
                    continue;
                }

                amMarkierteErinnerungIds.Add(erinnerung.Id);
                gefunden++;
            }

            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;

            // A/Opa-UMBAU "AM GRUNDLEGEND VEREINFACHEN" (17.08.), §7: eine
            // Erinnerung "ausgelegt" zu bekommen beendet den "leer"-Zustand.
            amArbeitstischGeleert = false;

            if (AmDirekteSucheTextBox != null)
            {
                AmDirekteSucheTextBox.Text = "";
            }

            AktualisiereAmDirekteAuswahlListe();

            if (nichtGefunden > 0)
            {
                James.Hinweis(gefunden + " Erinnerung(en) wurden markiert. " + nichtGefunden +
                    " Erinnerung(en) sind noch nicht Teil des neuen Modells (noch nicht migriert) und konnten deshalb nicht markiert werden.");
            }
        }

        private void AmDirekteSucheTextBox_TextChanged(object sender, RoutedEventArgs e)
        {
            AktualisiereAmDirekteAuswahlListe();
        }

        // A/Opa-OPTIMIERUNGSAUFTRAG "Opa-freundliches James" (16.08.), Teil G:
        // baut eine Nachschlagetabelle Dateiname -> alte Stichwoerter
        // (Ereignis.Stichwoerter, ALTES Modell) - REIN LESEND, keine
        // Migration, keine Aenderung am alten Modell. Die Verknuepfung zur
        // Erinnerung laeuft ueber den Dateinamen (das neue Modell speichert
        // volle Pfade, das alte nur Dateinamen an Person/Ereignis) - das ist
        // die einzige verfuegbare Bruecke, aber KEINE eindeutige ID-
        // Verknuepfung: zwei verschiedene Erinnerungen mit zufaellig
        // gleichem Dateinamen (z.B. IMG_0001.jpg aus zwei Quellen) wuerden
        // sich hier ununterscheidbar teilen. Bewusst nicht "verbessert" -
        // siehe Abschlussbericht, A wollte in diesem Fall lieber eine
        // Meldung als eine improvisierte Loesung.
        private Dictionary<string, List<string>> ErmittleAlteStichwoerterProDateiname()
        {
            Dictionary<string, List<string>> ergebnis = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            void ErfasseEreignis(Ereignis ereignis)
            {
                if (ereignis?.Stichwoerter == null || ereignis.Stichwoerter.Count == 0)
                {
                    return;
                }

                List<string> dateinamen = new List<string>();

                if (!string.IsNullOrEmpty(ereignis.EreignisFotoDateiname))
                {
                    dateinamen.Add(ereignis.EreignisFotoDateiname);
                }

                if (ereignis.WeitereFotoDateinamen != null)
                {
                    dateinamen.AddRange(ereignis.WeitereFotoDateinamen);
                }

                foreach (string dateiname in dateinamen)
                {
                    if (string.IsNullOrEmpty(dateiname))
                    {
                        continue;
                    }

                    if (!ergebnis.TryGetValue(dateiname, out List<string> liste))
                    {
                        liste = new List<string>();
                        ergebnis[dateiname] = liste;
                    }

                    liste.AddRange(ereignis.Stichwoerter);
                }
            }

            foreach (Person person in allePersonen)
            {
                if (person.Ereignisse != null)
                {
                    foreach (Ereignis ereignis in person.Ereignisse)
                    {
                        ErfasseEreignis(ereignis);
                    }
                }
            }

            foreach (object element in ArchivListe.Items)
            {
                Person person = element as Person;

                if (person?.Ereignisse != null)
                {
                    foreach (Ereignis ereignis in person.Ereignisse)
                    {
                        ErfasseEreignis(ereignis);
                    }
                }
            }

            foreach (Ereignis ereignis in freieEreignisse)
            {
                ErfasseEreignis(ereignis);
            }

            foreach (Ereignis ereignis in freieEreignisseArchiv)
            {
                ErfasseEreignis(ereignis);
            }

            return ergebnis;
        }

        // A/Opa-ENTSCHEIDUNG (11.08.): "Zwei Karteikästen"-Befund - James
        // lernte im Sehzentrum (hash-basiert, sehgedaechtnis.json), die
        // Suche fragte bisher aber das aeltere, dateiname-basierte
        // Erinnerungsgedaechtnis (LiesVisuelleMerkmale) ab, das davon nie
        // etwas erfuhr. A's Entscheidung: Sehzentrum wird die EINZIGE
        // Quelle fuer Bildwissen in der Suche. Das alte Erinnerungs-
        // gedaechtnis bleibt unveraendert liegen (kein Loeschen, keine
        // Migration jetzt) - wird hier schlicht nicht mehr angefragt, um
        // keine zwei parallelen Wissenssysteme gleichzeitig zu durchsuchen.
        // Verknuepfung ueber den Hashwert - denselben Schluessel, den auch
        // SehgedaechtnisEintrag selbst verwendet.
        //
        // A/Opa-OPTIMIERUNGSAUFTRAG (16.08.), Teil G: zusaetzlich alte
        // Ereignis.Stichwoerter durchsucht (siehe alteStichwoerter-Parameter,
        // rein lesend, ueber Dateiname verknuepft).
        // A/Opa-BAUAUFTRAG "JAMES-SUCHE KLARER UND OPA-FREUNDLICHER" (16.08.):
        // ersetzt ErinnerungPasstZurZentralenSuche (reines Ja/Nein). Liefert
        // jetzt zusaetzlich WARUM eine Erinnerung gefunden wurde, in der von
        // A/Opa vorgegebenen Vertrauens-Reihenfolge A-D. Prueft in dieser
        // Reihenfolge und liefert die erste (vertrauenswuerdigste)
        // zutreffende Quelle - bei einem Treffer ueber mehrere Quellen
        // gleichzeitig (z.B. Sehzentrum UND Dateipfad) wird bewusst nur die
        // vertrauenswuerdigste angezeigt, damit James nie den Eindruck
        // erweckt, ein Bild "erkannt" zu haben, wenn der Treffer eigentlich
        // nur ueber einen Ordnernamen zustande kam.
        private SuchTrefferQuelle? ErmittleSuchTrefferQuelle(Erinnerung erinnerung, string suchtext, List<SehgedaechtnisEintrag> sehgedaechtnis, Dictionary<string, List<string>> alteStichwoerter)
        {
            // A) Sehzentrum-Begriff: "James hat diesen Begriff für das Bild gelernt."
            if (!string.IsNullOrEmpty(erinnerung.Hashwert))
            {
                SehgedaechtnisEintrag eintrag = sehgedaechtnis.FirstOrDefault(s => s.Hashwert == erinnerung.Hashwert);

                if (eintrag != null && eintrag.BestaetigteStichwoerter != null
                    && eintrag.BestaetigteStichwoerter.Any(b => (b ?? "").ToLowerInvariant().Contains(suchtext)))
                {
                    return SuchTrefferQuelle.SehzentrumBegriff;
                }
            }

            // B) bestätigtes/übernommenes Stichwort (altes Modell): "Begriff wurde ausdrücklich gespeichert."
            string dateiname = erinnerung.Fundorte != null && erinnerung.Fundorte.Count > 0
                ? Path.GetFileName(erinnerung.Fundorte[0].Pfad)
                : null;

            if (!string.IsNullOrEmpty(dateiname) && alteStichwoerter.TryGetValue(dateiname, out List<string> stichwoerterListe)
                && stichwoerterListe.Any(s => (s ?? "").ToLowerInvariant().Contains(suchtext)))
            {
                return SuchTrefferQuelle.BestaetigtesStichwort;
            }

            // C) Zuordnungsname: Person/Ereignis/Sammlung enthält den Suchbegriff.
            if (erinnerungsmodellZuordnungen
                .Where(z => z.ErinnerungId == erinnerung.Id)
                .Any(z => !string.IsNullOrEmpty(z.ZielBezeichnung) && z.ZielBezeichnung.ToLowerInvariant().Contains(suchtext)))
            {
                return SuchTrefferQuelle.Zuordnungsname;
            }

            // D) Dateipfad/Dateiname: Suchbegriff kommt dort vor.
            if (erinnerung.Fundorte != null && erinnerung.Fundorte.Any(f => (f.Pfad ?? "").ToLowerInvariant().Contains(suchtext)))
            {
                return SuchTrefferQuelle.Dateipfad;
            }

            return null;
        }

        private static string BeschriftungFuerTrefferQuelle(SuchTrefferQuelle quelle)
        {
            switch (quelle)
            {
                case SuchTrefferQuelle.SehzentrumBegriff: return "👁️ von James erkannt";
                case SuchTrefferQuelle.BestaetigtesStichwort: return "🏷️ gespeichertes Stichwort";
                case SuchTrefferQuelle.Zuordnungsname: return "🔗 über Zuordnung gefunden";
                default: return "📁 über Ordner-/Dateiname gefunden";
            }
        }

        // A/Opa-BAUAUFTRAG "JAMES-EINZUG" (12.08.), Punkt 9: Signatur auf
        // das zentrale SortierModus-Enum umgestellt statt eines einfachen
        // Ja/Nein - deckt jetzt alle 4 geforderten Sortierungen ab, in
        // genau dieser einen Methode, keine zweite Sortierlogik irgendwo
        // sonst. AM, Arbeitsmotor und die neue James-Suche rufen alle
        // dieselbe Methode auf.
        //
        // A/Opa-BAUAUFTRAG "JAMES-SUCHE KLARER" (16.08.): diese Ueberladung
        // bleibt UNVERAENDERT in Signatur und Rueckgabewert bestehen - sie
        // wird als Delegate an den Arbeitsmotor uebergeben (OeffneArbeitsmotor,
        // Zeile mit "ZentraleErinnerungsSuche," als Methodengruppe), eine
        // Signaturaenderung dort haette dessen Konstruktoraufruf gebrochen.
        // Reicht intern einfach an die neue, dritte Ueberladung weiter.
        private List<Erinnerung> ZentraleErinnerungsSuche(string suchtext, SortierModus sortierung)
        {
            return ZentraleErinnerungsSuche(suchtext, sortierung, out _);
        }

        // A/Opa-BAUAUFTRAG "JAMES-SUCHE KLARER" (16.08.): neue Ueberladung
        // ausschliesslich fuer die AM - liefert zusaetzlich zur Trefferliste,
        // WARUM jede gefundene Erinnerung getroffen hat (trefferQuellen).
        // Jede Erinnerung wird hier genau EINMAL geprueft (eine Schleife
        // ueber erinnerungsmodellErinnerungen, kein Join/keine
        // Vervielfachung) - dadurch kann sie unabhaengig davon, ueber wie
        // viele Quellen sie passt, auch nur genau EINMAL in trefferQuellen/
        // treffer (und damit in der AM als genau eine Kachel) landen.
        private List<Erinnerung> ZentraleErinnerungsSuche(string suchtext, SortierModus sortierung, out Dictionary<Guid, SuchTrefferQuelle> trefferQuellen)
        {
            LadeErinnerungsmodellFallsNoetig();

            string normalisiert = (suchtext ?? "").Trim().ToLowerInvariant();

            trefferQuellen = new Dictionary<Guid, SuchTrefferQuelle>();

            List<Erinnerung> treffer;

            if (normalisiert == "")
            {
                treffer = erinnerungsmodellErinnerungen.ToList();
            }
            else
            {
                List<SehgedaechtnisEintrag> sehgedaechtnis = LadeSehgedaechtnis();
                Dictionary<string, List<string>> alteStichwoerter = ErmittleAlteStichwoerterProDateiname();

                treffer = new List<Erinnerung>();

                foreach (Erinnerung erinnerung in erinnerungsmodellErinnerungen)
                {
                    SuchTrefferQuelle? quelle = ErmittleSuchTrefferQuelle(erinnerung, normalisiert, sehgedaechtnis, alteStichwoerter);

                    if (quelle.HasValue)
                    {
                        treffer.Add(erinnerung);
                        trefferQuellen[erinnerung.Id] = quelle.Value;
                    }
                }
            }

            switch (sortierung)
            {
                case SortierModus.AlphabetischAufsteigend:
                    return treffer.OrderBy(er => er.Fundorte.Count > 0 ? Path.GetFileName(er.Fundorte[0].Pfad) : "", StringComparer.OrdinalIgnoreCase).ToList();

                case SortierModus.AlphabetischAbsteigend:
                    return treffer.OrderByDescending(er => er.Fundorte.Count > 0 ? Path.GetFileName(er.Fundorte[0].Pfad) : "", StringComparer.OrdinalIgnoreCase).ToList();

                case SortierModus.DatumAeltesteZuerst:
                    // A/Opa-REPARATURAUFTRAG (11.08.), PROBLEM 1: echter
                    // Importzeitpunkt vorrangig, Fallback fuer Altbestand
                    // ohne ImportiertAm bleibt bestehen.
                    return treffer.OrderBy(er => er.ImportiertAm ?? er.Erstellungsdatum ?? er.CreatedAt).ToList();

                default:
                    return treffer.OrderByDescending(er => er.ImportiertAm ?? er.Erstellungsdatum ?? er.CreatedAt).ToList();
            }
        }

        // A/Opa-UMBAU "AM GRUNDLEGEND VEREINFACHEN" (17.08.), §7: "Arbeitsmappe
        // leeren" ist ein echter, eigener Anzeige-Zustand - unabhaengig vom
        // Suchtext (der bei leerem Text normalerweise den GESAMTEN Bestand
        // zeigt). Bleibt gesetzt, bis explizit wieder gesucht/importiert/
        // ausgelegt wird (Reset-Stellen: UebergibSucheAnArbeitsmappe,
        // AmTestimportDatei_Click, AmTestimportOrdner_Click, SendeMarkierte-
        // ZurArbeitsmappe, SendeErinnerungsIdsZurArbeitsmappe). Aendert nur
        // die ANZEIGE - keine Datei, Zuordnung, Papierkorb oder AK betroffen.
        private bool amArbeitstischGeleert = false;

        private void AktualisiereAmDirekteAuswahlListe()
        {
            LadeErinnerungsmodellFallsNoetig();

            if (AmDirekteSucheTextBox == null || AmDirekteAuswahlListe == null)
            {
                return;
            }

            if (amArbeitstischGeleert)
            {
                AmDirekteAuswahlListe.Items.Clear();

                if (AmDirekteTrefferAnzahlText != null)
                {
                    AmDirekteTrefferAnzahlText.Text = "Die Arbeitsmappe ist leer - bitte suchen, importieren oder eine Erinnerung hierher schicken.";
                }

                AktualisiereAmBottomLeiste(0);
                AktualisiereAmMarkierungsAbhaengigeAnzeige();
                AktualisiereAmPapierkorbAnzeige();
                return;
            }

            string suchtext = AmDirekteSucheTextBox.Text ?? "";

            ComboBoxItem sortItem = AmSortierungComboBox?.SelectedItem as ComboBoxItem;
            bool alphabetisch = sortItem != null && sortItem.Content.ToString().StartsWith("Alphabetisch");
            SortierModus sortierung = alphabetisch ? SortierModus.AlphabetischAufsteigend : SortierModus.DatumNeuesteZuerst;

            List<Erinnerung> treffer = ZentraleErinnerungsSuche(suchtext, sortierung, out Dictionary<Guid, SuchTrefferQuelle> trefferQuellen).ToList();

            // A/Opa-REPARATURAUFTRAG "AM-PAPIERKORB = VOM ARBEITSTISCH
            // WEGLEGEN" (17.08.): Erinnerungen, die Opa aus der AM in den
            // (neuen, eigenen) AM-Papierkorb gelegt hat, werden hier aus
            // JEDER Ansicht der AM ausgeblendet - unabhaengig vom Suchtext.
            // Nichts an den Daten selbst (Zuordnungen/Datei) ist dabei
            // veraendert, nur die Sichtbarkeit in der AM.
            LadeAmPapierkorbFallsNoetig();
            treffer = treffer.Where(er => !amPapierkorbErinnerungIds.Contains(er.Id)).ToList();

            AmDirekteAuswahlListe.Items.Clear();

            foreach (Erinnerung erinnerung in treffer)
            {
                string dateiname = erinnerung.Fundorte.Count > 0 ? Path.GetFileName(erinnerung.Fundorte[0].Pfad) : erinnerung.Id.ToString();

                // A/Opa-BAUAUFTRAG "JAMES-SUCHE KLARER" (16.08.), Punkt 2:
                // zeigt fuer jeden Suchtreffer sichtbar, WARUM er gefunden
                // wurde - nur wenn tatsaechlich eine Suche laeuft (leerer
                // Suchtext = gesamter Bestand, da hat "gefunden wegen" keine
                // Bedeutung).
                string beschriftung = trefferQuellen.TryGetValue(erinnerung.Id, out SuchTrefferQuelle quelle)
                    ? dateiname + "\n" + BeschriftungFuerTrefferQuelle(quelle)
                    : dateiname;

                AmDirekteAuswahlListe.Items.Add(ErstelleErinnerungsKachel(erinnerung, beschriftung));
            }

            // A/Opa-BAUAUFTRAG "AM: GESAMTUMBAU" (16.08.): die Markierung
            // (amMarkierteErinnerungIds) ist unabhaengig vom aktuellen
            // Suchtext und uebersteht damit Sucheingaben. Nach jedem
            // Neuaufbau der Liste wird sie hier als ListBox-Auswahl wieder
            // sichtbar gemacht. amUnterdrueckeAmSelectionEvent verhindert,
            // dass das dabei ausgeloeste SelectionChanged die Markierung
            // faelschlich veraendert.
            amUnterdrueckeAmSelectionEvent = true;

            foreach (Border kachel in AmDirekteAuswahlListe.Items.Cast<Border>())
            {
                if (kachel.Tag is Erinnerung erinnerungFuerAuswahl && amMarkierteErinnerungIds.Contains(erinnerungFuerAuswahl.Id))
                {
                    AmDirekteAuswahlListe.SelectedItems.Add(kachel);
                    SetzeAmKachelMarkierungsOptik(kachel, true);
                }
            }

            amUnterdrueckeAmSelectionEvent = false;

            // A/Opa-ARCHITEKTURAUFTRAG "JAMES-SUCHE -> AM ALS EINZIGER
            // ARBEITSBEREICH" (16.08.), Punkt 6: Trefferzahl UND Suchbegriff
            // sichtbar machen - gilt gleichermassen, ob die Suche hier direkt
            // in der AM eingegeben wurde oder von der James-Suchleiste oben
            // uebergeben wurde (UebergibSucheAnArbeitsmappe in MainWindow.
            // Suche.cs setzt nur AmDirekteSucheTextBox.Text, dieser Text
            // wird hier ohnehin bereits ausgelesen).
            if (AmDirekteTrefferAnzahlText != null)
            {
                AmDirekteTrefferAnzahlText.Text = suchtext.Trim() == ""
                    ? treffer.Count + " Erinnerung(en) im Bestand:"
                    : "🔍 Suchergebnisse: \"" + suchtext.Trim() + "\" – " + treffer.Count + " Erinnerung(en):";
            }

            AktualisiereAmBottomLeiste(treffer.Count);
            AktualisiereAmMarkierungsAbhaengigeAnzeige();
            AktualisiereAmPapierkorbAnzeige();
        }

        // A/Opa-UMBAU "AM GRUNDLEGEND VEREINFACHEN" (17.08.), §7: schmale
        // Statusleiste ganz unten - Anzahl links. Der "N markiert"-Text
        // wird separat von AktualisiereAmMarkierungsAbhaengigeAnzeige
        // gepflegt (aktualisiert sich dadurch auch bei reinem Markieren,
        // ohne dass die Liste neu aufgebaut werden muss).
        private void AktualisiereAmBottomLeiste(int anzahl)
        {
            if (AmBottomAnzahlText != null)
            {
                AmBottomAnzahlText.Text = anzahl == 1 ? "1 Erinnerung" : anzahl + " Erinnerungen";
            }
        }

        // A/Opa-UMBAU "AM GRUNDLEGEND VEREINFACHEN" (17.08.), §7: markiert
        // ALLE aktuell sichtbaren (nicht durch einen Suchbegriff
        // weggefilterten) Kacheln - genau dieselbe Datenquelle, die auch
        // die einzelne Markierung per Klick verwendet.
        private void AmAlleMarkieren_Click(object sender, RoutedEventArgs e)
        {
            foreach (Border kachel in AmDirekteAuswahlListe.Items.Cast<Border>())
            {
                if (kachel.Tag is Erinnerung erinnerung)
                {
                    amMarkierteErinnerungIds.Add(erinnerung.Id);
                }
            }

            AktualisiereAmDirekteAuswahlListe();
        }

        // A/Opa-UMBAU "AM GRUNDLEGEND VEREINFACHEN" (17.08.), §7: "Tisch
        // abraeumen" - raeumt AUSSCHLIESSLICH die Anzeige der Arbeitsmappe
        // leer. Loescht KEINE Datei, KEINE Zuordnung, verschiebt nichts in
        // den Papierkorb oder die Asservatenkammer. Die Markierung wird mit
        // aufgeraeumt (ein leerer Tisch kann nichts markiert haben) - das
        // ist ein reiner Anzeige-/Arbeits-Zustand, keine Datenaenderung.
        private void AmArbeitsmappeLeeren_Click(object sender, RoutedEventArgs e)
        {
            amArbeitstischGeleert = true;
            amMarkierteErinnerungIds.Clear();
            AktualisiereAmDirekteAuswahlListe();
        }

        // A/Opa-BAUAUFTRAG "AM: GESAMTUMBAU" (16.08.): einzige Stelle, die
        // eine Auswahlaenderung im gruenen Fenster in amMarkierteErinnerungIds
        // uebertraegt. Betrifft NUR die aktuell sichtbaren (nicht durch
        // einen Suchbegriff weggefilterten) Kacheln - Erinnerungen, die
        // gerade nicht in der Liste stehen, bleiben in ihrem bisherigen
        // Markierungs-Zustand unangetastet. Dadurch uebersteht eine
        // Markierung eine zwischenzeitliche Sucheingabe.
        private void AmDirekteAuswahlListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (amUnterdrueckeAmSelectionEvent)
            {
                return;
            }

            foreach (Border kachel in AmDirekteAuswahlListe.Items.Cast<Border>())
            {
                if (!(kachel.Tag is Erinnerung erinnerung))
                {
                    continue;
                }

                bool ausgewaehlt = AmDirekteAuswahlListe.SelectedItems.Contains(kachel);

                if (ausgewaehlt)
                {
                    amMarkierteErinnerungIds.Add(erinnerung.Id);
                }
                else
                {
                    amMarkierteErinnerungIds.Remove(erinnerung.Id);
                }

                // A/Opa-REPARATURAUFTRAG "AM TEST 3" (16.08.): sofortige
                // sichtbare Rueckmeldung beim Markieren/Entmarkieren -
                // unabhaengig davon, ob die Liste danach neu aufgebaut wird.
                SetzeAmKachelMarkierungsOptik(kachel, ausgewaehlt);
            }

            AktualisiereAmMarkierungsAbhaengigeAnzeige();
        }

        // A/Opa-BAUAUFTRAG "AM: GESAMTUMBAU" (16.08.): landet nicht mehr in
        // einer eigenen zweiten Liste, sondern markiert die betroffenen
        // Erinnerungen direkt im EINEN gruenen Fenster. herkunft wird nicht
        // mehr separat angezeigt (die bisherige Unterscheidung "woher kam
        // die Erinnerung" sollte laut Auftrag fuer die Oberflaeche
        // vollstaendig entfallen), der Parameter bleibt aus Kompatibilitaets-
        // Gruenden zur bestehenden Aufrufstelle (ErinnerungsmodellBetrachterFenster) erhalten.
        private void SendeErinnerungsIdsZurArbeitsmappe(List<Guid> erinnerungIds, string herkunft)
        {
            foreach (Guid id in erinnerungIds)
            {
                amMarkierteErinnerungIds.Add(id);
            }

            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;

            // A/Opa-UMBAU "AM GRUNDLEGEND VEREINFACHEN" (17.08.), §7: eine
            // Erinnerung "ausgelegt" zu bekommen beendet den "leer"-Zustand.
            amArbeitstischGeleert = false;

            if (AmDirekteSucheTextBox != null)
            {
                AmDirekteSucheTextBox.Text = "";
            }

            AktualisiereAmDirekteAuswahlListe();
        }

        private class TestimportErgebnis
        {
            public int Importiert;
            public int UebersprungenDuplikat;
            public int UebersprungenTyp;
        }

        private static readonly string[] BildEndungen = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".heic", ".webp" };
        private static readonly string[] PdfEndungen = { ".pdf" };
        private static readonly string[] DokumentEndungen = { ".doc", ".docx", ".odt", ".rtf", ".xls", ".xlsx", ".ppt", ".pptx", ".txt", ".md" };
        private static readonly string[] VideoEndungen = { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".m4v" };

        private static MedienTyp? ErmittleMedienTyp(string dateiendung)
        {
            string endung = (dateiendung ?? "").ToLowerInvariant();

            if (BildEndungen.Contains(endung)) { return MedienTyp.Bild; }
            if (PdfEndungen.Contains(endung)) { return MedienTyp.Pdf; }
            if (DokumentEndungen.Contains(endung)) { return MedienTyp.Dokument; }
            if (VideoEndungen.Contains(endung)) { return MedienTyp.Video; }

            return null;
        }

        private static DateTime? SicheresDateiDatum(string pfad)
        {
            try
            {
                return File.GetLastWriteTime(pfad);
            }
            catch
            {
                return null;
            }
        }

        private TestimportErgebnis TestimportDurchfuehren(List<string> quellDateien)
        {
            LadeErinnerungsmodellFallsNoetig();

            TestimportErgebnis ergebnis = new TestimportErgebnis();

            string zielOrdner = Path.Combine(OrdnerPfad, "Testimport");
            Directory.CreateDirectory(zielOrdner);

            HashSet<string> bekannteHashes = erinnerungsmodellErinnerungen
                .Where(er => !string.IsNullOrEmpty(er.Hashwert))
                .Select(er => er.Hashwert)
                .ToHashSet();

            foreach (string quellDatei in quellDateien)
            {
                MedienTyp? typ = ErmittleMedienTyp(Path.GetExtension(quellDatei));

                if (typ == null)
                {
                    ergebnis.UebersprungenTyp++;
                    continue;
                }

                // A/Opa-ENTSCHEIDUNG (11.08.), Folgefund zum "Zwei Karteikaesten"-
                // Bug: der Testimport berechnete den Hashwert bisher selbst
                // (SHA256 + Convert.ToHexString), waehrend die bereits
                // bestehende, im Projekt etablierte Methode BerechneHashwert
                // (Werkzeuge.cs, auch vom Sehzentrum und vom "Computer
                // kennenlernen"-Rundgang genutzt) SHA256 + Convert.
                // ToBase64String verwendet. Gleicher Algorithmus, aber zwei
                // unterschiedliche Textformate - dieselbe Datei bekam so
                // NIE denselben Hashwert, die Sehzentrum-Verknuepfung lief
                // dadurch komplett ins Leere. Fix: keine zweite Hash-
                // Berechnung mehr, stattdessen dieselbe geteilte Methode.
                string hash;

                try
                {
                    hash = BerechneHashwert(quellDatei);
                }
                catch
                {
                    continue;
                }

                if (bekannteHashes.Contains(hash))
                {
                    ergebnis.UebersprungenDuplikat++;
                    continue;
                }

                string neuerDateiname = Guid.NewGuid() + Path.GetExtension(quellDatei);
                string zielPfad = Path.Combine(zielOrdner, neuerDateiname);
                File.Copy(quellDatei, zielPfad, overwrite: false);

                Erinnerung erinnerung = new Erinnerung
                {
                    Hashwert = hash,
                    MedienTyp = typ.Value,
                    Erstellungsdatum = SicheresDateiDatum(quellDatei),
                    // A/Opa-REPARATURAUFTRAG (11.08.), PROBLEM 1: echter
                    // Importzeitpunkt, getrennt vom (moeglicherweise sehr
                    // alten) Erstellungsdatum der Originaldatei - macht
                    // "neueste zuerst" endlich zuverlaessig.
                    ImportiertAm = DateTime.Now
                };
                erinnerung.Fundorte.Add(new Fundort { Pfad = zielPfad, FundortRolle = "Testimport-Kopie" });

                erinnerungsmodellErinnerungen.Add(erinnerung);
                bekannteHashes.Add(hash);
                ergebnis.Importiert++;
            }

            if (ergebnis.Importiert > 0)
            {
                SpeichereErinnerungsmodell();
            }

            return ergebnis;
        }

        private void BestaetigeUndFuehreTestimportAus(List<string> dateien, string quellBeschreibung)
        {
            if (dateien == null || dateien.Count == 0)
            {
                James.Hinweis("Keine Datei(en) ausgewählt.");
                return;
            }

            int unterstuetzt = dateien.Count(d => ErmittleMedienTyp(Path.GetExtension(d)) != null);

            bool bestaetigt = James.FrageJaNein(
                quellBeschreibung + "\n\n" +
                dateien.Count + " Datei(en) zum Import ausgewählt, davon " + unterstuetzt + " unterstützte Bild-/PDF-/Dokument-/Videodateien.\n\n" +
                "Diese werden KOPIERT (das Original bleibt unverändert) und als zusätzlicher Testbestand aufgenommen. Bereits identische, schon importierte Dateien werden automatisch übersprungen.\n\n" +
                "Import jetzt starten?",
                James.TitelEntscheidung);

            if (!bestaetigt)
            {
                return;
            }

            TestimportErgebnis testergebnis = TestimportDurchfuehren(dateien);

            James.Hinweis(
                testergebnis.Importiert + " neue Erinnerung(en) importiert.\n" +
                testergebnis.UebersprungenDuplikat + " bereits vorhanden (übersprungen).\n" +
                testergebnis.UebersprungenTyp + " nicht unterstützte Datei(en) übersprungen.");
        }

        private void TestimportDateienWaehlenUndAusfuehren()
        {
            LadeErinnerungsmodellFallsNoetig();

            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Einzelne Datei(en) für Testimport wählen",
                Multiselect = true,
                Filter = "Unterstützte Dateien|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff;*.heic;*.webp;*.pdf;*.doc;*.docx;*.odt;*.rtf;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.md;*.mp4;*.mov;*.avi;*.mkv;*.wmv;*.m4v|Alle Dateien (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            BestaetigeUndFuehreTestimportAus(dialog.FileNames.ToList(), "Einzeln ausgewählte Datei(en).");
        }

        private void TestimportOrdnerWaehlenUndAusfuehren()
        {
            LadeErinnerungsmodellFallsNoetig();

            Microsoft.Win32.OpenFolderDialog dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Ordner für Testimport wählen"
            };

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            string ordner = dialog.FolderName;

            List<string> dateien = Directory.Exists(ordner)
                ? Directory.GetFiles(ordner, "*", SearchOption.AllDirectories).ToList()
                : new List<string>();

            BestaetigeUndFuehreTestimportAus(dateien, "Ordner: " + ordner);
        }

        private void AmTestimportDatei_Click(object sender, RoutedEventArgs e)
        {
            TestimportDateienWaehlenUndAusfuehren();
            amArbeitstischGeleert = false;
            AktualisiereAmDirekteAuswahlListe();
        }

        private void AmTestimportOrdner_Click(object sender, RoutedEventArgs e)
        {
            TestimportOrdnerWaehlenUndAusfuehren();
            amArbeitstischGeleert = false;
            AktualisiereAmDirekteAuswahlListe();
        }

        private void ErinnerungsmodellBetrachterOeffnen_Click(object sender, RoutedEventArgs e)
        {
            OeffneArbeitsmotor(null, null);
        }

        private void OeffneArbeitsmotorFuerZiel(ZuordnungsZielTyp zielTyp, Guid zielId)
        {
            OeffneArbeitsmotor(zielTyp, zielId);
        }

        private void OeffneArbeitsmotor(ZuordnungsZielTyp? vorausgewaehlterZielTyp, Guid? vorausgewaehlteZielId)
        {
            LadeErinnerungsmodellFallsNoetig();

            ErinnerungsmodellBetrachterFenster fenster = new ErinnerungsmodellBetrachterFenster(
                erinnerungsmodellErinnerungen,
                erinnerungsmodellZuordnungen,
                erinnerungsmodellZuordnungenPapierkorb,
                allePersonen,
                ArchivListe.Items.OfType<Person>().ToList(),
                freieEreignisse,
                freieEreignisseArchiv,
                sammlungen,
                sammlungenArchiv,
                LiesVisuelleMerkmale,
                ZentraleErinnerungsSuche,
                TestimportDateienWaehlenUndAusfuehren,
                TestimportOrdnerWaehlenUndAusfuehren,
                PruefeSehzentrumBestand,
                EntferneZuordnungenInPapierkorb,
                WiederherstelleZuordnung,
                LoescheZuordnungEndgueltig,
                ids => SendeErinnerungsIdsZurArbeitsmappe(ids, "Suche"),
                ids => SendeErinnerungsIdsZurArbeitsmappe(ids, "Ziel"),
                vorausgewaehlterZielTyp,
                vorausgewaehlteZielId);

            fenster.Owner = this;
            fenster.ShowDialog();
        }

        // A/Opa-REPARATURAUFTRAG "AM-PAPIERKORB VERWENDET NOCH ALTE
        // ZIELBEZOGENE LOGIK" (17.08.): AmZielTypComboBox_SelectionChanged
        // und AktualisiereAmZielAuswahl sind komplett entfallen - die AM
        // fragt beim Papierkorb-Vorgang kein Ziel mehr ab (siehe
        // AmMarkierteInPapierkorb_Click). AmZielTypComboBox/AmZielObjekt-
        // ComboBox wurden aus MainWindow.xaml entfernt, keine andere Stelle
        // im Code verwendet sie noch (codegenau geprueft).

        // A/Opa-BAUAUFTRAG "AM: GESAMTUMBAU ZU EINEM ARBEITSBEREICH" (16.08.):
        // liest die Markierung jetzt direkt aus amMarkierteErinnerungIds -
        // der EINEN, einzigen Quelle der Wahrheit. Ersetzt sowohl die
        // fruehere zweigeteilte Version (AmDirekteAuswahlListe UND
        // AmArbeitsauswahlListe) als auch die noch aeltere reine
        // Direktsuche-Version. Wird von der rechten Aktionsleiste
        // (Person/Ereignis/Sammlung/besonderes Ereignis/Asservatenkammer)
        // ebenso verwendet wie von der AM-eigenen Ziel-Auswahl.
        private List<Guid> ErmittleMarkierteGruenBereichErinnerungIds()
        {
            return amMarkierteErinnerungIds.ToList();
        }

        // A/Opa-BAUAUFTRAG "AM: GESAMTUMBAU" (16.08.): EINE zentrale Stelle,
        // die nach jeder Aenderung der Markierung (Auswahl im gruenen
        // Fenster, Ziel-Auswahl, Import, Neuaufbau der Liste) sowohl die
        // beiden AM-eigenen Buttons ("Neue Zuordnung anlegen"/"Markierte in
        // den Papierkorb", zusaetzlich von der Ziel-Auswahl abhaengig) als
        // auch die komplette rechte Aktionsleiste (Person/Ereignis/
        // Sammlung/Asservatenkammer - nur von der Markierung selbst
        // abhaengig, AktualisiereArbeitsmappenWerkzeuge in MainWindow.
        // Arbeitsmappe.cs) auf denselben, einzigen Markierungs-Stand
        // abstimmt.
        private void AktualisiereAmMarkierungsAbhaengigeAnzeige()
        {
            int anzahl = amMarkierteErinnerungIds.Count;

            // A/Opa-REPARATURAUFTRAG "AM-PAPIERKORB VERWENDET NOCH ALTE
            // ZIELBEZOGENE LOGIK" (17.08.): kein Ziel mehr noetig - nur
            // noch von der Markierung selbst abhaengig.
            if (AmMarkierteInPapierkorbButton != null)
            {
                AmMarkierteInPapierkorbButton.IsEnabled = anzahl > 0;
            }

            // A/Opa-UMBAU "AM GRUNDLEGEND VEREINFACHEN" (17.08.), §7: die
            // "N markiert"-Anzeige in der unteren Statusleiste haengt
            // ausschliesslich von der Markierung ab (nicht von der Anzahl
            // sichtbarer Kacheln) - deshalb hier statt in Aktualisiere-
            // AmBottomLeiste aktualisiert, wird von JEDER Markierungs-
            // aenderung erreicht (auch reinem Klicken ohne Listen-Neuaufbau).
            if (AmBottomMarkiertText != null)
            {
                AmBottomMarkiertText.Text = anzahl == 0 ? "" : anzahl + " markiert";
            }

            AktualisiereArbeitsmappenWerkzeuge();
        }

        // A/Opa-BAUAUFTRAG "JAMES-EINZUG" (12.08.), Punkt 5: Kernlogik des
        // Zuordnens, bisher nur inline im AM-Button-Handler - jetzt eine
        // zentrale, wiederverwendbare Methode (kein Kopieren des Handlers
        // fuer die neue James-Suche noetig). Erzeugt ausschliesslich neue
        // Zuordnungs-Datensaetze - NIE eine physische Dateikopie.
        //
        // A/Opa-DIAGNOSE-PLUS-SCHUTZAUFTRAG (13.08.), code-bestaetigte
        // Root Cause der 7 gefundenen Abweichungen ("9 gespeichert, nur 6
        // angezeigt"): diese Methode war die EINZIGE Stelle im gesamten
        // Bestand, die neue Zuordnung-Objekte erzeugt (per grep bestaetigt),
        // und pruefte bisher NICHT, ob dieselbe Erinnerung demselben Ziel
        // bereits zugeordnet war. Jetzt: Schutzregel - die Kombination
        // Erinnerung+ZielTyp+ZielId darf nur einmal existieren. Bereits
        // zugeordnete Erinnerungen werden uebersprungen (gezaehlt, aber
        // kein zweiter Datensatz angelegt). WICHTIG: bestehende, VOR dieser
        // Aenderung bereits entstandene Doppelzuordnungen werden hier NICHT
        // angefasst/bereinigt - das ist ausdruecklich ein separater, noch
        // nicht erteilter Auftrag (A/Opa-Entscheidung 13.08., Punkt 3+4).
        private bool FuehreZuordnungDurch(List<Guid> erinnerungIds, ZuordnungsZielTyp zielTyp, Guid zielId, string zielBezeichnung, out int anzahlBereitsVorhanden)
        {
            LadeErinnerungsmodellFallsNoetig();

            anzahlBereitsVorhanden = 0;

            foreach (Guid erinnerungId in erinnerungIds)
            {
                bool bereitsVorhanden = erinnerungsmodellZuordnungen.Any(z =>
                    z.ErinnerungId == erinnerungId && z.ZielTyp == zielTyp && z.ZielId == zielId);

                if (bereitsVorhanden)
                {
                    anzahlBereitsVorhanden++;
                    continue;
                }

                erinnerungsmodellZuordnungen.Add(new Zuordnung
                {
                    ErinnerungId = erinnerungId,
                    ZielTyp = zielTyp,
                    ZielId = zielId,
                    ZielBezeichnung = zielBezeichnung
                });
            }

            return SpeichereErinnerungsmodell();
        }

        // A/Opa-OPTIMIERUNGSAUFTRAG "Opa-freundliches James" (16.08.), Teil D:
        // urspruengliche Version dieses Buttons - siehe REPARATURAUFTRAG
        // direkt darunter fuer die aktuelle Logik.
        //
        // A/Opa-REPARATURAUFTRAG "AM-PAPIERKORB = VOM ARBEITSTISCH WEGLEGEN"
        // (17.08.), zweiter, endgueltiger Anlauf: der vorherige Fix (Zuord-
        // nungen der markierten Erinnerung(en) entfernen) traf den falschen
        // Grundsatz - A/Opa stellten klar, dass "AM -> Papierkorb" NICHTS an
        // Zuordnungen/Dateien aendern darf, sondern AUSSCHLIESSLICH die
        // Sichtbarkeit in der Arbeitsmappe selbst betrifft ("vom Arbeitstisch
        // weglegen", nicht "Zuordnung loeschen"). Neu: markierte Erinnerung(en)
        // wandern in einen eigenen, neuen, dauerhaft gespeicherten AM-
        // Papierkorb (amPapierkorbErinnerungIds, eigene Datei am_papierkorb.
        // json - bewusst NICHT im bestehenden erinnerungsmodell.json, dessen
        // Speicherklasse mir nicht vorliegt) und werden dadurch aus JEDER
        // Ansicht der AM ausgeblendet (AktualisiereAmDirekteAuswahlListe
        // filtert sie heraus). Zuordnungen, Ordner und Originaldatei(en)
        // bleiben dabei zu 100% unangetastet - auch bei einer bisher
        // UNZUGEORDNETEN Erinnerung funktioniert das jetzt. Im Papierkorb-Tab
        // erscheinen sie im neuen Abschnitt "Aus der Arbeitsmappe entfernt"
        // und koennen von dort jederzeit wiederhergestellt werden.
        private void AmMarkierteInPapierkorb_Click(object sender, RoutedEventArgs e)
        {
            List<Guid> markiert = ErmittleMarkierteGruenBereichErinnerungIds();

            if (markiert.Count == 0)
            {
                James.Hinweis("Bitte zuerst markieren, welche Erinnerung(en) in den Papierkorb sollen (in der Liste oben Strg-/Umschalt-Klick). Ohne Markierung wird nichts verändert.");
                return;
            }

            LadeAmPapierkorbFallsNoetig();

            bool ergebnis = James.FrageJaNein(
                markiert.Count + " markierte Erinnerung(en) in den Papierkorb legen?\n\n" +
                "Entfernt die markierten Erinnerungen nur aus der Arbeitsmappe. Zuordnungen, Ordner und Originaldateien bleiben vollständig unverändert. Im Papierkorb-Tab jederzeit wiederherstellbar.",
                James.TitelEntscheidung, MessageBoxImage.Warning);

            if (!ergebnis)
            {
                return;
            }

            foreach (Guid id in markiert)
            {
                amPapierkorbErinnerungIds.Add(id);
                amMarkierteErinnerungIds.Remove(id);
            }

            SpeichereAmPapierkorb();
            AktualisiereAmDirekteAuswahlListe();

            AmStatusText.Text = "✓ " + markiert.Count + " Erinnerung(en) in den Papierkorb gelegt.";
        }

        // A/Opa-REPARATURAUFTRAG "AM-PAPIERKORB = VOM ARBEITSTISCH WEGLEGEN"
        // (17.08.): eigene, unabhaengige Speicherdatei fuer die neue Liste
        // "aus der AM entfernte Erinnerungen" - bewusst NICHT ein neues Feld
        // in der bestehenden erinnerungsmodell.json-Speicherklasse
        // (ArchivErinnerungsDaten), da deren Definition in einer Datei liegt,
        // die mir nicht vorliegt und die ich deshalb nicht gefahrlos aendern
        // kann. Diese eigene Datei beruehrt erinnerungsmodell.json in keiner
        // Weise.
        private string AmPapierkorbDateiPfad => Path.Combine(OrdnerPfad, "am_papierkorb.json");

        private HashSet<Guid> amPapierkorbErinnerungIds = new HashSet<Guid>();
        private bool amPapierkorbGeladen = false;

        private void LadeAmPapierkorbFallsNoetig()
        {
            if (amPapierkorbGeladen)
            {
                return;
            }

            amPapierkorbErinnerungIds = new HashSet<Guid>();

            try
            {
                if (File.Exists(AmPapierkorbDateiPfad))
                {
                    string json = File.ReadAllText(AmPapierkorbDateiPfad);
                    List<Guid> geladen = JsonSerializer.Deserialize<List<Guid>>(json);

                    if (geladen != null)
                    {
                        amPapierkorbErinnerungIds = new HashSet<Guid>(geladen);
                    }
                }
            }
            catch
            {
            }

            amPapierkorbGeladen = true;
        }

        private void SpeichereAmPapierkorb()
        {
            try
            {
                Directory.CreateDirectory(OrdnerPfad);
                string json = JsonSerializer.Serialize(amPapierkorbErinnerungIds.ToList());
                File.WriteAllText(AmPapierkorbDateiPfad, json);
            }
            catch
            {
            }
        }

        // Baut die Anzeige im Papierkorb-Tab, Abschnitt "Aus der Arbeitsmappe
        // entfernt" - ein eigener, in sich geschlossener Bereich, der keine
        // der bestehenden Papierkorb-Listen (GemeinsamerPapierkorbListe, in
        // einer mir nicht vorliegenden Datei gepflegt) beruehrt oder
        // dupliziert.
        private void AktualisiereAmPapierkorbAnzeige()
        {
            LadeErinnerungsmodellFallsNoetig();
            LadeAmPapierkorbFallsNoetig();

            if (AmPapierkorbListe == null)
            {
                return;
            }

            AmPapierkorbListe.Items.Clear();

            List<Erinnerung> eintraege = erinnerungsmodellErinnerungen
                .Where(er => amPapierkorbErinnerungIds.Contains(er.Id))
                .ToList();

            foreach (Erinnerung erinnerung in eintraege)
            {
                string dateiname = erinnerung.Fundorte.Count > 0 ? Path.GetFileName(erinnerung.Fundorte[0].Pfad) : erinnerung.Id.ToString();
                AmPapierkorbListe.Items.Add(ErstelleErinnerungsKachel(erinnerung, dateiname));
            }

            if (AmPapierkorbAnzahlText != null)
            {
                AmPapierkorbAnzahlText.Text = eintraege.Count == 0
                    ? "Der Arbeitsmappen-Papierkorb ist leer."
                    : eintraege.Count + " Erinnerung(en) aus der Arbeitsmappe entfernt:";
            }
        }

        private void AmPapierkorbListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AmPapierkorbWiederherstellenButton != null)
            {
                AmPapierkorbWiederherstellenButton.IsEnabled = AmPapierkorbListe.SelectedItems.Count > 0;
            }
        }

        private void AmPapierkorbWiederherstellen_Click(object sender, RoutedEventArgs e)
        {
            List<Guid> ausgewaehlt = AmPapierkorbListe.SelectedItems
                .Cast<Border>()
                .Select(b => b.Tag as Erinnerung)
                .Where(er => er != null)
                .Select(er => er.Id)
                .ToList();

            if (ausgewaehlt.Count == 0)
            {
                return;
            }

            foreach (Guid id in ausgewaehlt)
            {
                amPapierkorbErinnerungIds.Remove(id);
            }

            SpeichereAmPapierkorb();
            AktualisiereAmPapierkorbAnzeige();
            AktualisiereAmDirekteAuswahlListe();

            James.Hinweis(ausgewaehlt.Count + " Erinnerung(en) wiederhergestellt - wieder sichtbar in der Arbeitsmappe.");
        }

        // ============================================================
        // A/Opa-REPARATURAUFTRAG (11.08.), PROBLEM 3
        // ============================================================
        // Wird von den alten Papierkorb-Kontext-Callbacks (MainWindow.
        // Erinnerungskarte.cs, MainWindow.Sammlungen.cs, MainWindow.
        // BesondereEreignisse.cs) NUR dann aufgerufen, wenn die jeweilige
        // alte Entfernen-Logik nichts gefunden hat (keine stillschweigend
        // wirkungslose Aktion mehr). Sucht die Erinnerung ueber den
        // Dateipfad, prueft ob dafuer tatsaechlich eine aktive Zuordnung
        // zu genau diesem Ziel existiert, und verschiebt in diesem Fall
        // NUR diese eine Zuordnung in den Zuordnungs-Papierkorb (bereits
        // bestehende, getestete EntferneZuordnungenInPapierkorb wird
        // wiederverwendet - keine zweite Papierkorb-Logik). Erinnerung
        // selbst, andere Zuordnungen und die Originaldatei bleiben
        // unangetastet. Gibt zurueck, ob wirklich etwas entfernt wurde.
        private bool VersucheAusNeuemModellEntfernen(ZuordnungsZielTyp zielTyp, Guid zielId, string pfad)
        {
            LadeErinnerungsmodellFallsNoetig();

            Erinnerung erinnerung = erinnerungsmodellErinnerungen.FirstOrDefault(er =>
                er.Fundorte != null && er.Fundorte.Any(f => string.Equals(f.Pfad, pfad, StringComparison.OrdinalIgnoreCase)));

            if (erinnerung == null)
            {
                return false;
            }

            bool vorhanden = erinnerungsmodellZuordnungen.Any(z =>
                z.ErinnerungId == erinnerung.Id && z.ZielTyp == zielTyp && z.ZielId == zielId);

            if (!vorhanden)
            {
                return false;
            }

            EntferneZuordnungenInPapierkorb(new List<Guid> { erinnerung.Id }, zielTyp, zielId);

            return true;
        }

        private void EntferneZuordnungenInPapierkorb(List<Guid> erinnerungIds, ZuordnungsZielTyp zielTyp, Guid zielId)
        {
            LadeErinnerungsmodellFallsNoetig();

            List<Zuordnung> betroffene = erinnerungsmodellZuordnungen
                .Where(z => z.ZielTyp == zielTyp && z.ZielId == zielId && erinnerungIds.Contains(z.ErinnerungId))
                .ToList();

            foreach (Zuordnung zuordnung in betroffene)
            {
                erinnerungsmodellZuordnungen.Remove(zuordnung);
                erinnerungsmodellZuordnungenPapierkorb.Add(zuordnung);
            }

            SpeichereErinnerungsmodell();
        }

        private void WiederherstelleZuordnung(Zuordnung zuordnung)
        {
            LadeErinnerungsmodellFallsNoetig();

            if (!erinnerungsmodellZuordnungenPapierkorb.Remove(zuordnung))
            {
                return;
            }

            erinnerungsmodellZuordnungen.Add(zuordnung);
            SpeichereErinnerungsmodell();
        }

        private void LoescheZuordnungEndgueltig(Zuordnung zuordnung)
        {
            LadeErinnerungsmodellFallsNoetig();

            erinnerungsmodellZuordnungenPapierkorb.Remove(zuordnung);
            SpeichereErinnerungsmodell();
        }

        // ============================================================
        // A/Opa-BAUAUFTRAG "JAMES-EINZUG" (12.08.), PUNKT 7
        // ============================================================
        // Der Zuordnungs-Papierkorb war bisher nur im Arbeitsmotor-Fenster
        // sichtbar (siehe Test 3/4 vom 11.08. - keine Bug, aber schlecht
        // auffindbar). Jetzt zusaetzlich im normalen Papierkorb-Tab als
        // eigener, klar beschrifteter Bereich "Gelöste Zuordnungen" -
        // bewusst sprachlich von den drei bestehenden Objekt-Papierkorb-
        // Listen (ganze Person/Ereignis/Sammlung) unterschieden, damit die
        // zwei verschiedenen Ebenen (Objekt vs. einzelne Verknuepfung)
        // fuer den Nutzer nicht verschwimmen. Wiederverwendet ausschliesslich
        // die bereits bestehenden, getesteten Methoden - keine zweite
        // Papierkorb-Logik.
        private void AktualisiereZuordnungsPapierkorbAnzeige()
        {
            LadeErinnerungsmodellFallsNoetig();

            if (JamesGeloesteZuordnungenListe == null)
            {
                return;
            }

            JamesGeloesteZuordnungenListe.Items.Clear();

            foreach (Zuordnung zuordnung in erinnerungsmodellZuordnungenPapierkorb)
            {
                Erinnerung erinnerung = erinnerungsmodellErinnerungen.FirstOrDefault(er => er.Id == zuordnung.ErinnerungId);

                string dateiname = erinnerung != null && erinnerung.Fundorte.Count > 0
                    ? Path.GetFileName(erinnerung.Fundorte[0].Pfad)
                    : "(Erinnerung nicht mehr auffindbar)";

                string herkunftText = "war zugeordnet zu " + zuordnung.ZielTyp + ": " + zuordnung.ZielBezeichnung;

                Border kachel = erinnerung != null
                    ? ErstelleErinnerungsKachel(erinnerung, dateiname + "\n" + herkunftText)
                    : new Border { Child = new TextBlock { Text = dateiname + "\n" + herkunftText, TextWrapping = TextWrapping.Wrap, MaxWidth = 195, Margin = new Thickness(6) } };

                kachel.Tag = zuordnung;
                JamesGeloesteZuordnungenListe.Items.Add(kachel);
            }

            JamesGeloesteZuordnungenAnzahlText.Text = erinnerungsmodellZuordnungenPapierkorb.Count == 0
                ? "Keine einzelnen Erinnerungs-Zuordnungen im Papierkorb."
                : erinnerungsmodellZuordnungenPapierkorb.Count + " einzelne Erinnerungs-Zuordnung(en):";

            JamesZuordnungWiederherstellenButton.IsEnabled = false;
            JamesZuordnungEndgueltigLoeschenButton.IsEnabled = false;
        }

        private void JamesGeloesteZuordnungenListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int anzahl = JamesGeloesteZuordnungenListe.SelectedItems.Count;
            JamesZuordnungWiederherstellenButton.IsEnabled = anzahl > 0;
            JamesZuordnungEndgueltigLoeschenButton.IsEnabled = anzahl > 0;
        }

        private void JamesZuordnungWiederherstellen_Click(object sender, RoutedEventArgs e)
        {
            List<Zuordnung> ausgewaehlt = JamesGeloesteZuordnungenListe.SelectedItems
                .Cast<Border>()
                .Select(b => b.Tag as Zuordnung)
                .Where(z => z != null)
                .ToList();

            if (ausgewaehlt.Count == 0)
            {
                return;
            }

            foreach (Zuordnung zuordnung in ausgewaehlt)
            {
                WiederherstelleZuordnung(zuordnung);
            }

            AktualisiereZuordnungsPapierkorbAnzeige();

            ZeigeStatusMeldung(ausgewaehlt.Count == 1
                ? "1 Zuordnung wurde wiederhergestellt."
                : ausgewaehlt.Count + " Zuordnungen wurden wiederhergestellt.");
        }

        private void JamesZuordnungEndgueltigLoeschen_Click(object sender, RoutedEventArgs e)
        {
            List<Zuordnung> ausgewaehlt = JamesGeloesteZuordnungenListe.SelectedItems
                .Cast<Border>()
                .Select(b => b.Tag as Zuordnung)
                .Where(z => z != null)
                .ToList();

            if (ausgewaehlt.Count == 0)
            {
                return;
            }

            bool ergebnis = James.FrageJaNein(
                ausgewaehlt.Count + " gelöste Zuordnung(en) endgültig entfernen?\n\n" +
                "Das betrifft ausschließlich diese Zuordnungs-Datensätze - die Erinnerung(en) selbst und ihre physischen Dateien bleiben davon vollständig unberührt.",
                James.TitelEndgueltigeEntscheidung, MessageBoxImage.Warning);

            if (!ergebnis)
            {
                return;
            }

            foreach (Zuordnung zuordnung in ausgewaehlt)
            {
                LoescheZuordnungEndgueltig(zuordnung);
            }

            AktualisiereZuordnungsPapierkorbAnzeige();
        }

        // A/Opa-ENTSCHEIDUNG (11.08.): auf Sehzentrum umgestellt, wie die
        // Suche selbst - dieselbe Quelle, dieselbe Logik ueberall.
        private string PruefeSehzentrumBestand()
        {
            LadeErinnerungsmodellFallsNoetig();

            List<SehgedaechtnisEintrag> sehgedaechtnis = LadeSehgedaechtnis();

            int mitWissen = 0;
            int gesamtBegriffe = 0;

            foreach (Erinnerung erinnerung in erinnerungsmodellErinnerungen)
            {
                if (string.IsNullOrEmpty(erinnerung.Hashwert))
                {
                    continue;
                }

                SehgedaechtnisEintrag eintrag = sehgedaechtnis.FirstOrDefault(s => s.Hashwert == erinnerung.Hashwert);

                if (eintrag != null && eintrag.BestaetigteStichwoerter != null && eintrag.BestaetigteStichwoerter.Count > 0)
                {
                    mitWissen++;
                    gesamtBegriffe += eintrag.BestaetigteStichwoerter.Count;
                }
            }

            return mitWissen + " von " + erinnerungsmodellErinnerungen.Count + " Erinnerung(en) haben bereits Sehzentrum-Wissen (" + gesamtBegriffe + " Begriff(e) insgesamt).\n\n" +
                (mitWissen == 0
                    ? "Das erklärt vermutlich, warum eine Suche wie \"Hund\" noch nichts findet - James hat für den aktuellen Bestand einfach noch nichts gelernt, nicht weil die Suche fehlerhaft wäre."
                    : "Eine Suche nach einem tatsächlich gelernten Begriff sollte jetzt Treffer liefern.");
        }

        // ============================================================
        // A/Opa-DIAGNOSEAUFTRAG "FEHLENDE FUNDORTE" (13.08.)
        // ============================================================
        // AUSDRUECKLICH REIN LESEND: prueft nur File.Exists (und einen
        // kurzen Lesezugriff zur Unterscheidung "fehlt" vs. "vorhanden,
        // aber nicht lesbar"), veraendert an keiner Stelle Dateien,
        // Zuordnungen, den Papierkorb oder fuehrt irgendeine Migration
        // durch. Soll klaeren, ob "20 zugeordnet, nur 3 angezeigt" bzw.
        // "neu zugeordnet, am Ziel nicht sichtbar" tatsaechlich auf fehlende
        // Fundorte zurueckzufuehren sind - keine Reparatur, nur Befund.

        // Prueft einen einzelnen Fundort-Pfad und unterscheidet die von
        // A verlangten drei Faelle. Reiner Lesezugriff - FileStream wird
        // sofort wieder geschlossen, nichts wird geschrieben.
        private static string PruefeFundortStatus(string pfad)
        {
            if (string.IsNullOrWhiteSpace(pfad))
            {
                return "Pfad ungültig (leer)";
            }

            string vollPfad;

            try
            {
                vollPfad = Path.GetFullPath(pfad);
            }
            catch
            {
                return "Pfad ungültig";
            }

            if (!File.Exists(vollPfad))
            {
                return "Datei nicht vorhanden";
            }

            try
            {
                using (FileStream stream = File.OpenRead(vollPfad))
                {
                    // Nur oeffnen+sofort schliessen, um "vorhanden, aber
                    // nicht lesbar" (z.B. Berechtigung, gesperrt) von
                    // echtem Fehlen zu unterscheiden. Kein Schreibzugriff.
                }

                return "Vorhanden";
            }
            catch
            {
                return "Datei vorhanden, aber nicht lesbar";
            }
        }

        private class FundortDiagnoseZeile
        {
            public Guid ErinnerungId;
            public ZuordnungsZielTyp ZielTyp;
            public string ZielBezeichnung;
            public string FundortDetails;
        }

        private string DiagnoseFehlendeFundorte()
        {
            LadeErinnerungsmodellFallsNoetig();

            int erinnerungenGesamt = erinnerungsmodellErinnerungen.Count;
            int erinnerungenMitFundort = 0;
            int erinnerungenOhneFundort = 0;

            Dictionary<Guid, bool> erinnerungHatVorhandenenFundort = new Dictionary<Guid, bool>();
            Dictionary<Guid, string> erinnerungFundortDetails = new Dictionary<Guid, string>();

            foreach (Erinnerung erinnerung in erinnerungsmodellErinnerungen)
            {
                List<string> details = new List<string>();
                bool hatVorhandenen = false;

                if (erinnerung.Fundorte != null)
                {
                    foreach (Fundort fundort in erinnerung.Fundorte)
                    {
                        string status = PruefeFundortStatus(fundort.Pfad);
                        details.Add((fundort.Pfad ?? "(leer)") + " -> " + status);

                        if (status == "Vorhanden")
                        {
                            hatVorhandenen = true;
                        }
                    }
                }
                else
                {
                    details.Add("(keine Fundorte gespeichert)");
                }

                erinnerungHatVorhandenenFundort[erinnerung.Id] = hatVorhandenen;
                erinnerungFundortDetails[erinnerung.Id] = string.Join("; ", details);

                if (hatVorhandenen)
                {
                    erinnerungenMitFundort++;
                }
                else
                {
                    erinnerungenOhneFundort++;
                }
            }

            int zuordnungenGesamt = erinnerungsmodellZuordnungen.Count;
            int zuordnungenOhneFundort = 0;

            Dictionary<ZuordnungsZielTyp, int> betroffenProZielTyp = new Dictionary<ZuordnungsZielTyp, int>
            {
                { ZuordnungsZielTyp.Person, 0 },
                { ZuordnungsZielTyp.Ereignis, 0 },
                { ZuordnungsZielTyp.Sammlung, 0 }
            };

            List<FundortDiagnoseZeile> problematisch = new List<FundortDiagnoseZeile>();

            foreach (Zuordnung zuordnung in erinnerungsmodellZuordnungen)
            {
                bool hatVorhandenen = erinnerungHatVorhandenenFundort.TryGetValue(zuordnung.ErinnerungId, out bool wert) && wert;

                if (hatVorhandenen)
                {
                    continue;
                }

                zuordnungenOhneFundort++;
                betroffenProZielTyp[zuordnung.ZielTyp]++;

                problematisch.Add(new FundortDiagnoseZeile
                {
                    ErinnerungId = zuordnung.ErinnerungId,
                    ZielTyp = zuordnung.ZielTyp,
                    ZielBezeichnung = zuordnung.ZielBezeichnung,
                    FundortDetails = erinnerungFundortDetails.TryGetValue(zuordnung.ErinnerungId, out string d) ? d : "(unbekannt)"
                });
            }

            // A's Zusatzfrage: treten dieselben fehlenden Fundorte
            // gleichzeitig bei mehreren Zieltypen derselben Erinnerung auf?
            List<Guid> erinnerungenMitMehrerenZieltypenBetroffen = problematisch
                .GroupBy(z => z.ErinnerungId)
                .Where(g => g.Select(z => z.ZielTyp).Distinct().Count() > 1)
                .Select(g => g.Key)
                .ToList();

            // Bericht als eigene, reine Text-Datei schreiben (nur
            // erzeugt/geschrieben - keine bestehende Datei wird
            // veraendert), damit die komplette Detailliste einsehbar
            // ist, auch wenn sie fuer eine einzelne Meldung zu lang waere.
            string berichtPfad = Path.Combine(OrdnerPfad, "diagnose_fehlende_fundorte_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");

            System.Text.StringBuilder bericht = new System.Text.StringBuilder();
            bericht.AppendLine("DIAGNOSE: Fehlende Fundorte (rein lesend, " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + ")");
            bericht.AppendLine();
            bericht.AppendLine("Erinnerungen insgesamt: " + erinnerungenGesamt);
            bericht.AppendLine("Erinnerungen mit mindestens einem vorhandenen Fundort: " + erinnerungenMitFundort);
            bericht.AppendLine("Erinnerungen OHNE vorhandenen Fundort: " + erinnerungenOhneFundort);
            bericht.AppendLine();
            bericht.AppendLine("Zuordnungen insgesamt: " + zuordnungenGesamt);
            bericht.AppendLine("Zuordnungen, deren Erinnerung keinen vorhandenen Fundort mehr hat: " + zuordnungenOhneFundort);
            bericht.AppendLine("  davon Person: " + betroffenProZielTyp[ZuordnungsZielTyp.Person]);
            bericht.AppendLine("  davon Ereignis: " + betroffenProZielTyp[ZuordnungsZielTyp.Ereignis]);
            bericht.AppendLine("  davon Sammlung: " + betroffenProZielTyp[ZuordnungsZielTyp.Sammlung]);
            bericht.AppendLine();
            bericht.AppendLine("Erinnerungen, die GLEICHZEITIG bei mehreren Zieltypen betroffen sind: " + erinnerungenMitMehrerenZieltypenBetroffen.Count);
            bericht.AppendLine();
            bericht.AppendLine("=== EINZELFAELLE ===");

            foreach (FundortDiagnoseZeile zeile in problematisch.OrderBy(z => z.ErinnerungId))
            {
                bericht.AppendLine();
                bericht.AppendLine("ErinnerungId: " + zeile.ErinnerungId);
                bericht.AppendLine("Zuordnung: " + zeile.ZielTyp + " -> " + zeile.ZielBezeichnung);
                bericht.AppendLine("Fundort(e): " + zeile.FundortDetails);
            }

            try
            {
                File.WriteAllText(berichtPfad, bericht.ToString());
            }
            catch
            {
                // Bericht-Datei konnte nicht geschrieben werden - Diagnose
                // trotzdem als Kurzfassung zurueckgeben, nichts Kritisches.
            }

            return "Diagnose abgeschlossen (rein lesend, nichts verändert).\n\n" +
                erinnerungenGesamt + " Erinnerung(en) insgesamt, davon " + erinnerungenOhneFundort + " ohne vorhandenen Fundort.\n" +
                zuordnungenGesamt + " Zuordnung(en) insgesamt, davon " + zuordnungenOhneFundort + " betroffen " +
                "(Person: " + betroffenProZielTyp[ZuordnungsZielTyp.Person] + ", Ereignis: " + betroffenProZielTyp[ZuordnungsZielTyp.Ereignis] + ", Sammlung: " + betroffenProZielTyp[ZuordnungsZielTyp.Sammlung] + ").\n\n" +
                erinnerungenMitMehrerenZieltypenBetroffen.Count + " Erinnerung(en) sind gleichzeitig bei mehreren Zieltypen betroffen.\n\n" +
                "Vollständige Einzelfall-Liste gespeichert unter:\n" + berichtPfad;
        }

        private void DiagnoseFehlendeFundorte_Click(object sender, RoutedEventArgs e)
        {
            string ergebnis = DiagnoseFehlendeFundorte();
            James.Hinweis(ergebnis, "Diagnose: Fehlende Fundorte");
        }

        // ============================================================
        // A/Opa-DIAGNOSEAUFTRAG "ZUORDNUNGS-KETTE" (13.08.)
        // ============================================================
        // AUSDRUECKLICH REIN LESEND, KEIN FIX. Verfolgt fuer JEDES Ziel
        // (Person/Ereignis/Sammlung), zu dem mindestens eine Zuordnung
        // existiert, genau die von A vorgegebene Kette:
        //   1) im Datenbestand gespeicherte Zuordnungen (roh)
        //   2) davon eindeutige ErinnerungIds (Duplikate in den
        //      Zuordnungen selbst wuerden hier auffallen)
        //   3) davon tatsaechlich noch existierende Erinnerung-Objekte
        //      (verwaiste Zuordnungen auf geloeschte Erinnerungen wuerden
        //      hier auffallen)
        //   4) davon nach der Pfad-Deduplizierung von ErgaenzeUmNeuesModell
        //      uebrig bleibende (mehrere Erinnerungen mit demselben Fundort-
        //      Pfad wuerden sich hier gegenseitig verdraengen)
        //   5) davon tatsaechlich als Bild ladbar (ErinnerungenFenster.
        //      ZeigeUebersicht versucht JEDE Datei als Bild zu laden,
        //      unabhaengig vom MedienTyp - ein PDF/Dokument/Video wuerde
        //      hier durchfallen und in der Anzeige fehlen)
        // Meldet nur die Ziele, bei denen Schritt 1 != Schritt 5 ist -
        // genau dort verschwindet etwas zwischen Speicherung und Anzeige.
        private class ZuordnungsKettenBefund
        {
            public ZuordnungsZielTyp ZielTyp;
            public string ZielBezeichnung;
            public int SchrittGespeichert;
            public int SchrittEindeutigeIds;
            public int SchrittExistierendeErinnerungen;
            public int SchrittNachPfadDeduplizierung;
            public int SchrittTatsaechlichAlsBildLadbar;
            public List<string> Details = new List<string>();
        }

        private string DiagnoseZuordnungsKette()
        {
            LadeErinnerungsmodellFallsNoetig();

            // Alle Ziele ermitteln, zu denen mindestens eine Zuordnung
            // existiert - kein Raten, welche Sammlung/Person betroffen ist.
            List<(ZuordnungsZielTyp ZielTyp, Guid ZielId, string ZielBezeichnung)> alleZiele = erinnerungsmodellZuordnungen
                .Select(z => (z.ZielTyp, z.ZielId, z.ZielBezeichnung))
                .Distinct()
                .ToList();

            List<ZuordnungsKettenBefund> befunde = new List<ZuordnungsKettenBefund>();

            foreach ((ZuordnungsZielTyp zielTyp, Guid zielId, string zielBezeichnung) in alleZiele)
            {
                ZuordnungsKettenBefund befund = new ZuordnungsKettenBefund
                {
                    ZielTyp = zielTyp,
                    ZielBezeichnung = zielBezeichnung
                };

                // Schritt 1: roh gespeicherte Zuordnungen fuer dieses Ziel
                List<Zuordnung> zuordnungenFuerZiel = erinnerungsmodellZuordnungen
                    .Where(z => z.ZielTyp == zielTyp && z.ZielId == zielId)
                    .ToList();
                befund.SchrittGespeichert = zuordnungenFuerZiel.Count;

                // Schritt 2: eindeutige ErinnerungIds darunter
                List<Guid> eindeutigeIds = zuordnungenFuerZiel.Select(z => z.ErinnerungId).Distinct().ToList();
                befund.SchrittEindeutigeIds = eindeutigeIds.Count;

                if (eindeutigeIds.Count != zuordnungenFuerZiel.Count)
                {
                    befund.Details.Add("Es gibt mehrfache Zuordnungs-Datensätze zur selben Erinnerung (" + zuordnungenFuerZiel.Count + " Zuordnungen, aber nur " + eindeutigeIds.Count + " verschiedene Erinnerungen).");
                }

                // Schritt 3: davon tatsaechlich noch existierende Erinnerungen
                List<Erinnerung> existierendeErinnerungen = erinnerungsmodellErinnerungen
                    .Where(er => eindeutigeIds.Contains(er.Id))
                    .ToList();
                befund.SchrittExistierendeErinnerungen = existierendeErinnerungen.Count;

                if (existierendeErinnerungen.Count != eindeutigeIds.Count)
                {
                    int verwaist = eindeutigeIds.Count - existierendeErinnerungen.Count;
                    befund.Details.Add(verwaist + " Zuordnung(en) verweisen auf eine ErinnerungId, die im Erinnerungsbestand nicht mehr existiert (verwaiste Zuordnung).");
                }

                // Schritt 4: Pfad-Deduplizierung wie in ErgaenzeUmNeuesModell -
                // simuliert exakt dieselbe Logik (bekanntePfade-HashSet,
                // case-insensitive), aber rein lesend, nichts wird der
                // echten Anzeige hinzugefuegt.
                HashSet<string> bekanntePfadeSimuliert = new HashSet<string>();
                List<Erinnerung> nachPfadDeduplizierung = new List<Erinnerung>();
                Dictionary<string, List<Guid>> pfadKollisionen = new Dictionary<string, List<Guid>>();

                foreach (Erinnerung erinnerung in existierendeErinnerungen)
                {
                    string pfad = erinnerung.Fundorte?
                        .Select(f => f.Pfad)
                        .FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));

                    if (pfad == null)
                    {
                        befund.Details.Add("Erinnerung " + erinnerung.Id + " hat keinen gueltigen, existierenden Fundort-Pfad (widerspricht der vorigen Fundort-Diagnose - bitte melden).");
                        continue;
                    }

                    string pfadKlein = pfad.ToLowerInvariant();

                    if (!pfadKollisionen.ContainsKey(pfadKlein))
                    {
                        pfadKollisionen[pfadKlein] = new List<Guid>();
                    }
                    pfadKollisionen[pfadKlein].Add(erinnerung.Id);

                    if (bekanntePfadeSimuliert.Contains(pfadKlein))
                    {
                        continue;
                    }

                    bekanntePfadeSimuliert.Add(pfadKlein);
                    nachPfadDeduplizierung.Add(erinnerung);
                }

                befund.SchrittNachPfadDeduplizierung = nachPfadDeduplizierung.Count;

                foreach (KeyValuePair<string, List<Guid>> kollision in pfadKollisionen.Where(k => k.Value.Count > 1))
                {
                    befund.Details.Add("Mehrere Erinnerungen zeigen auf denselben Fundort-Pfad (" + kollision.Key + "): " + string.Join(", ", kollision.Value) + " - nur die erste zaehlt in der Anzeige, der Rest wird durch die Pfad-Deduplizierung verdraengt.");
                }

                // Schritt 5: tatsaechliche Bild-Ladbarkeit wie in
                // ErinnerungenFenster.ZeigeUebersicht (versucht jede Datei
                // als Bild zu laden, unabhaengig vom MedienTyp).
                int alsBildLadbar = 0;

                foreach (Erinnerung erinnerung in nachPfadDeduplizierung)
                {
                    string pfad = erinnerung.Fundorte
                        .Select(f => f.Pfad)
                        .FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));

                    try
                    {
                        BitmapImage testbild = new BitmapImage();
                        testbild.BeginInit();
                        testbild.CacheOption = BitmapCacheOption.OnLoad;
                        testbild.DecodePixelWidth = 200;
                        testbild.UriSource = new Uri(pfad);
                        testbild.EndInit();

                        alsBildLadbar++;
                    }
                    catch
                    {
                        befund.Details.Add("Erinnerung " + erinnerung.Id + " (MedienTyp: " + erinnerung.MedienTyp + ", Pfad: " + pfad + ") lässt sich NICHT als Bild laden - würde in \"Erinnerungen ansehen\" unsichtbar bleiben, egal was sonst stimmt.");
                    }
                }

                befund.SchrittTatsaechlichAlsBildLadbar = alsBildLadbar;

                befunde.Add(befund);
            }

            // Bericht schreiben - volle Liste aller Ziele, damit auch
            // unauffaellige Ziele zur Kontrolle einsehbar sind.
            string berichtPfad = Path.Combine(OrdnerPfad, "diagnose_zuordnungskette_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");

            System.Text.StringBuilder bericht = new System.Text.StringBuilder();
            bericht.AppendLine("DIAGNOSE: Zuordnungs-Kette (rein lesend, " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + ")");
            bericht.AppendLine("Kette je Ziel: 1) gespeichert -> 2) eindeutige Erinnerungen -> 3) davon existierend -> 4) davon nach Pfad-Deduplizierung -> 5) davon tatsächlich als Bild ladbar (= das, was am Ende in \"Erinnerungen ansehen\" erscheinen würde)");
            bericht.AppendLine();

            List<ZuordnungsKettenBefund> mitAbweichung = befunde.Where(b => b.SchrittGespeichert != b.SchrittTatsaechlichAlsBildLadbar).ToList();

            // A/Opa-SCHUTZAUFTRAG (13.08.), Punkt 3: Gesamtzahl der BEREITS
            // BESTEHENDEN Doppelzuordnungen (nur zaehlen und berichten -
            // ausdruecklich KEINE automatische Bereinigung, siehe
            // Schutzregel in FuehreZuordnungDurch, die nur kuenftige neue
            // Duplikate verhindert, nicht rueckwirkend aufraeumt).
            int gesamtDoppelzuordnungen = befunde.Sum(b => b.SchrittGespeichert - b.SchrittEindeutigeIds);

            bericht.AppendLine(alleZiele.Count + " Ziel(e) insgesamt geprüft, " + mitAbweichung.Count + " mit Abweichung zwischen gespeichert und tatsächlich anzeigbar.");
            bericht.AppendLine("Insgesamt " + gesamtDoppelzuordnungen + " bestehende Doppelzuordnung(en) über alle Ziele hinweg (nur gezählt, NICHT bereinigt).");
            bericht.AppendLine();
            bericht.AppendLine("=== ZIELE MIT ABWEICHUNG ===");

            foreach (ZuordnungsKettenBefund b in mitAbweichung.OrderByDescending(x => x.SchrittGespeichert - x.SchrittTatsaechlichAlsBildLadbar))
            {
                bericht.AppendLine();
                bericht.AppendLine(b.ZielTyp + ": " + b.ZielBezeichnung);
                bericht.AppendLine("  1) gespeichert: " + b.SchrittGespeichert);
                bericht.AppendLine("  2) eindeutige Erinnerungen: " + b.SchrittEindeutigeIds);
                bericht.AppendLine("  3) davon existierend: " + b.SchrittExistierendeErinnerungen);
                bericht.AppendLine("  4) davon nach Pfad-Deduplizierung: " + b.SchrittNachPfadDeduplizierung);
                bericht.AppendLine("  5) davon tatsächlich als Bild ladbar: " + b.SchrittTatsaechlichAlsBildLadbar);

                foreach (string detail in b.Details)
                {
                    bericht.AppendLine("  - " + detail);
                }
            }

            bericht.AppendLine();
            bericht.AppendLine("=== ALLE ZIELE (zur Kontrolle) ===");

            foreach (ZuordnungsKettenBefund b in befunde.OrderBy(x => x.ZielTyp).ThenBy(x => x.ZielBezeichnung))
            {
                bericht.AppendLine(b.ZielTyp + ": " + b.ZielBezeichnung + " - gespeichert " + b.SchrittGespeichert + ", angezeigt " + b.SchrittTatsaechlichAlsBildLadbar);
            }

            try
            {
                File.WriteAllText(berichtPfad, bericht.ToString());
            }
            catch
            {
            }

            if (mitAbweichung.Count == 0)
            {
                return "Diagnose abgeschlossen (rein lesend, nichts verändert).\n\n" +
                    alleZiele.Count + " Ziel(e) geprüft - bei KEINEM gibt es eine Abweichung zwischen gespeicherten und tatsächlich anzeigbaren Zuordnungen.\n\n" +
                    "Vollständiger Bericht gespeichert unter:\n" + berichtPfad;
            }

            ZuordnungsKettenBefund groesste = mitAbweichung.OrderByDescending(b => b.SchrittGespeichert - b.SchrittTatsaechlichAlsBildLadbar).First();

            return "Diagnose abgeschlossen (rein lesend, nichts verändert).\n\n" +
                alleZiele.Count + " Ziel(e) geprüft, " + mitAbweichung.Count + " mit Abweichung.\n" +
                "Insgesamt " + gesamtDoppelzuordnungen + " bestehende Doppelzuordnung(en) über alle Ziele hinweg (nur gezählt, nicht bereinigt).\n\n" +
                "Größte Abweichung: " + groesste.ZielTyp + " \"" + groesste.ZielBezeichnung + "\" - " +
                groesste.SchrittGespeichert + " gespeichert, aber nur " + groesste.SchrittTatsaechlichAlsBildLadbar + " tatsächlich anzeigbar.\n\n" +
                "Vollständiger Bericht (alle betroffenen Ziele mit genauer Kette + Einzelfall-Details) gespeichert unter:\n" + berichtPfad;
        }

        private void DiagnoseZuordnungsKette_Click(object sender, RoutedEventArgs e)
        {
            string ergebnis = DiagnoseZuordnungsKette();
            James.Hinweis(ergebnis, "Diagnose: Zuordnungs-Kette");
        }

        // ============================================================
        // A/Opa-REPARATURAUFTRAG (11.08.), PROBLEM 2: HASHWERT-REPARATURLAUF
        // ============================================================
        // Berechnet den Hashwert JEDER bestehenden Erinnerung mit der
        // einheitlichen BerechneHashwert-Methode (Werkzeuge.cs) neu -
        // behebt den fruehren Base64/Hex-Formatunterschied fuer bereits
        // vor dem Fix importierte Erinnerungen. Liest dafuer nur die
        // vorhandene Originaldatei ein (kein Schreiben, kein Verschieben,
        // kein Loeschen) und ueberschreibt ausschliesslich das Hashwert-
        // Feld im Verwaltungsbestand (erinnerungsmodell.json). Das
        // Sehzentrum-Wissen (sehgedaechtnis.json) wird hier nirgends
        // angefasst - es wird nur ueber den jetzt korrekten Hashwert
        // wieder auffindbar. Vor dem eigentlichen Lauf wird zur
        // Sicherheit eine zeitgestempelte Kopie der aktuellen
        // erinnerungsmodell.json angelegt.
        private string HashwertReparaturlaufDurchfuehren()
        {
            LadeErinnerungsmodellFallsNoetig();

            string sicherungsPfad = Path.Combine(
                OrdnerPfad,
                "erinnerungsmodell_sicherung_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");

            try
            {
                if (File.Exists(ErinnerungsmodellDateiPfad))
                {
                    File.Copy(ErinnerungsmodellDateiPfad, sicherungsPfad, overwrite: false);
                }
            }
            catch (Exception ex)
            {
                return "⚠ Sicherung konnte nicht erstellt werden, Reparaturlauf wurde deshalb NICHT gestartet: " + ex.Message;
            }

            int geprueft = 0;
            int geaendert = 0;
            int nichtLesbar = 0;

            foreach (Erinnerung erinnerung in erinnerungsmodellErinnerungen)
            {
                string pfad = erinnerung.Fundorte?
                    .Select(f => f.Pfad)
                    .FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));

                if (pfad == null)
                {
                    continue;
                }

                geprueft++;

                try
                {
                    string neuerHashwert = BerechneHashwert(pfad);

                    if (neuerHashwert != erinnerung.Hashwert)
                    {
                        erinnerung.Hashwert = neuerHashwert;
                        geaendert++;
                    }
                }
                catch
                {
                    nichtLesbar++;
                }
            }

            bool gespeichertVerifiziert = SpeichereErinnerungsmodell();

            // A's Punkt 6: nach dem Lauf pruefen, ob Erinnerung->Hash->
            // Sehgedaechtnis->Suchbegriff jetzt tatsaechlich zusammenpasst -
            // nutzt die bereits bestehende, bewaehrte Diagnose-Methode.
            string sehzentrumPruefung = PruefeSehzentrumBestand();

            return "Hashwert-Reparaturlauf abgeschlossen.\n\n" +
                "Sicherung vor dem Lauf erstellt unter:\n" + sicherungsPfad + "\n\n" +
                geprueft + " Erinnerung(en) mit vorhandener Datei geprüft, " + geaendert + " Hashwert(e) korrigiert" +
                (nichtLesbar > 0 ? ", " + nichtLesbar + " Datei(en) nicht lesbar (übersprungen, nichts verändert)" : "") + ".\n\n" +
                (gespeichertVerifiziert ? "✓ Ergebnis gespeichert und verifiziert.\n\n" : "⚠ Speichern konnte nicht verifiziert werden - bitte prüfen.\n\n") +
                sehzentrumPruefung;
        }

        private void HashwertReparaturlauf_Click(object sender, RoutedEventArgs e)
        {
            bool bestaetigt = James.FrageJaNein(
                "Der Hashwert-Reparaturlauf berechnet den Hashwert aller bestehenden Erinnerungen mit der jetzt einheitlichen Methode neu (behebt den fruehren Formatunterschied zum Sehzentrum).\n\n" +
                "Es wird vorher automatisch eine Sicherung erstellt. Originaldateien werden dabei nicht gelesen zum Verändern, nur zum Berechnen des Fingerabdrucks - nichts wird gelöscht oder verschoben.\n\n" +
                "Jetzt starten?",
                James.TitelEntscheidung);

            if (!bestaetigt)
            {
                return;
            }

            string ergebnis = HashwertReparaturlaufDurchfuehren();
            James.Hinweis(ergebnis, "Hashwert-Reparaturlauf");
        }

        private bool SpeichereErinnerungsmodell()
        {
            try
            {
                ArchivErinnerungsDaten daten = new ArchivErinnerungsDaten
                {
                    Erinnerungen = erinnerungsmodellErinnerungen,
                    Zuordnungen = erinnerungsmodellZuordnungen,
                    ZuordnungenPapierkorb = erinnerungsmodellZuordnungenPapierkorb
                };

                JsonSerializerOptions optionen = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(daten, optionen);

                File.WriteAllText(ErinnerungsmodellDateiPfad, json);

                string rueckgelesen = File.ReadAllText(ErinnerungsmodellDateiPfad);
                ArchivErinnerungsDaten kontrolle = JsonSerializer.Deserialize<ArchivErinnerungsDaten>(rueckgelesen);

                return kontrolle != null
                    && kontrolle.Erinnerungen != null
                    && kontrolle.Zuordnungen != null
                    && kontrolle.Erinnerungen.Count == erinnerungsmodellErinnerungen.Count
                    && kontrolle.Zuordnungen.Count == erinnerungsmodellZuordnungen.Count;
            }
            catch (Exception ex)
            {
                James.Problem("Das neue Erinnerungsmodell konnte nicht gespeichert werden: " + ex.Message + "\n\npersonen.json ist davon nicht betroffen.");
                return false;
            }
        }

        // ============================================================
        // A/Opa-OPTIMIERUNGSAUFTRAG "Opa-freundliches James" (16.08.), TEIL A
        // ============================================================
        // EIN gemeinsamer Papierkorb ueber die vier bisherigen, technisch
        // getrennten Listen (Person/Ereignis/Sammlung/Zuordnung) - "gemeinsame
        // Oberflaeche statt Datenumbau" (A's ausdrueckliche Vorgabe). Diese
        // Uebersicht LIEST nur aus den vier bestehenden Listen und ruft bei
        // Wiederherstellen/Endgueltig-Loeschen die JEWEILS BEREITS
        // BESTEHENDE, typspezifische Logik auf (Wiederherstellen_Click/
        // EndgueltigLoeschen_Click fuer Person, FreiesEreignisWiederherstellen_
        // Click/FreiesEreignisEndgueltigLoeschen_Click fuer Ereignis,
        // SammlungWiederherstellen_Click/SammlungEndgueltigLoeschen_Click fuer
        // Sammlung, JamesZuordnungWiederherstellen_Click/
        // JamesZuordnungEndgueltigLoeschen_Click fuer Zuordnung) - dafuer wird
        // die Auswahl kurz in die jeweils zustaendige, bestehende ListBox
        // uebertragen und deren vorhandener Click-Handler direkt aufgerufen.
        // Keine neue Wiederherstellen-/Loesch-Logik, keine Aenderung an den
        // vier bestehenden, weiterhin unveraendert sichtbaren Bereichen.
        private class GemeinsamerPapierkorbEintrag
        {
            public string Typ;
            public string Name;
            public string Herkunft;
            public object Referenz;
        }

        private List<GemeinsamerPapierkorbEintrag> ErmittleGemeinsamePapierkorbEintraege()
        {
            List<GemeinsamerPapierkorbEintrag> ergebnis = new List<GemeinsamerPapierkorbEintrag>();

            foreach (Person person in PapierkorbListe.Items.OfType<Person>())
            {
                ergebnis.Add(new GemeinsamerPapierkorbEintrag { Typ = "Person", Name = person.ToString(), Herkunft = "", Referenz = person });
            }

            foreach (Ereignis ereignis in freieEreignissePapierkorb)
            {
                ergebnis.Add(new GemeinsamerPapierkorbEintrag { Typ = "Ereignis", Name = ereignis.Titel, Herkunft = "", Referenz = ereignis });
            }

            foreach (Sammlung sammlung in sammlungenPapierkorb)
            {
                ergebnis.Add(new GemeinsamerPapierkorbEintrag { Typ = "Sammlung", Name = sammlung.Titel, Herkunft = "", Referenz = sammlung });
            }

            foreach (Zuordnung zuordnung in erinnerungsmodellZuordnungenPapierkorb)
            {
                Erinnerung erinnerung = erinnerungsmodellErinnerungen.FirstOrDefault(er => er.Id == zuordnung.ErinnerungId);
                string name = erinnerung != null && erinnerung.Fundorte.Count > 0
                    ? Path.GetFileName(erinnerung.Fundorte[0].Pfad)
                    : "(Erinnerung nicht mehr auffindbar)";

                ergebnis.Add(new GemeinsamerPapierkorbEintrag
                {
                    Typ = "Erinnerung",
                    Name = name,
                    Herkunft = "war zugeordnet zu " + zuordnung.ZielTyp + ": " + zuordnung.ZielBezeichnung,
                    Referenz = zuordnung
                });
            }

            return ergebnis;
        }

        private void AktualisiereGemeinsamePapierkorbUebersicht()
        {
            if (GemeinsamerPapierkorbListe == null)
            {
                return;
            }

            List<GemeinsamerPapierkorbEintrag> eintraege = ErmittleGemeinsamePapierkorbEintraege();

            GemeinsamerPapierkorbListe.Items.Clear();

            foreach (GemeinsamerPapierkorbEintrag eintrag in eintraege)
            {
                string symbol = eintrag.Typ switch
                {
                    "Person" => "👤",
                    "Ereignis" => "📅",
                    "Sammlung" => "🗂️",
                    _ => "🖼️"
                };

                Border kachel = new Border
                {
                    Width = 230,
                    MinHeight = 64,
                    Margin = new Thickness(4),
                    Padding = new Thickness(8),
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Background = Brushes.WhiteSmoke,
                    Tag = eintrag
                };

                StackPanel inhalt = new StackPanel();

                inhalt.Children.Add(new TextBlock { Text = symbol + " " + eintrag.Typ, FontWeight = FontWeights.Bold, FontSize = 11 });
                inhalt.Children.Add(new TextBlock { Text = eintrag.Name, TextWrapping = TextWrapping.Wrap, MaxWidth = 210 });

                if (!string.IsNullOrEmpty(eintrag.Herkunft))
                {
                    inhalt.Children.Add(new TextBlock { Text = eintrag.Herkunft, TextWrapping = TextWrapping.Wrap, MaxWidth = 210, Foreground = Brushes.Gray, FontSize = 11 });
                }

                kachel.Child = inhalt;
                GemeinsamerPapierkorbListe.Items.Add(kachel);
            }

            GemeinsamerPapierkorbAnzahlText.Text = eintraege.Count == 0
                ? "Der Papierkorb ist leer."
                : eintraege.Count + " Eintrag/Einträge im Papierkorb:";

            GemeinsamerPapierkorbWiederherstellenButton.IsEnabled = false;
            GemeinsamerPapierkorbEndgueltigLoeschenButton.IsEnabled = false;
        }

        private void GemeinsamerPapierkorbListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int anzahl = GemeinsamerPapierkorbListe.SelectedItems.Count;
            GemeinsamerPapierkorbWiederherstellenButton.IsEnabled = anzahl > 0;
            GemeinsamerPapierkorbEndgueltigLoeschenButton.IsEnabled = anzahl > 0;
        }

        // Ermittelt, welche der ausgewaehlten Kacheln zu welchem Typ gehoeren.
        private void GruppiereGemeinsamePapierkorbAuswahl(
            out List<Person> personen, out List<Ereignis> ereignisse,
            out List<Sammlung> sammlungenListe, out List<Zuordnung> zuordnungenListe)
        {
            List<GemeinsamerPapierkorbEintrag> ausgewaehlt = GemeinsamerPapierkorbListe.SelectedItems
                .Cast<Border>()
                .Select(b => b.Tag as GemeinsamerPapierkorbEintrag)
                .Where(x => x != null)
                .ToList();

            personen = ausgewaehlt.Where(x => x.Typ == "Person").Select(x => (Person)x.Referenz).ToList();
            ereignisse = ausgewaehlt.Where(x => x.Typ == "Ereignis").Select(x => (Ereignis)x.Referenz).ToList();
            sammlungenListe = ausgewaehlt.Where(x => x.Typ == "Sammlung").Select(x => (Sammlung)x.Referenz).ToList();
            zuordnungenListe = ausgewaehlt.Where(x => x.Typ == "Erinnerung").Select(x => (Zuordnung)x.Referenz).ToList();
        }

        private void GemeinsamerPapierkorbWiederherstellen_Click(object sender, RoutedEventArgs e)
        {
            GruppiereGemeinsamePapierkorbAuswahl(out List<Person> personen, out List<Ereignis> ereignisse, out List<Sammlung> sammlungenListe, out List<Zuordnung> zuordnungenListe);

            if (personen.Count > 0)
            {
                PapierkorbListe.SelectedItems.Clear();
                foreach (Person p in personen) { PapierkorbListe.SelectedItems.Add(p); }
                Wiederherstellen_Click(sender, e);
            }

            if (ereignisse.Count > 0)
            {
                FreieEreignissePapierkorbListe.SelectedItems.Clear();
                foreach (Ereignis ev in ereignisse) { FreieEreignissePapierkorbListe.SelectedItems.Add(ev); }
                FreiesEreignisWiederherstellen_Click(sender, e);
            }

            if (sammlungenListe.Count > 0)
            {
                SammlungenPapierkorbListe.SelectedItems.Clear();
                foreach (Sammlung s in sammlungenListe) { SammlungenPapierkorbListe.SelectedItems.Add(s); }
                SammlungWiederherstellen_Click(sender, e);
            }

            if (zuordnungenListe.Count > 0)
            {
                JamesGeloesteZuordnungenListe.SelectedItems.Clear();

                foreach (object element in JamesGeloesteZuordnungenListe.Items)
                {
                    if (element is Border b && b.Tag is Zuordnung z && zuordnungenListe.Contains(z))
                    {
                        JamesGeloesteZuordnungenListe.SelectedItems.Add(b);
                    }
                }

                JamesZuordnungWiederherstellen_Click(sender, e);
            }

            AktualisiereGemeinsamePapierkorbUebersicht();
        }

        private void GemeinsamerPapierkorbEndgueltigLoeschen_Click(object sender, RoutedEventArgs e)
        {
            GruppiereGemeinsamePapierkorbAuswahl(out List<Person> personen, out List<Ereignis> ereignisse, out List<Sammlung> sammlungenListe, out List<Zuordnung> zuordnungenListe);

            if (personen.Count > 0)
            {
                PapierkorbListe.SelectedItems.Clear();
                foreach (Person p in personen) { PapierkorbListe.SelectedItems.Add(p); }
                EndgueltigLoeschen_Click(sender, e);
            }

            if (ereignisse.Count > 0)
            {
                FreieEreignissePapierkorbListe.SelectedItems.Clear();
                foreach (Ereignis ev in ereignisse) { FreieEreignissePapierkorbListe.SelectedItems.Add(ev); }
                FreiesEreignisEndgueltigLoeschen_Click(sender, e);
            }

            if (sammlungenListe.Count > 0)
            {
                SammlungenPapierkorbListe.SelectedItems.Clear();
                foreach (Sammlung s in sammlungenListe) { SammlungenPapierkorbListe.SelectedItems.Add(s); }
                SammlungEndgueltigLoeschen_Click(sender, e);
            }

            if (zuordnungenListe.Count > 0)
            {
                JamesGeloesteZuordnungenListe.SelectedItems.Clear();

                foreach (object element in JamesGeloesteZuordnungenListe.Items)
                {
                    if (element is Border b && b.Tag is Zuordnung z && zuordnungenListe.Contains(z))
                    {
                        JamesGeloesteZuordnungenListe.SelectedItems.Add(b);
                    }
                }

                JamesZuordnungEndgueltigLoeschen_Click(sender, e);
            }

            AktualisiereGemeinsamePapierkorbUebersicht();
        }
    }
}
