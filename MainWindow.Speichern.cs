using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow
    {
        private void LadeDaten()
        {
            try
            {
                if (File.Exists(DateiPfad))
                {
                    string json = File.ReadAllText(DateiPfad);

                    ArchivDaten daten = null;

                    try
                    {
                        daten = JsonSerializer.Deserialize<ArchivDaten>(json);
                    }
                    catch
                    {
                        daten = null;
                    }

                    if (daten != null && (daten.Personen != null || daten.Papierkorb != null || daten.Archiv != null))
                    {
                        if (daten.Personen != null)
                        {
                            allePersonen.AddRange(daten.Personen);
                        }

                        if (daten.Papierkorb != null)
                        {
                            foreach (Person person in daten.Papierkorb)
                            {
                                PapierkorbListe.Items.Add(person);
                            }
                        }

                        if (daten.Archiv != null)
                        {
                            foreach (Person person in daten.Archiv)
                            {
                                ArchivListe.Items.Add(person);
                            }
                        }

                        // Build 5.0: freie Ereignisse laden (altes
                        // Dateiformat kennt dieses Feld noch nicht - dann
                        // bleibt die Liste einfach leer, keine Migration
                        // nötig).
                        if (daten.FreieEreignisse != null)
                        {
                            freieEreignisse.AddRange(daten.FreieEreignisse);
                        }

                        if (daten.FreieEreignisseArchiv != null)
                        {
                            freieEreignisseArchiv.AddRange(daten.FreieEreignisseArchiv);
                        }

                        // Etappe A.5: altes Dateiformat kennt dieses Feld
                        // noch nicht - dann bleibt die Liste einfach leer,
                        // keine Migration nötig.
                        if (daten.FreieEreignissePapierkorb != null)
                        {
                            freieEreignissePapierkorb.AddRange(daten.FreieEreignissePapierkorb);
                        }

                        // Neue Funktion (Generaltest 2): Sammlungen laden -
                        // dasselbe additive Muster wie bei freien
                        // Ereignissen (altes Dateiformat kennt dieses Feld
                        // noch nicht, dann bleibt die Liste einfach leer).
                        if (daten.Sammlungen != null)
                        {
                            sammlungen.AddRange(daten.Sammlungen);
                        }

                        if (daten.SammlungenArchiv != null)
                        {
                            sammlungenArchiv.AddRange(daten.SammlungenArchiv);
                        }

                        if (daten.SammlungenPapierkorb != null)
                        {
                            sammlungenPapierkorb.AddRange(daten.SammlungenPapierkorb);
                        }

                        // Neue Funktion (Generaltest 2): Asservatenkammer.
                        if (daten.Asservatenkammer != null)
                        {
                            asservatenkammer.AddRange(daten.Asservatenkammer);
                        }

                        // Etappe B (Build 2.9): dasselbe additive Muster -
                        // altes Dateiformat kennt dieses Feld noch nicht,
                        // dann bleibt die Liste einfach leer.
                        if (daten.ErinnerungsGedaechtnis != null)
                        {
                            erinnerungsGedaechtnis.AddRange(daten.ErinnerungsGedaechtnis);
                        }

                        // Build 3.0: dasselbe additive Muster - reine
                        // Vorbereitung, wird noch nirgends befüllt.
                        if (daten.WissensBeziehungen != null)
                        {
                            wissensBeziehungen.AddRange(daten.WissensBeziehungen);
                        }
                    }
                    else
                    {
                        // Altes Dateiformat (vor Einführung von Papierkorb/Archiv): einfache Liste von Personen
                        List<Person> alteListe = JsonSerializer.Deserialize<List<Person>>(json);

                        if (alteListe != null)
                        {
                            allePersonen.AddRange(alteListe);
                        }
                    }

                    SortiereAllePersonen();
                    AktualisierePersonenAnzeige();
                    AktualisiereFreieEreignisseAnzeige();
                    AktualisiereSammlungenAnzeige();
                    AktualisiereAsservatenkammerAnzeige();
                    AktualisiereEinheitlicheEreignisliste();
                }
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimLadenDaten(ex.Message));
            }
        }

        private void SpeichereDaten()
        {
            try
            {
                Directory.CreateDirectory(OrdnerPfad);

                ArchivDaten daten = new ArchivDaten
                {
                    Personen = allePersonen,
                    Papierkorb = PapierkorbListe.Items.Cast<Person>().ToList(),
                    Archiv = ArchivListe.Items.Cast<Person>().ToList(),
                    FreieEreignisse = freieEreignisse,
                    FreieEreignisseArchiv = freieEreignisseArchiv,
                    FreieEreignissePapierkorb = freieEreignissePapierkorb,
                    Sammlungen = sammlungen,
                    SammlungenArchiv = sammlungenArchiv,
                    SammlungenPapierkorb = sammlungenPapierkorb,
                    Asservatenkammer = asservatenkammer,
                    ErinnerungsGedaechtnis = erinnerungsGedaechtnis,
                    WissensBeziehungen = wissensBeziehungen
                };

                JsonSerializerOptions optionen = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(daten, optionen);

                File.WriteAllText(DateiPfad, json);
            }
            catch (Exception ex)
            {
                James.Problem(James.FehlerBeimSpeichernDaten(ex.Message));
            }
        }
    }
}
