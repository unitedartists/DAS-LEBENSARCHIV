using System.Windows;

namespace DAS_LEBENSARCHIV
{
    // ============================================================
    // SANIERUNG BAUPHASE 1 - TESTBEREICH (08.08.)
    // ============================================================
    // Bewusst als eigene, neue Datei angelegt (statt in
    // MainWindow.Werkzeuge.cs einzufügen) - dadurch bleibt jede
    // bestehende Datei für diese Bauphase vollständig unangetastet,
    // wie von A/Opa gefordert. Öffnet ausschließlich das isolierte
    // SanierungTestFenster, ohne jede Verbindung zu den echten Daten.
    public partial class MainWindow
    {
        private void SanierungTestbereichOeffnen_Click(object sender, RoutedEventArgs e)
        {
            SanierungTestFenster fenster = new SanierungTestFenster();
            fenster.Owner = this;
            fenster.ShowDialog();
        }
    }
}
