using System.Windows;

namespace DAS_LEBENSARCHIV
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            VornameTextBox.Focus();
        }

        private void Speichern_Click(object sender, RoutedEventArgs e)
        {
            string vorname = VornameTextBox.Text.Trim();
            string nachname = NachnameTextBox.Text.Trim();

            if (vorname == "" && nachname == "")
            {
                MessageBox.Show(
                    "Bitte mindestens einen Namen eingeben.",
                    "Hinweis",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                VornameTextBox.Focus();
                return;
            }

            string anzeige = (vorname + " " + nachname).Trim();

            PersonenListe.Items.Add(anzeige);

            MessageBox.Show(
                anzeige + " wurde gespeichert.",
                "Person gespeichert",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            VornameTextBox.Clear();
            NachnameTextBox.Clear();
            GeburtTextBox.Clear();
            OrtTextBox.Clear();

            VornameTextBox.Focus();
        }
    }
}