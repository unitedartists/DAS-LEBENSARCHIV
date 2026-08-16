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
        private void ArbeitsmappeMitEreignisVerbinden_Click(object sender, RoutedEventArgs e)
        {
            if (ErmittleMarkierteGruenBereichErinnerungIds().Count == 0)
            {
                James.Hinweis("Bitte zuerst im grünen Bereich markieren, welche Erinnerung(en) betroffen sein sollen.");
                return;
            }

            List<Person> alleAuswaehlbarenPersonen = allePersonen
                .Concat(ArchivListe.Items.Cast<Person>())
                .ToList();

            ArbeitsmappePersonComboBox.ItemsSource = alleAuswaehlbarenPersonen;
            ArbeitsmappePersonComboBox.SelectedIndex = -1;
            ArbeitsmappeEreignisComboBox.ItemsSource = null;

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeEreignisAuswahlPanel.Visibility = Visibility.Visible;
        }

        private void ArbeitsmappePersonComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Person person = ArbeitsmappePersonComboBox.SelectedItem as Person;
            ArbeitsmappeEreignisComboBox.ItemsSource = person != null ? person.Ereignisse : null;

            if (person != null)
            {
                int anzahlEreignisse = person.Ereignisse != null ? person.Ereignisse.Count : 0;
                ArbeitsmappeStatusText.Text = James.DiagnosePersonAusgewaehltFuerEreignis(person.ToString(), anzahlEreignisse);
            }
        }

        // A/Opa-BAUAUFTRAG "AM: RECHTE AKTIONSLEISTE AUF DAS NEUE MODELL
        // UMSTELLEN" (16.08.), Weg 2: statt physischer Kopie via
        // VerknuepfeArbeitsmappenDateienMitEreignis (alt) jetzt
        // FuehreZuordnungDurch (neues Modell) - keine Kopie der
        // Originaldatei, keine Aenderung an alten Ereignis-Foto-Feldern.
        // Betrifft ausschliesslich die im gruenen Bereich markierten
        // Erinnerungen (ErmittleMarkierteGruenBereichErinnerungIds).
        private void ArbeitsmappeEreignisBestaetigen_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArbeitsmappePersonComboBox.SelectedItem as Person;
            Ereignis ereignis = ArbeitsmappeEreignisComboBox.SelectedItem as Ereignis;

            if (person == null || ereignis == null)
            {
                James.Hinweis(James.BittePersonUndEreignisAuswaehlen);
                return;
            }

            List<Guid> markiert = ErmittleMarkierteGruenBereichErinnerungIds();

            if (markiert.Count == 0)
            {
                James.Hinweis("Bitte zuerst im grünen Bereich markieren, welche Erinnerung(en) zugeordnet werden sollen.");
                return;
            }

            bool gespeichertVerifiziert = FuehreZuordnungDurch(markiert, ZuordnungsZielTyp.Ereignis, ereignis.Id, ereignis.Titel, out int anzahlBereitsVorhanden);

            int anzahlNeu = markiert.Count - anzahlBereitsVorhanden;

            ArbeitsmappeStatusText.Text = gespeichertVerifiziert
                ? "✓ " + anzahlNeu + " Erinnerung(en) dem Ereignis \"" + ereignis.Titel + "\" zugeordnet." + (anzahlBereitsVorhanden > 0 ? " (" + anzahlBereitsVorhanden + " war(en) bereits zugeordnet.)" : "")
                : "⚠ Zuordnung angelegt, aber Speichern konnte nicht verifiziert werden - bitte prüfen.";

            // A/Opa-REPARATURAUFTRAG "AM TEST 3" (16.08.): siehe ArbeitsmappeTitelbildBestaetigen_Click.
            if (gespeichertVerifiziert)
            {
                James.Hinweis((markiert.Count == 1 ? "1 Erinnerung wurde " : markiert.Count + " Erinnerungen wurden ") + "dem Ereignis \"" + ereignis.Titel + "\" zugeordnet." +
                    (anzahlBereitsVorhanden > 0 ? " (" + anzahlBereitsVorhanden + " war(en) bereits zugeordnet und wurde(n) übersprungen.)" : ""));
            }

            ArbeitsmappeEreignisAuswahlPanel.Visibility = Visibility.Collapsed;
            AktualisiereAmDirekteAuswahlListe();
        }

        private int VerbindeDateienMitEreignis(Ereignis ereignis, List<string> pfade, string zielOrdner)
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

                if (string.IsNullOrEmpty(ereignis.EreignisFotoDateiname))
                {
                    ereignis.EreignisFotoDateiname = neuerDateiname;
                }
                else
                {
                    if (ereignis.WeitereFotoDateinamen == null)
                    {
                        ereignis.WeitereFotoDateinamen = new List<string>();
                    }

                    ereignis.WeitereFotoDateinamen.Add(neuerDateiname);
                }

                // Punkt 3 (Optimierung nach Test 2): Markierung bleibt
                // bestehen, damit dieselben markierten Erinnerungen im
                // selben Arbeitsgang zusätzlich auch einer Person und/oder
                // einer Sammlung zugeordnet werden können. Nur der Button
                // "Markierung aufheben" löscht die Markierung noch aktiv.
                arbeitsmappeBereitsZugeordnet.Add(pfad);
                verbunden++;
            }

            if (verbunden > 0)
            {
                ereignis.ModifiedAt = DateTime.Now;
            }

            return verbunden;
        }

        private void VerknuepfeArbeitsmappenDateienMitEreignis(Person person, Ereignis ereignis, List<string> pfade)
        {
            if (person == null || ereignis == null || pfade == null || pfade.Count == 0)
            {
                return;
            }

            try
            {
                int verbunden = VerbindeDateienMitEreignis(ereignis, pfade, ErinnerungsOrdnerFuer(person, ereignis));

                if (verbunden > 0)
                {
                    person.ModifiedAt = DateTime.Now;
                    SpeichereDaten();
                    SpeichereArbeitsmappeZugeordnet();
                }

                if (verbunden == 0)
                {
                    James.Hinweis(James.ArbeitsmappeNurBilder);
                }
                else
                {
                    int gesamtErinnerungen = ZaehleErinnerungenDerPerson(person);

                    ArbeitsmappeStatusText.Text = verbunden == 1
                        ? James.ArbeitsmappeVerbunden(ereignis.Titel, person.ToString(), gesamtErinnerungen)
                        : James.ArbeitsmappeVerbundenMehrere(verbunden, ereignis.Titel, person.ToString(), gesamtErinnerungen);

                    ZeigeArbeitsmappeEreignisOeffnenButton(person, ereignis);
                }

                if (verbunden > 0)
                {
                    AktualisiereErinnerungskarteFallsBetroffen(person, ereignis);
                    AktualisiereSchreibtischFallsBetroffen(person);
                }
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimSpeichernFoto(ex.Message));
            }
        }

        private void ArbeitsmappeNeuesEreignisAnlegen_Click(object sender, RoutedEventArgs e)
        {
            if (ErmittleMarkierteGruenBereichErinnerungIds().Count == 0)
            {
                James.Hinweis("Bitte zuerst im grünen Bereich markieren, welche Erinnerung(en) betroffen sein sollen.");
                return;
            }

            if (allePersonen.Count == 0)
            {
                James.Hinweis(James.ArbeitsmappeBittePersonZuerst);
                return;
            }

            ArbeitsmappeNeuesEreignisPersonComboBox.ItemsSource = allePersonen;
            ArbeitsmappeNeuesEreignisPersonComboBox.SelectedIndex = -1;

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeNeuesEreignisPersonPanel.Visibility = Visibility.Visible;
        }

        private void ArbeitsmappeNeuesEreignisWeiter_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArbeitsmappeNeuesEreignisPersonComboBox.SelectedItem as Person;

            if (person == null)
            {
                James.Hinweis(James.ArbeitsmappeBitteEreignisPersonAuswaehlen);
                return;
            }

            arbeitsmappeNeuesEreignisPerson = person;

            ArbeitsmappeEreignisTitelTextBox.Clear();
            ArbeitsmappeEreignisBeschreibungTextBox.Clear();
            ArbeitsmappeEreignisDatumTextBox.Clear();
            ArbeitsmappeEreignisOrtTextBox.Clear();
            ArbeitsmappeEreignisJahreszeitComboBox.SelectedIndex = 0;
            ArbeitsmappeEreignisStichwoerterTextBox.Clear();
            ArbeitsmappeEreignisBemerkungenTextBox.Clear();
            ArbeitsmappeEreignisBeteiligteTextBox.Clear();

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeNeuesEreignisFormularPanel.Visibility = Visibility.Visible;
            ArbeitsmappeEreignisTitelTextBox.Focus();
        }

        private void ArbeitsmappeEreignisSpeichern_Click(object sender, RoutedEventArgs e)
        {
            Person person = arbeitsmappeNeuesEreignisPerson;

            if (person == null)
            {
                James.Hinweis(James.ArbeitsmappeBitteEreignisPersonAuswaehlen);
                return;
            }

            string titel = ArbeitsmappeEreignisTitelTextBox.Text.Trim();

            if (titel == "")
            {
                James.Hinweis(James.BitteEreignisTitelEingeben);
                return;
            }

            List<Guid> markiert = ErmittleMarkierteGruenBereichErinnerungIds();

            if (markiert.Count == 0)
            {
                James.Hinweis("Bitte zuerst im grünen Bereich markieren, welche Erinnerung(en) diesem neuen Ereignis zugeordnet werden sollen.");
                return;
            }

            string jahreszeit = "";
            ComboBoxItem ausgewaehlteJahreszeit = ArbeitsmappeEreignisJahreszeitComboBox.SelectedItem as ComboBoxItem;

            if (ausgewaehlteJahreszeit != null)
            {
                jahreszeit = ausgewaehlteJahreszeit.Content.ToString();
            }

            List<string> stichwoerter = ArbeitsmappeEreignisStichwoerterTextBox.Text
                .Split(',')
                .Select(teil => teil.Trim())
                .Where(teil => teil != "")
                .ToList();

            List<string> beteiligte = ArbeitsmappeEreignisBeteiligteTextBox.Text
                .Split(',')
                .Select(teil => teil.Trim())
                .Where(teil => teil != "")
                .ToList();

            Ereignis neuesEreignis = new Ereignis
            {
                Titel = titel,
                Beschreibung = ArbeitsmappeEreignisBeschreibungTextBox.Text.Trim(),
                Datum = ArbeitsmappeEreignisDatumTextBox.Text.Trim(),
                Ort = ArbeitsmappeEreignisOrtTextBox.Text.Trim(),
                Jahreszeit = jahreszeit,
                Stichwoerter = stichwoerter,
                Bemerkungen = ArbeitsmappeEreignisBemerkungenTextBox.Text.Trim(),
                Beteiligte = beteiligte
            };

            if (person.Ereignisse == null)
            {
                person.Ereignisse = new List<Ereignis>();
            }

            person.Ereignisse.Add(neuesEreignis);
            person.ModifiedAt = DateTime.Now;

            SpeichereDaten();

            // A/Opa-BAUAUFTRAG "AM: RECHTE AKTIONSLEISTE AUF DAS NEUE MODELL
            // UMSTELLEN" (16.08.), Weg 2: das ANLEGEN des Ereignisses selbst
            // bleibt (schreibt weiterhin personen.json - das ist die
            // eigentliche "neues Ereignis"-Funktion, keine automatische
            // AM-Zuordnung). Die ZUORDNUNG der markierten Erinnerungen zu
            // diesem Ereignis laeuft jetzt aber ueber FuehreZuordnungDurch
            // statt physischer Kopie.
            bool gespeichertVerifiziert = FuehreZuordnungDurch(markiert, ZuordnungsZielTyp.Ereignis, neuesEreignis.Id, neuesEreignis.Titel, out int anzahlBereitsVorhanden);

            int anzahlNeu = markiert.Count - anzahlBereitsVorhanden;

            ArbeitsmappeStatusText.Text = gespeichertVerifiziert
                ? "✓ Ereignis \"" + neuesEreignis.Titel + "\" angelegt, " + anzahlNeu + " Erinnerung(en) zugeordnet."
                : "⚠ Ereignis angelegt, aber Zuordnung konnte nicht verifiziert werden - bitte prüfen.";

            // A/Opa-REPARATURAUFTRAG "AM TEST 3" (16.08.): siehe ArbeitsmappeTitelbildBestaetigen_Click.
            if (gespeichertVerifiziert)
            {
                James.Hinweis("Ereignis \"" + neuesEreignis.Titel + "\" angelegt. " +
                    (anzahlNeu == 1 ? "1 Erinnerung wurde " : anzahlNeu + " Erinnerungen wurden ") + "zugeordnet.");
            }

            ZeigeArbeitsmappeEreignisOeffnenButton(person, neuesEreignis);

            arbeitsmappeNeuesEreignisPerson = null;

            ArbeitsmappeNeuesEreignisFormularPanel.Visibility = Visibility.Collapsed;

            AktualisiereAmDirekteAuswahlListe();
        }

        private void ArbeitsmappeNeuePersonAnlegen_Click(object sender, RoutedEventArgs e)
        {
            if (ErmittleMarkierteGruenBereichErinnerungIds().Count == 0)
            {
                James.Hinweis("Bitte zuerst im grünen Bereich markieren, welche Erinnerung(en) betroffen sein sollen.");
                return;
            }

            ArbeitsmappeNeuePersonVornameTextBox.Clear();
            ArbeitsmappeNeuePersonNachnameTextBox.Clear();
            ArbeitsmappeNeuePersonGeburtTextBox.Clear();
            ArbeitsmappeNeuePersonOrtTextBox.Clear();
            ArbeitsmappeNeuePersonBeziehungComboBox.Text = "";

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappeNeuePersonPanel.Visibility = Visibility.Visible;
            ArbeitsmappeNeuePersonVornameTextBox.Focus();
        }

        private void ArbeitsmappeNeuePersonSpeichern_Click(object sender, RoutedEventArgs e)
        {
            string vorname = ArbeitsmappeNeuePersonVornameTextBox.Text.Trim();
            string nachname = ArbeitsmappeNeuePersonNachnameTextBox.Text.Trim();

            if (vorname == "" && nachname == "")
            {
                James.Hinweis(James.BitteErstNamenEingeben);
                return;
            }

            List<Guid> markiert = ErmittleMarkierteGruenBereichErinnerungIds();

            if (markiert.Count == 0)
            {
                James.Hinweis("Bitte zuerst im grünen Bereich markieren, welche Erinnerung(en) dieser neuen Person zugeordnet werden sollen.");
                return;
            }

            string beziehungstext = ArbeitsmappeNeuePersonBeziehungComboBox.Text != null
                ? ArbeitsmappeNeuePersonBeziehungComboBox.Text.Trim()
                : "";

            Person neuePerson = new Person
            {
                Vorname = vorname,
                Nachname = nachname,
                Geburt = ArbeitsmappeNeuePersonGeburtTextBox.Text.Trim(),
                Ort = ArbeitsmappeNeuePersonOrtTextBox.Text.Trim(),
                Beziehung = beziehungstext == "" ? null : new Beziehung { Rolle = beziehungstext }
            };

            allePersonen.Add(neuePerson);

            SortiereAllePersonen();
            AktualisierePersonenAnzeige();

            SpeichereDaten();

            // A/Opa-BAUAUFTRAG "AM: RECHTE AKTIONSLEISTE AUF DAS NEUE MODELL
            // UMSTELLEN" (16.08.), Weg 2: das ANLEGEN der Person selbst
            // bleibt (schreibt weiterhin personen.json - das ist die
            // eigentliche "neue Person"-Funktion). Die ZUORDNUNG der
            // markierten Erinnerungen laeuft jetzt ueber FuehreZuordnungDurch
            // statt physischer Kopie in einen Personen-Ordner.
            bool gespeichertVerifiziert = FuehreZuordnungDurch(markiert, ZuordnungsZielTyp.Person, neuePerson.Id, neuePerson.ToString(), out int anzahlBereitsVorhanden);

            int anzahlNeu = markiert.Count - anzahlBereitsVorhanden;

            ArbeitsmappeStatusText.Text = gespeichertVerifiziert
                ? "✓ Person \"" + neuePerson.ToString() + "\" angelegt, " + anzahlNeu + " Erinnerung(en) zugeordnet."
                : "⚠ Person angelegt, aber Zuordnung konnte nicht verifiziert werden - bitte prüfen.";

            // A/Opa-REPARATURAUFTRAG "AM TEST 3" (16.08.): siehe ArbeitsmappeTitelbildBestaetigen_Click.
            if (gespeichertVerifiziert)
            {
                James.Hinweis("Person \"" + neuePerson.ToString() + "\" angelegt. " +
                    (anzahlNeu == 1 ? "1 Erinnerung wurde " : anzahlNeu + " Erinnerungen wurden ") + "zugeordnet.");
            }

            ArbeitsmappeNeuePersonPanel.Visibility = Visibility.Collapsed;

            AktualisiereAmDirekteAuswahlListe();
        }
        private void VersteckeAlleArbeitsmappenPanels()
        {
            ArbeitsmappeEreignisAuswahlPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappePersonAuswahlPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeNeuesEreignisPersonPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeNeuesEreignisFormularPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeNeuePersonPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeNeuesFreiesEreignisPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeFreiesEreignisAuswahlPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeNeueSammlungPanel.Visibility = Visibility.Collapsed;
            ArbeitsmappeSammlungAuswahlPanel.Visibility = Visibility.Collapsed;
        }

        private void ZeigeArbeitsmappeEreignisOeffnenButton(Person person, Ereignis ereignis)
        {
            arbeitsmappeLetztesEreignisPerson = person;
            arbeitsmappeLetztesEreignis = ereignis;
            ArbeitsmappeEreignisOeffnenButton.Visibility = Visibility.Visible;
        }

        private void ArbeitsmappeEreignisOeffnen_Click(object sender, RoutedEventArgs e)
        {
            if (arbeitsmappeLetztesEreignisPerson == null || arbeitsmappeLetztesEreignis == null)
            {
                return;
            }

            SucheTextBox.Text = "";

            HauptTabControl.SelectedIndex = 0;
            PersonenListe.SelectedItem = arbeitsmappeLetztesEreignisPerson;
            EreignisseListe.SelectedItem = arbeitsmappeLetztesEreignis;

            ArbeitsmappeEreignisOeffnenButton.Visibility = Visibility.Collapsed;
        }

        private void VerknuepfeArbeitsmappenDateienMitPerson(Person person, List<string> pfade)
        {
            if (person == null || pfade == null || pfade.Count == 0)
            {
                return;
            }

            int verbunden = 0;

            try
            {
                string zielOrdner = PersonErinnerungsOrdner(person);
                Directory.CreateDirectory(zielOrdner);

                if (person.ErinnerungsDateinamen == null)
                {
                    person.ErinnerungsDateinamen = new List<string>();
                }

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

                    person.ErinnerungsDateinamen.Add(neuerDateiname);

                    if (string.IsNullOrEmpty(person.TitelbildDateiname))
                    {
                        person.TitelbildDateiname = neuerDateiname;
                    }

                    // Punkt 3 (Optimierung nach Test 2): Markierung bleibt
                    // bestehen für gleichzeitige Zuordnung zu Ereignis/Sammlung.
                    arbeitsmappeBereitsZugeordnet.Add(pfad);
                    verbunden++;
                }

                if (verbunden == 0)
                {
                    James.Hinweis(James.ArbeitsmappeNurBilder);
                    return;
                }

                person.ModifiedAt = DateTime.Now;

                SpeichereDaten();
                SpeichereArbeitsmappeZugeordnet();

                int gesamtErinnerungen = ZaehleErinnerungenDerPerson(person);

                ArbeitsmappeStatusText.Text = verbunden == 1
                    ? James.ArbeitsmappeErinnerungZugeordnet(person.ToString(), gesamtErinnerungen)
                    : James.ArbeitsmappeErinnerungenZugeordnetMehrere(verbunden, person.ToString(), gesamtErinnerungen);

                AktualisiereSchreibtischFallsBetroffen(person);
                AktualisiereErinnerungskarteFallsBetroffen(person, null);
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimSpeichernFoto(ex.Message));
            }
        }

        private void ArbeitsmappePersonZuordnen_Click(object sender, RoutedEventArgs e)
        {
            if (ErmittleMarkierteGruenBereichErinnerungIds().Count == 0)
            {
                James.Hinweis("Bitte zuerst im grünen Bereich markieren, welche Erinnerung(en) betroffen sein sollen.");
                return;
            }

            ArbeitsmappeTitelbildPersonComboBox.ItemsSource = null;
            ArbeitsmappeTitelbildPersonComboBox.ItemsSource = allePersonen;

            VersteckeAlleArbeitsmappenPanels();
            ArbeitsmappePersonAuswahlPanel.Visibility = Visibility.Visible;
        }

        private void ArbeitsmappeTitelbildPersonComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            bool istAusgewaehlt = ArbeitsmappeTitelbildPersonComboBox.SelectedItem != null;

            ArbeitsmappeTitelbildBestaetigenButton.IsEnabled = istAusgewaehlt;
            ArbeitsmappePersonenAnsehenButton.IsEnabled = istAusgewaehlt;
            ArbeitsmappePersonenArchivierenButton.IsEnabled = istAusgewaehlt;
            ArbeitsmappePersonenInPapierkorbButton.IsEnabled = istAusgewaehlt;
        }

        // Neue Funktion (Generaltest 2, Wunsch von Oma+Opa): Mehrfachzuordnung -
        // die ausgewählten Erinnerungen werden in einem Arbeitsgang JEDER
        // markierten Person zugeordnet (z.B. eine Geburtsfotoserie
        // gleichzeitig an Vater, Mutter, Oma, Opa und Geschwister).
        //
        // A/Opa-BAUAUFTRAG "AM: RECHTE AKTIONSLEISTE AUF DAS NEUE MODELL
        // UMSTELLEN" (16.08.), Weg 2: statt physischer Kopie via
        // VerknuepfeArbeitsmappenDateienMitPerson (alt) jetzt
        // FuehreZuordnungDurch je ausgewaehlter Person (neues Modell) -
        // keine Kopie der Originaldatei, keine Aenderung an alten
        // Personen-Foto-Feldern.
        private void ArbeitsmappeTitelbildBestaetigen_Click(object sender, RoutedEventArgs e)
        {
            List<Person> ausgewaehltePersonen = ArbeitsmappeTitelbildPersonComboBox.SelectedItems.Cast<Person>().ToList();

            if (ausgewaehltePersonen.Count == 0)
            {
                James.Hinweis(James.BittePersonAuswaehlen);
                return;
            }

            List<Guid> markiert = ErmittleMarkierteGruenBereichErinnerungIds();

            if (markiert.Count == 0)
            {
                James.Hinweis("Bitte zuerst im grünen Bereich markieren, welche Erinnerung(en) zugeordnet werden sollen.");
                return;
            }

            int gesamtNeu = 0;
            int gesamtBereitsVorhanden = 0;

            foreach (Person person in ausgewaehltePersonen)
            {
                FuehreZuordnungDurch(markiert, ZuordnungsZielTyp.Person, person.Id, person.ToString(), out int anzahlBereitsVorhanden);
                gesamtNeu += markiert.Count - anzahlBereitsVorhanden;
                gesamtBereitsVorhanden += anzahlBereitsVorhanden;
            }

            ArbeitsmappeStatusText.Text = "✓ " + gesamtNeu + " Zuordnung(en) angelegt zu " + ausgewaehltePersonen.Count + " Person(en)." +
                (gesamtBereitsVorhanden > 0 ? " (" + gesamtBereitsVorhanden + " bereits vorhanden, übersprungen.)" : "");

            // A/Opa-REPARATURAUFTRAG "AM TEST 3" (16.08.): ArbeitsmappeStatusText
            // sitzt ganz unten in der langen rechten Aktionsleiste und blieb beim
            // Zuordnen ueber diesen Weg oft ausserhalb des sichtbaren Bereichs -
            // Opa sah dadurch keine verstaendliche Erfolgsmeldung. Zusaetzliches
            // Popup (dasselbe Muster wie ueberall sonst im Programm) stellt sicher,
            // dass die Meldung tatsaechlich gesehen wird, unabhaengig vom Scroll-
            // Stand. Format wie von A/Opa gewuenscht: "<N> Erinnerung(en) wurden
            // <Ziel> zugeordnet."
            string zielNamen = string.Join(", ", ausgewaehltePersonen.Select(p => p.ToString()));
            James.Hinweis((markiert.Count == 1 ? "1 Erinnerung wurde " : markiert.Count + " Erinnerungen wurden ") + zielNamen + " zugeordnet." +
                (gesamtBereitsVorhanden > 0 ? " (" + gesamtBereitsVorhanden + " war(en) bereits zugeordnet und wurde(n) übersprungen.)" : ""));

            // Optimierungswunsch (31.07.): Panel bleibt offen, damit "Ansehen"
            // und "Archivieren" direkt im Anschluss noch nutzbar sind - schließt
            // erst, wenn der Benutzer selbst auf "Abbrechen" klickt.
            AktualisiereAmDirekteAuswahlListe();
        }

        // Neue Funktion (Generaltest 2): "Erinnerungen ansehen" für Personen
        // jetzt auch direkt aus der Arbeitsmappe heraus möglich (vorher nur
        // über den Schreibtisch/das Archiv erreichbar).
        // TÜV-Reparatur (07.08.), NACHTRAG: dies war die 10. (bisher
        // übersehene) Aufrufstelle von ErinnerungenFenster - hatte beim
        // ersten Umbau noch den fehlenden entferneAusKontext-Callback,
        // jetzt ergänzt (analog zu ArchivPersonErinnerungenAnsehen_Click
        // in MainWindow.Personen.cs).
        private void ArbeitsmappePersonenAnsehen_Click(object sender, RoutedEventArgs e)
        {
            Person person = ArbeitsmappeTitelbildPersonComboBox.SelectedItem as Person;

            if (person == null)
            {
                return;
            }

            List<ErinnerungsInfo> erinnerungenListe = SammelErinnerungenFuerPerson(person);

            if (erinnerungenListe.Count == 0)
            {
                return;
            }

            ErinnerungenFenster fenster = new ErinnerungenFenster(James.ErinnerungenFensterTitelPerson(person.ToString()), erinnerungenListe, LiesVisuelleMerkmale, SpeichereVisuelleMerkmale, ZaehleVorkommenVisuellesMerkmal, ErstelleEntferneAusPersonCallback(person), SendeMarkierteZurArbeitsmappe);
            fenster.Owner = this;
            fenster.Show();
        }

        // Neue Funktion (Generaltest 2): "Archivieren" für Personen jetzt
        // auch direkt aus der Arbeitsmappe heraus möglich, blockweise.
        private void ArbeitsmappePersonenArchivieren_Click(object sender, RoutedEventArgs e)
        {
            List<Person> ausgewaehltePersonen = ArbeitsmappeTitelbildPersonComboBox.SelectedItems.Cast<Person>().ToList();

            if (ausgewaehltePersonen.Count == 0)
            {
                return;
            }

            foreach (Person person in ausgewaehltePersonen)
            {
                allePersonen.Remove(person);
                ArchivListe.Items.Add(person);
            }

            AktualisierePersonenAnzeige();
            SpeichereDaten();

            ArbeitsmappeTitelbildPersonComboBox.ItemsSource = null;
            ArbeitsmappeTitelbildPersonComboBox.ItemsSource = allePersonen;

            ArbeitsmappeStatusText.Text = ausgewaehltePersonen.Count == 1
                ? James.ImArchivAngekommen(ausgewaehltePersonen[0].ToString())
                : James.ImArchivAngekommenMehrere(ausgewaehltePersonen.Count);
        }

        // Neue Funktion (Generaltest 2): "In den Papierkorb legen" für
        // Personen jetzt auch direkt aus der Arbeitsmappe heraus möglich,
        // blockweise, mit derselben Sicherheitsabfrage wie sonst im Programm.
        private void ArbeitsmappePersonenInPapierkorb_Click(object sender, RoutedEventArgs e)
        {
            List<Person> ausgewaehltePersonen = ArbeitsmappeTitelbildPersonComboBox.SelectedItems.Cast<Person>().ToList();

            if (ausgewaehltePersonen.Count == 0)
            {
                return;
            }

            string frage = ausgewaehltePersonen.Count == 1
                ? James.FrageInPapierkorbEinzeln(ausgewaehltePersonen[0].ToString())
                : James.FrageInPapierkorbMehrere(ausgewaehltePersonen.Count);

            bool ergebnis = James.FrageJaNein(frage, James.TitelEntscheidung, MessageBoxImage.Warning);

            if (!ergebnis)
            {
                return;
            }

            foreach (Person person in ausgewaehltePersonen)
            {
                allePersonen.Remove(person);
                PapierkorbListe.Items.Add(person);
            }

            AktualisierePersonenAnzeige();
            SpeichereDaten();

            ArbeitsmappeTitelbildPersonComboBox.ItemsSource = null;
            ArbeitsmappeTitelbildPersonComboBox.ItemsSource = allePersonen;

            ArbeitsmappeStatusText.Text = ausgewaehltePersonen.Count == 1
                ? James.InPapierkorbGelegtEinzeln(ausgewaehltePersonen[0].ToString())
                : James.InPapierkorbGelegtMehrere(ausgewaehltePersonen.Count);
        }

        // Neue Funktion (Generaltest 2): einfacher Abbruch.
        private void ArbeitsmappePersonenAbbrechen_Click(object sender, RoutedEventArgs e)
        {
            ArbeitsmappePersonAuswahlPanel.Visibility = Visibility.Collapsed;
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && ArbeitsmappeGrossansichtPanel.Visibility == Visibility.Visible)
            {
                ArbeitsmappeGrossansichtSchliessen_Click(sender, e);
            }
        }
    }
}
