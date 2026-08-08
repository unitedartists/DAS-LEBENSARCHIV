using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace DAS_LEBENSARCHIV
{
    // ============================================================
    // SANIERUNG BAUPHASE 4 (08.08.): LESENDE AM-ANBINDUNG
    // ============================================================
    // A's Vorgabe: das neue Erinnerungsmodell wird zunächst LESEND an
    // die Arbeitsmappe angeschlossen - keine alte Struktur wird
    // entfernt oder ersetzt. personen.json wird an keiner Stelle in
    // dieser Datei beschrieben; geschrieben wird ausschließlich die
    // bereits in Bauphase 3 angelegte, separate Datei
    // erinnerungsmodell.json (beim Bestätigen einer neuen Zuordnung).
    // Keine physische Foto-Datei wird kopiert oder verschoben.
    //
    // Testziel (A, 08.08.): Archiv -> Erinnerungen markieren ->
    // "Zuordnen" -> in der Arbeitsmappe erscheinen GENAU diese
    // Erinnerungen (nicht die bisherigen Kachel-Inhalte) -> dort neue
    // Zuordnung anlegen -> prüfen.
    public partial class MainWindow
    {
        private List<Erinnerung> erinnerungsmodellErinnerungen;
        private List<Zuordnung> erinnerungsmodellZuordnungen;
        private bool erinnerungsmodellGeladen;

        // Session-gebundene Arbeitsauswahl - nur Erinnerungs-Ids, keine
        // neue physische Kopie, kein zweiter Erinnerungsbestand.
        private readonly List<Guid> amArbeitsauswahl = new List<Guid>();

        private string ErinnerungsmodellDateiPfad => Path.Combine(OrdnerPfad, "erinnerungsmodell.json");

        private void LadeErinnerungsmodellFallsNoetig()
        {
            if (erinnerungsmodellGeladen)
            {
                return;
            }

            erinnerungsmodellErinnerungen = new List<Erinnerung>();
            erinnerungsmodellZuordnungen = new List<Zuordnung>();

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
                    }
                }
            }
            catch
            {
                // Noch keine Migration durchgeführt oder Datei nicht lesbar -
                // dann bleibt die Arbeitsauswahl-Funktion einfach leer nutzbar,
                // kein Fehler für den Rest des Programms.
            }

            erinnerungsmodellGeladen = true;
        }

        // Wird als Action<List<string>> an ErinnerungenFenster übergeben
        // (siehe alle new ErinnerungenFenster(...)-Aufrufstellen). Sucht zu
        // jedem übergebenen Pfad die passende, bereits migrierte Erinnerung
        // (Vergleich über Fundort.Pfad) und trägt deren Id in die
        // Arbeitsauswahl ein - rein additiv, erzeugt nichts Neues.
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

                if (!amArbeitsauswahl.Contains(erinnerung.Id))
                {
                    amArbeitsauswahl.Add(erinnerung.Id);
                }

                gefunden++;
            }

            HauptTabControl.SelectedIndex = ArbeitsmappeTabIndex;

            AktualisiereAmArbeitsauswahlAnzeige();

            if (nichtGefunden > 0)
            {
                James.Hinweis(gefunden + " Erinnerung(en) wurden in die Arbeitsauswahl übernommen. " + nichtGefunden +
                    " Erinnerung(en) sind noch nicht Teil des neuen Modells (noch nicht migriert) und konnten deshalb nicht übernommen werden.");
            }
        }

        private void AktualisiereAmArbeitsauswahlAnzeige()
        {
            LadeErinnerungsmodellFallsNoetig();

            List<Erinnerung> ausgewaehlt = erinnerungsmodellErinnerungen
                .Where(er => amArbeitsauswahl.Contains(er.Id))
                .ToList();

            AmArbeitsauswahlText.Text = ausgewaehlt.Count == 0
                ? "Noch keine Erinnerungen aus dem Archiv hierher geschickt."
                : ausgewaehlt.Count + " Erinnerung(en) aus dem Archiv zur Neuzuordnung markiert:";

            AmArbeitsauswahlListe.ItemsSource = null;
            AmArbeitsauswahlListe.ItemsSource = ausgewaehlt
                .Select(er => er.Fundorte.Count > 0 ? Path.GetFileName(er.Fundorte[0].Pfad) : er.Id.ToString())
                .ToList();

            bool istAusgewaehlt = ausgewaehlt.Count > 0;

            AmArbeitsauswahlLeerenButton.IsEnabled = istAusgewaehlt;
            AktualisiereAmZielAuswahl();
        }

        private void AmZielTypComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AktualisiereAmZielAuswahl();
        }

        private void AktualisiereAmZielAuswahl()
        {
            if (AmZielTypComboBox == null || AmZielObjektComboBox == null)
            {
                return;
            }

            ComboBoxItem ausgewaehlterTyp = AmZielTypComboBox.SelectedItem as ComboBoxItem;
            string typText = ausgewaehlterTyp != null ? ausgewaehlterTyp.Content.ToString() : "Person";

            AmZielObjektComboBox.ItemsSource = null;

            if (typText == "Ereignis")
            {
                AmZielObjektComboBox.ItemsSource = freieEreignisse.ToList();
            }
            else if (typText == "Sammlung")
            {
                AmZielObjektComboBox.ItemsSource = sammlungen.ToList();
            }
            else
            {
                AmZielObjektComboBox.ItemsSource = allePersonen.ToList();
            }

            bool arbeitsauswahlVorhanden = amArbeitsauswahl.Count > 0;
            bool zielVorhanden = AmZielObjektComboBox.Items.Count > 0;

            AmZuordnenBestaetigenButton.IsEnabled = arbeitsauswahlVorhanden && zielVorhanden;

            if (zielVorhanden)
            {
                AmZielObjektComboBox.SelectedIndex = 0;
            }
        }

        // Legt für jede Erinnerung in der Arbeitsauswahl eine NEUE
        // Zuordnung zum gewählten Ziel an - die BISHERIGEN Zuordnungen
        // dieser Erinnerung bleiben dabei unverändert bestehen (A's
        // Regel: "alte Zuordnung bleibt bis zur bestätigten Neuordnung
        // erhalten" - hier ist die Neuordnung selbst der bestätigte
        // Schritt, die alte bleibt trotzdem einfach zusätzlich
        // bestehen, da diese Testphase noch keine Entfernungslogik für
        // Zuordnungen im neuen Modell hat). Schreibt ausschließlich in
        // erinnerungsmodell.json - personen.json bleibt unangetastet.
        private void AmZuordnenBestaetigen_Click(object sender, RoutedEventArgs e)
        {
            if (amArbeitsauswahl.Count == 0)
            {
                return;
            }

            ComboBoxItem ausgewaehlterTyp = AmZielTypComboBox.SelectedItem as ComboBoxItem;
            string typText = ausgewaehlterTyp != null ? ausgewaehlterTyp.Content.ToString() : "Person";

            ZuordnungsZielTyp zielTyp;
            Guid zielId;
            string zielBezeichnung;

            if (typText == "Ereignis")
            {
                Ereignis ereignis = AmZielObjektComboBox.SelectedItem as Ereignis;
                if (ereignis == null) { return; }
                zielTyp = ZuordnungsZielTyp.Ereignis;
                zielId = ereignis.Id;
                zielBezeichnung = ereignis.Titel;
            }
            else if (typText == "Sammlung")
            {
                Sammlung sammlung = AmZielObjektComboBox.SelectedItem as Sammlung;
                if (sammlung == null) { return; }
                zielTyp = ZuordnungsZielTyp.Sammlung;
                zielId = sammlung.Id;
                zielBezeichnung = sammlung.Titel;
            }
            else
            {
                Person person = AmZielObjektComboBox.SelectedItem as Person;
                if (person == null) { return; }
                zielTyp = ZuordnungsZielTyp.Person;
                zielId = person.Id;
                zielBezeichnung = person.ToString();
            }

            bool ergebnis = James.FrageJaNein(
                amArbeitsauswahl.Count + " Erinnerung(en) neu zuordnen zu \"" + zielBezeichnung + "\"?\n\n" +
                "Bisherige Zuordnungen dieser Erinnerungen bleiben dabei zusätzlich bestehen (Testphase).",
                James.TitelEntscheidung);

            if (!ergebnis)
            {
                return;
            }

            foreach (Guid erinnerungId in amArbeitsauswahl)
            {
                erinnerungsmodellZuordnungen.Add(new Zuordnung
                {
                    ErinnerungId = erinnerungId,
                    ZielTyp = zielTyp,
                    ZielId = zielId,
                    ZielBezeichnung = zielBezeichnung
                });
            }

            int anzahlNeu = amArbeitsauswahl.Count;

            bool gespeichertVerifiziert = SpeichereErinnerungsmodell();

            amArbeitsauswahl.Clear();
            AktualisiereAmArbeitsauswahlAnzeige();

            AmStatusText.Text = gespeichertVerifiziert
                ? "✓ " + anzahlNeu + " neue Zuordnung(en) zu \"" + zielBezeichnung + "\" angelegt und gespeichert."
                : "⚠ Zuordnung angelegt, aber Speichern konnte nicht verifiziert werden - bitte prüfen.";
        }

        private void AmArbeitsauswahlLeeren_Click(object sender, RoutedEventArgs e)
        {
            amArbeitsauswahl.Clear();
            AktualisiereAmArbeitsauswahlAnzeige();
            AmStatusText.Text = "Arbeitsauswahl geleert - es wurde nichts zugeordnet.";
        }

        // Schreibt AUSSCHLIESSLICH erinnerungsmodell.json (niemals
        // personen.json), danach Rückeinlese-Verifikation wie bereits in
        // Bauphase 3 - gleiches Sicherheitsprinzip.
        private bool SpeichereErinnerungsmodell()
        {
            try
            {
                ArchivErinnerungsDaten daten = new ArchivErinnerungsDaten
                {
                    Erinnerungen = erinnerungsmodellErinnerungen,
                    Zuordnungen = erinnerungsmodellZuordnungen
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
    }
}
