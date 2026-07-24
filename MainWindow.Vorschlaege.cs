using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow
    {
        // ============================================================
        // BUILD 1.7: JAMES MACHT VORSCHLÄGE
        // ============================================================
        // Architekturbeschluss 010: James wartet nicht mehr nur darauf,
        // dass gefragt wird - er macht von sich aus freundliche
        // Vorschläge, ausschließlich aus bereits vorhandenen Daten
        // (keine KI, keine automatische Bewertung). WICHTIG: James
        // entscheidet dabei niemals etwas - er schlägt nur vor, der
        // Bereich lässt sich jederzeit über "Schließen" ausblenden und
        // erscheint als ruhiger Bereich oberhalb der Suche, niemals als
        // Popup.
        //
        // Bewusst einfache Umsetzung: nur Personen auf dem Schreibtisch
        // werden berücksichtigt (nicht das Archiv), damit die Vorschläge
        // sich auf das beziehen, womit gerade gearbeitet wird. Die
        // Kategorien sind in einer festen Rangfolge sortiert und jeweils
        // auf wenige Einträge begrenzt, damit der Bereich klein und
        // ruhig bleibt statt lang und aufdringlich zu werden.

        private const int VorschlaegeMaxGesamt = 4;
        private const int VorschlaegeMaxProKategorie = 2;

        // Build 6.0: der aktuell auf der Startseite gezeigte Vorschlag -
        // gemerkt, damit "Vorschlag annehmen" weiß, wohin es springen soll.
        private Vorschlag aktuellerStartseitenVorschlag = null;

        // Build 6.0: zeigt genau EINEN Vorschlag direkt auf der Startseite,
        // statt der bisherigen Liste im oberen Banner (Build 1.7). Nutzt
        // dieselbe, unveränderte Vorschlagslogik (ErstelleVorschlaege) -
        // James erzeugt dadurch weiterhin keine eigenen Ereignisse, er
        // zeigt lediglich, worum sich der Benutzer selbst schon gekümmert
        // hat. Gibt es nichts vorzuschlagen, bleibt der Bereich schlicht
        // ausgeblendet (kein erzwungener Vorschlag).
        private void ZeigeStartseiteVorschlag()
        {
            List<Vorschlag> vorschlaege = ErstelleVorschlaege();

            if (vorschlaege.Count == 0)
            {
                aktuellerStartseitenVorschlag = null;
                StartseiteVorschlagPanel.Visibility = Visibility.Collapsed;
                return;
            }

            aktuellerStartseitenVorschlag = vorschlaege[0];

            StartseiteVorschlagGrussText.Text = James.StartseiteVorschlagGruss();
            StartseiteVorschlagText.Text = aktuellerStartseitenVorschlag.Text;
            StartseiteVorschlagPanel.Visibility = Visibility.Visible;
        }

        private void VorschlagAnnehmen_Click(object sender, RoutedEventArgs e)
        {
            if (aktuellerStartseitenVorschlag == null)
            {
                return;
            }

            OeffneVorschlag(aktuellerStartseitenVorschlag);
        }

        // Build 6.0: die eigentliche Sprunglogik - herausgelöst aus
        // VorschlaegeListe_SelectionChanged (Build 1.7), damit sowohl der
        // neue "Vorschlag annehmen"-Button als auch (falls später wieder
        // benötigt) die ursprüngliche Vorschlagsliste denselben, bewährten
        // Weg nutzen. Verlässt dabei zusätzlich die Startseite und zeigt
        // den Personen-Bereich, falls der Vorschlag zu einer Person/einem
        // Ereignis führt.
        private void OeffneVorschlag(Vorschlag vorschlag)
        {
            if (vorschlag.Ereignis != null && vorschlag.Person != null)
            {
                SucheTextBox.Text = "";

                HauptTabControl.SelectedIndex = 0;

                StartseiteBereich.Visibility = Visibility.Collapsed;
                EreignisBereich.Visibility = Visibility.Collapsed;
                PersonenFormularBereich.Visibility = Visibility.Visible;
                PersonenListeBereich.Visibility = Visibility.Visible;

                PersonenListe.SelectedItem = vorschlag.Person;
                EreignisseListe.SelectedItem = vorschlag.Ereignis;
            }
            else
            {
                HauptTabControl.SelectedIndex = vorschlag.ZielTabIndex ?? 5;
            }
        }

        private List<Vorschlag> ErstelleVorschlaege()
        {
            List<Vorschlag> ergebnis = new List<Vorschlag>();

            ErgaenzeVorschlaegeHeuteVor(ergebnis);
            ErgaenzeVorschlaegeJahreszeit(ergebnis);
            ErgaenzeVorschlaegeFavorit(ergebnis);
            ErgaenzeVorschlaegeUnvollstaendig(ergebnis);
            ErgaenzeVorschlagUnzugeordneteDateien(ergebnis);
            ErgaenzeVorschlagEinstellungenKennenlernen(ergebnis);

            return ergebnis;
        }

        // Build 2.0: Solange James seinen Besitzer noch gar nicht kennt
        // (einstellungen.json existiert noch nicht), lädt er einmalig
        // freundlich dazu ein - ganz ohne Zwang, wie ein Vorschlag unter
        // vielen, jederzeit ignorierbar. Sobald einmal gespeichert wurde
        // (auch mit lauter übersprungenen Fragen), erscheint der Hinweis
        // nicht mehr.
        private void ErgaenzeVorschlagEinstellungenKennenlernen(List<Vorschlag> ergebnis)
        {
            if (ergebnis.Count >= VorschlaegeMaxGesamt)
            {
                return;
            }

            if (File.Exists(EinstellungenPfad))
            {
                return;
            }

            ergebnis.Add(new Vorschlag
            {
                Text = James.VorschlagEinstellungenKennenlernen(),
                Person = null,
                Ereignis = null,
                ZielTabIndex = EinstellungenTabIndex
            });
        }

        // Variante 3 aus dem Architekturbeschluss: "Heute vor X Jahren".
        // Das Datumsfeld ist bewusst freier Text (siehe Notiz weiter oben
        // im Code, "Ereignis.Datum später von string auf DateTime
        // umstellen") - deshalb wird hier nur ein Versuch unternommen,
        // gängige Formate zu erkennen. Gelingt das nicht, wird dieses
        // Ereignis für diese Kategorie einfach übersprungen, statt einen
        // Fehler zu zeigen.
        private void ErgaenzeVorschlaegeHeuteVor(List<Vorschlag> ergebnis)
        {
            int hinzugefuegt = 0;

            foreach (Person person in allePersonen)
            {
                if (person.Ereignisse == null)
                {
                    continue;
                }

                foreach (Ereignis ereignis in person.Ereignisse)
                {
                    if (ergebnis.Count >= VorschlaegeMaxGesamt || hinzugefuegt >= VorschlaegeMaxProKategorie)
                    {
                        return;
                    }

                    DateTime datum;

                    if (VersucheDatumZuLesen(ereignis.Datum, out datum) &&
                        datum.Day == DateTime.Now.Day &&
                        datum.Month == DateTime.Now.Month &&
                        datum.Year < DateTime.Now.Year)
                    {
                        int jahre = DateTime.Now.Year - datum.Year;

                        ergebnis.Add(new Vorschlag
                        {
                            Text = James.VorschlagHeuteVor(jahre, ereignis.Titel, person.ToString()),
                            Person = person,
                            Ereignis = ereignis
                        });

                        hinzugefuegt++;
                    }
                }
            }
        }

        private static bool VersucheDatumZuLesen(string text, out DateTime ergebnis)
        {
            ergebnis = default;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            string[] bekannteFormate = { "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy" };
            CultureInfo deutsch = CultureInfo.GetCultureInfo("de-DE");

            if (DateTime.TryParseExact(text.Trim(), bekannteFormate, deutsch, DateTimeStyles.None, out ergebnis))
            {
                return true;
            }

            return DateTime.TryParse(text.Trim(), deutsch, DateTimeStyles.None, out ergebnis);
        }

        // Variante 2: Ereignisse, deren Jahreszeit zur aktuellen
        // Jahreszeit passt.
        private void ErgaenzeVorschlaegeJahreszeit(List<Vorschlag> ergebnis)
        {
            string aktuelleJahreszeit = ErmittleAktuelleJahreszeit();
            int hinzugefuegt = 0;

            foreach (Person person in allePersonen)
            {
                if (person.Ereignisse == null)
                {
                    continue;
                }

                foreach (Ereignis ereignis in person.Ereignisse)
                {
                    if (ergebnis.Count >= VorschlaegeMaxGesamt || hinzugefuegt >= VorschlaegeMaxProKategorie)
                    {
                        return;
                    }

                    if (ereignis.Jahreszeit == aktuelleJahreszeit)
                    {
                        ergebnis.Add(new Vorschlag
                        {
                            Text = James.VorschlagJahreszeit(aktuelleJahreszeit, ereignis.Titel, person.ToString()),
                            Person = person,
                            Ereignis = ereignis
                        });

                        hinzugefuegt++;
                    }
                }
            }
        }

        private static string ErmittleAktuelleJahreszeit()
        {
            int monat = DateTime.Now.Month;

            if (monat == 12 || monat <= 2)
            {
                return "Winter";
            }

            if (monat <= 5)
            {
                return "Frühling";
            }

            if (monat <= 8)
            {
                return "Sommer";
            }

            return "Herbst";
        }

        // Variante 1: Ereignisse mit der Bewertung "Favorit".
        private void ErgaenzeVorschlaegeFavorit(List<Vorschlag> ergebnis)
        {
            int hinzugefuegt = 0;

            foreach (Person person in allePersonen)
            {
                if (person.Ereignisse == null)
                {
                    continue;
                }

                foreach (Ereignis ereignis in person.Ereignisse)
                {
                    if (ergebnis.Count >= VorschlaegeMaxGesamt || hinzugefuegt >= VorschlaegeMaxProKategorie)
                    {
                        return;
                    }

                    if (ereignis.Bewertungen != null && ereignis.Bewertungen.Contains("Favorit"))
                    {
                        ergebnis.Add(new Vorschlag
                        {
                            Text = James.VorschlagFavorit(ereignis.Titel, person.ToString()),
                            Person = person,
                            Ereignis = ereignis
                        });

                        hinzugefuegt++;
                    }
                }
            }
        }

        // Variante 4: Ereignisse ohne eigenes Foto.
        private void ErgaenzeVorschlaegeUnvollstaendig(List<Vorschlag> ergebnis)
        {
            int hinzugefuegt = 0;

            foreach (Person person in allePersonen)
            {
                if (person.Ereignisse == null)
                {
                    continue;
                }

                foreach (Ereignis ereignis in person.Ereignisse)
                {
                    if (ergebnis.Count >= VorschlaegeMaxGesamt || hinzugefuegt >= VorschlaegeMaxProKategorie)
                    {
                        return;
                    }

                    if (string.IsNullOrEmpty(ereignis.EreignisFotoDateiname))
                    {
                        ergebnis.Add(new Vorschlag
                        {
                            Text = James.VorschlagUnvollstaendig(ereignis.Titel, person.ToString()),
                            Person = person,
                            Ereignis = ereignis
                        });

                        hinzugefuegt++;
                    }
                }
            }
        }

        // Variante 5 (vereinfacht): Hinweis auf Bilder im
        // Erinnerungsverzeichnis (Build 0.4/1.1). Wichtig: es wird noch
        // NICHT geprüft, ob diese Bilder tatsächlich bereits einer Person
        // zugeordnet sind - dafür gibt es noch keine Verknüpfung zwischen
        // Erinnerungsverzeichnis und Personen/Ereignissen. Der Text ist
        // deshalb bewusst zurückhaltend formuliert (ein Hinweis auf
        // vorhandenes Material, keine Behauptung über den Zuordnungsstatus).
        private void ErgaenzeVorschlagUnzugeordneteDateien(List<Vorschlag> ergebnis)
        {
            if (ergebnis.Count >= VorschlaegeMaxGesamt)
            {
                return;
            }

            if (!File.Exists(ErinnerungsVerzeichnisPfad))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(ErinnerungsVerzeichnisPfad);
                ErinnerungsVerzeichnis verzeichnis = JsonSerializer.Deserialize<ErinnerungsVerzeichnis>(json);

                int anzahlBilder = verzeichnis != null && verzeichnis.Dateien != null
                    ? verzeichnis.Dateien.Count(d => d.Dateityp == "Bilder")
                    : 0;

                if (anzahlBilder > 0)
                {
                    ergebnis.Add(new Vorschlag
                    {
                        Text = James.VorschlagUnzugeordneteDateien(anzahlBilder),
                        Person = null,
                        Ereignis = null
                    });
                }
            }
            catch
            {
                // Erinnerungsverzeichnis nicht lesbar - dann eben kein
                // Vorschlag dieser Art, kein Fehlerdialog noetig.
            }
        }

        private void VorschlaegeListe_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Vorschlag vorschlag = VorschlaegeListe.SelectedItem as Vorschlag;

            if (vorschlag == null)
            {
                return;
            }

            OeffneVorschlag(vorschlag);

            // Auswahl zurücksetzen, damit ein erneutes Anklicken desselben
            // Vorschlags wieder funktioniert (sonst feuert SelectionChanged
            // beim zweiten Klick auf denselben Eintrag nicht erneut).
            VorschlaegeListe.SelectedItem = null;
        }

        private void VorschlaegeSchliessen_Click(object sender, RoutedEventArgs e)
        {
            VorschlaegePanel.Visibility = Visibility.Collapsed;
        }
    }
}