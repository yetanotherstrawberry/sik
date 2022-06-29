using Microsoft.VisualBasic;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SKProj1
{

    class Umieszczenie
    {
        public uint Wiersz { get; set; }
        public uint Kolumna { get; set; }
    }

    enum Typ {

        Odswiezenie,
        Zaznaczenie,
        WyslijPlik,
        OdbierzPlik,
        Wiadomosc,

    }

    class Polecenie
    {

        public Task Zadanie { get; set; }
        public Typ Typ { get; set; }

    }

    public partial class MainWindow : Window
    {

        private static readonly ObservableCollection<ListBoxItem> Wiadomosci = new ObservableCollection<ListBoxItem>();
        private static readonly bool?[,] Pola = new bool?[3, 3]; // true = O, false = X, null to puste pole.
        private bool? Gracz = null;  // Litera gracza: true = O, false = X, null brak połączenia.
        private TcpClient Klient;
        private string Literka;
        private CancellationTokenSource KoniecOdswiezania;
        private bool? MojRuch = null;
        private uint? do_ustawienia = null;
        private ICollection<Polecenie> DoWykonania = null;

        private static Exception BladSerwera(string ret) => new InvalidOperationException("Serwer zwrócił nieprawidłową operację (\"" + ret + "\").");
        
        private async void Rozlacz(object sender = null, RoutedEventArgs e = null)
        {

            KoniecOdswiezania.Cancel();

            while (DoWykonania.Count() > 0) await Task.Delay(400);

            if (Klient != null)
            {
                Klient?.Close();
            }

            NowaSzachownica();
            PolaczenieGUI.IsEnabled = true;
            OdlaczGuzik.IsEnabled = false;
            WiadomosciGUI.IsEnabled = false;
            Wiadomosci.Clear();
            Szachownica.IsEnabled = false;
            PlikGUI.IsEnabled = false;
            DoWykonania?.Clear();
            this.Close();

        }
        
        private static void WyzerujPola() => Pola.Initialize();
        private static void UstawPole(uint wiersz, uint kolumna, bool? wartosc = null) => Pola[wiersz, kolumna] = wartosc;
        private static bool? Pole(uint wiersz, uint kolumna) => Pola[wiersz, kolumna];
        private static void DodajWiadomosc(string wiadomosc, bool blad = false)
        {
            if (wiadomosc.Length > 0)
                Wiadomosci.Insert(0, new ListBoxItem { Content = new TextBlock { Text = wiadomosc, Foreground = new SolidColorBrush(Color.FromArgb(255, (byte)(blad ? 255 : 0), 0, 0)), } });
        }

        private void WyslijZaznaczenie()
        {

            if (do_ustawienia != null)
            {

                DoWykonania.Add(new Polecenie
                {
                    Zadanie = new Task(() =>
                    {

                        var zad1 = Pobierz("GAME_" + Literka);
                        zad1.Wait();
                        string wynik1 = zad1.Result;
                        wynik1 = wynik1.Replace("\0", "");
                        if (!wynik1.Equals("")) throw BladSerwera(wynik1);

                        var zad2 = Pobierz(do_ustawienia.ToString());
                        zad2.Wait();
                        string wynik2 = zad2.Result;
                        wynik2 = wynik2.Replace("\0", "");
                        if (!wynik2.Equals("")) throw BladSerwera(wynik2);
                        
                        do_ustawienia = null;
                        
                    }),
                    Typ = Typ.Zaznaczenie,
                });

            }

        }

        private void UstawSzachownice(uint wiersz, uint kolumna, bool? wartosc = null)
        {

            if (MojRuch ?? false)
            {

                MojRuch = false;

                if (!Pole(wiersz, kolumna).HasValue)
                {

                    if (do_ustawienia == null)
                    {

                        do_ustawienia = kolumna + (wiersz * 3);

                    }
                    else
                    {

                        Dispatcher.Invoke(() => DodajWiadomosc("Już wybrano pole. Poczekaj chwilę, aż chomik okrążenie zrobi...", blad: true));

                    }

                }
                else
                {

                    Dispatcher.Invoke(() => DodajWiadomosc("Pole jest zajęte!", blad: true));

                }

            }
            else
            {

                Dispatcher.Invoke(() => DodajWiadomosc("To nie jest Twój ruch!", blad: true));

            }

        }

        private void OdswiezSzachownice()
        {

            Szachownica.Children.Clear();

            for (int wiersz = 0; wiersz < Pola.GetLength(0); wiersz++)
                for (int kolumna = 0; kolumna < Pola.GetLength(1); kolumna++)
                {

                    UIElement kafelek = new TextBlock
                    {
                        Text = Pola[wiersz, kolumna].HasValue ? (Pola[wiersz, kolumna].Value ? "O" : "X") : "",
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 40,
                        TextAlignment = TextAlignment.Center,
                    };

                    var obramowanie = new Border
                    {
                        BorderBrush = new SolidColorBrush(Color.FromArgb(255, 0, 0, 0)),
                        BorderThickness = new Thickness(kolumna == 0 ? 1 : 0, wiersz == 0 ? 1 : 0, 1, 1),
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        VerticalAlignment = VerticalAlignment.Stretch,
                        Child = kafelek,
                        Tag = new Umieszczenie { Wiersz = (uint)wiersz, Kolumna = (uint)kolumna, },
                    };

                    obramowanie.MouseDown += (object obiekt, MouseButtonEventArgs argumenty)
                        => UstawSzachownice(((Umieszczenie)obramowanie.Tag).Wiersz, ((Umieszczenie)obramowanie.Tag).Kolumna, Gracz);

                    Grid.SetRow(obramowanie, wiersz);
                    Grid.SetColumn(obramowanie, kolumna);

                    Szachownica.Children.Add(obramowanie);

                }

        }

        private void NowaSzachownica(bool czyZerowacPola = true)
        {

            if (czyZerowacPola) WyzerujPola();
            OdswiezSzachownice();

        }

        private async Task<ICollection<string>> PobierzPodzielone(string wiadomosc = null, bool debug = false)
        {

            if (debug) Debug.WriteLine("[NET] Ty -> Serwer: " + wiadomosc);

            var szachownica = await Dispatcher.InvokeAsync(() => Szachownica.IsEnabled);

            await Dispatcher.InvokeAsync(() => Szachownica.IsEnabled = false);

            if (wiadomosc != null && wiadomosc.Length > 0)
                await Klient.GetStream().WriteAsync(Encoding.ASCII.GetBytes(wiadomosc), 0, wiadomosc.Length);

            var odpowiedz = new byte[1368];
            var pakiety = 0;
            var wynik = new List<string>();

            do
            {

                await Task.Delay(300);

            } while (!Klient.GetStream().DataAvailable);

            do
            {

                pakiety = await Klient.GetStream().ReadAsync(odpowiedz, 0, odpowiedz.Length);
                if (pakiety == 2 && odpowiedz[0] == 'O' && odpowiedz[1] == 'K') break;
                wynik.Add(Encoding.ASCII.GetString(odpowiedz, 0, pakiety));
                Array.Clear(odpowiedz, 0, odpowiedz.Length);

            } while (pakiety == odpowiedz.Length);

            await Dispatcher.InvokeAsync(() => Szachownica.IsEnabled = szachownica);

            if (debug) Debug.WriteLine("[NET] Serwer -> Ty: " + wynik);

            return wynik;

        }

        private async Task<string> Pobierz(string wiadomosc = null, bool czekajNaOdpowiedz = true, bool debug = false)
        {

            if (debug) Debug.WriteLine("[NET] Ty -> Serwer: " + wiadomosc);

            var szachownica = await Dispatcher.InvokeAsync(() => Szachownica.IsEnabled);

            await Dispatcher.InvokeAsync(() => Szachownica.IsEnabled = false);

            if (wiadomosc != null && wiadomosc.Length > 0)
                await Klient.GetStream().WriteAsync(Encoding.ASCII.GetBytes(wiadomosc), 0, wiadomosc.Length);

            if (czekajNaOdpowiedz)
            {

                var odpowiedz = new byte[1024];
                var pakiety = 0;
                var wynik = "";

                do
                {

                    await Task.Delay(300);

                } while (!Klient.GetStream().DataAvailable);

                do
                {

                    pakiety = await Klient.GetStream().ReadAsync(odpowiedz, 0, odpowiedz.Length);
                    if (pakiety == 2 && odpowiedz[0] == 'O' && odpowiedz[1] == 'K') break;
                    wynik += Encoding.ASCII.GetString(odpowiedz, 0, pakiety);
                    Array.Clear(odpowiedz, 0, odpowiedz.Length);

                } while (pakiety == odpowiedz.Length);

                await Dispatcher.InvokeAsync(() => Szachownica.IsEnabled = szachownica);

                if (debug) Debug.WriteLine("[NET] Serwer -> Ty: " + wynik);

                return wynik;

            }
            else return "";

        }

        private async Task Tablica()
        {

            string wynik = await Pobierz("BOARD");

            wynik = wynik.Replace("\0", "");
            var odpowiedz = wynik.Split(' ');

            if (odpowiedz.Count() != 10) throw BladSerwera(wynik);

            for (int i = 0; i < Pola.GetLength(0); i++)
                for (int j = 0; j < Pola.GetLength(1); j++)
                {

                    string temp = odpowiedz[(3 * i) + j];
                    bool? pole = null;
                    if (temp.Equals("X")) pole = false;
                    else if (temp.Equals("O")) pole = true;
                    else if (temp.Equals("N")) pole = null;
                    else throw BladSerwera(wynik);

                    await Dispatcher.InvokeAsync(() => UstawPole((uint)i, (uint)j, pole));

                }

            if (odpowiedz[9].Equals(Literka))
            {

                if (MojRuch == null || !MojRuch.Value) await Dispatcher.InvokeAsync(() => DodajWiadomosc("Twój ruch!"));
                await Dispatcher.InvokeAsync(() => Szachownica.IsEnabled = true);
                MojRuch = true;

            }
            else
            {

                if (MojRuch == null || MojRuch.Value) await Dispatcher.InvokeAsync(() => DodajWiadomosc("Czekam na ruch przeciwnika..."));
                await Dispatcher.InvokeAsync(() => Szachownica.IsEnabled = false);
                MojRuch = false;

            }

        }

        private async void Polacz(object sender = null, RoutedEventArgs e = null)
        {

            try
            {

                KoniecOdswiezania = new CancellationTokenSource();
                PolaczenieGUI.IsEnabled = false;
                NowaSzachownica();

                Klient = new TcpClient();
                var dane = Adres.Text.Split(':');
                if (dane.Count() < 1 || dane.Count() > 2) throw new ArgumentException("Nieprawidłowy adres.");

                await Klient.ConnectAsync(dane[0], dane.Length > 1 ? int.Parse(dane[1]) : 3014);

                string wynik = await Pobierz("HELLO");
                wynik = wynik.Replace("\0", "");

                if (!new[] { "GIVE_O", "GIVE_X" }.Contains(wynik)) throw BladSerwera(wynik);

                Gracz = wynik.Equals("GIVE_O");
                Literka = Gracz.Value ? "O" : "X";
                DoWykonania = new List<Polecenie>();

                DodajWiadomosc("Grasz jako: " + Literka + ".");

                var zadanie = new Task(
                    async () =>
                    {
                        while (true)
                        {
                            try
                            {
                                await Task.Delay(300);
                                if (!DoWykonania.Any(x => x.Typ == Typ.Odswiezenie))
                                {
                                    DoWykonania.Add(new Polecenie
                                    {
                                        Zadanie = new Task(() =>
                                        {
                                            try
                                            {
                                                Dispatcher.Invoke(WyslijZaznaczenie);
                                                if (!DoWykonania.Any(x => x.Typ == Typ.Zaznaczenie)) Tablica()?.Wait();
                                                Dispatcher.Invoke(OdswiezSzachownice);
                                                WezWiadomosci()?.Wait();
                                            }
                                            catch { }
                                        }),
                                        Typ = Typ.Odswiezenie
                                    });
                                }
                            }
                            catch (Exception wyjatek)
                            {
                                Dispatcher.Invoke(() => DodajWiadomosc(wyjatek.Message, blad: true));
                            }
                        }
                    }, KoniecOdswiezania.Token);

                zadanie.Start();

                var zadanie2 = new Task(async () => {
                    while (true)
                    {
                        await Task.Delay(500);
                        if (DoWykonania != null)
                        {
                            if (DoWykonania.Count > 0)
                            {

                                var kopia = new List<Polecenie>(DoWykonania.AsEnumerable());
                                foreach (var element in kopia)
                                {

                                    DoWykonania.Remove(element);
                                    if (!element.Zadanie.IsCanceled)
                                    {
                                        element.Zadanie.Start();
                                        element.Zadanie.Wait();
                                    }

                                }

                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }, KoniecOdswiezania.Token);
                zadanie2.Start();

                WiadomosciGUI.IsEnabled = true;
                OdlaczGuzik.IsEnabled = true;
                PlikGUI.IsEnabled = true;

            }
            catch (Exception wyjatek)
            {

                DodajWiadomosc(wyjatek.Message, blad: true);
                PolaczenieGUI.IsEnabled = true;

            }

        }

        public MainWindow()
        {

            InitializeComponent();
            Chat.DataContext = Wiadomosci;
            NowaSzachownica();
            OdswiezSzachownice();

        }

        private async Task WezWiadomosci()
        {

            string wiadomosci = await Pobierz("GETMSG_" + Literka);

            foreach (string wiadomosc in wiadomosci.Split('\0'))
                await Dispatcher.InvokeAsync(() => DodajWiadomosc(wiadomosc));

        }

        private void Wyslij(object sender, RoutedEventArgs e)
        {

            string wiadomosc = Msg.Text;

            DoWykonania.Add(new Polecenie
            {
                Zadanie = new Task(() =>
                {
                    var zad1 = Pobierz("SENDMSG_" + Literka);
                    zad1.Wait();
                    var wynik1 = zad1.Result;
                    if (!wynik1.Equals("")) throw BladSerwera(wynik1);

                    var zad2 = Pobierz(wiadomosc);
                    zad2.Wait();
                    var wynik2 = zad2.Result;
                    if (!wynik2.Equals("")) throw BladSerwera(wynik2);
                }),
                Typ = Typ.Wiadomosc,
            });

            Dispatcher.Invoke(() => Msg.Text = "");

        }

        private void DodajPlik(object sender, RoutedEventArgs e)
        {

            bool okienko_status = Okno.IsEnabled;
            OpenFileDialog otworz = new OpenFileDialog();

            if (otworz.ShowDialog() ?? false)
            {

                DoWykonania.Add(new Polecenie
                {
                    Zadanie = new Task(() =>
                    {

                        bool stare_okno = okienko_status;
                        Dispatcher.Invoke(() => Okno.IsEnabled = false);

                        var zad1 = Pobierz("SEND_FILE");
                        zad1.Wait();
                        var wynik1 = zad1.Result;
                        if (!wynik1.Equals("")) throw BladSerwera(wynik1);

                        var plik = File.ReadAllBytes(otworz.FileName);

                        var zad2 = Pobierz(otworz.SafeFileName);
                        zad2.Wait();
                        var wynik2 = zad2.Result;
                        if (!wynik2.Equals("")) throw BladSerwera(wynik2);

                        int ile = 0, ilosc_pel_tablic = plik.Length / 1024, ostatnia_tab = plik.Length - (ilosc_pel_tablic * 1024);
                        var plik_string = new string[ilosc_pel_tablic + (ostatnia_tab > 0 ? 1 : 0)];

                        for (; ile < ilosc_pel_tablic; ile++)
                        {

                            var temp = new byte[1024];
                            Array.Copy(plik, ile * 1024, temp, 0, 1024);
                            plik_string[ile] = Convert.ToBase64String(temp);

                        }

                        if(ostatnia_tab > 0)
                        {

                            var temp = new byte[ostatnia_tab];
                            Array.Copy(plik, ile * 1024, temp, 0, ostatnia_tab);
                            plik_string[ile] = Convert.ToBase64String(temp);

                        }

                        var zad3 = Pobierz(plik_string.Select(x => x.Length).Sum().ToString());
                        zad3.Wait();
                        var wynik3 = zad3.Result;
                        if (!wynik3.Equals("")) throw BladSerwera(wynik3);

                        foreach(var czesc in plik_string)
                        {

                            var zad4 = Pobierz(czesc, czekajNaOdpowiedz: czesc == plik_string.Last());
                            zad4.Wait();
                            var wynik4 = zad4.Result;
                            if (!wynik4.Equals("")) throw BladSerwera(wynik4);

                        }

                        Dispatcher.Invoke(() => Okno.IsEnabled = stare_okno);

                        Dispatcher.Invoke(() => DodajWiadomosc("Plik wysłany."));

                    }),
                    Typ = Typ.WyslijPlik,
                });

            }

        }

        private void PobierzPlik(object sender, RoutedEventArgs e)
        {

            bool okienko_status = Okno.IsEnabled;
            string nazwa = Interaction.InputBox("Nazwa?");

            SaveFileDialog zapisz = new SaveFileDialog();

            if ((zapisz.ShowDialog() ?? false) && nazwa != null && nazwa.Length > 0)
            {

                DoWykonania.Add(new Polecenie
                {
                    Zadanie = new Task(() =>
                    {
                        bool stare_okno = okienko_status;
                        Dispatcher.Invoke(() => Okno.IsEnabled = false);

                        var zad1 = Pobierz("GET_FILE");
                        zad1.Wait();
                        var wynik1 = zad1.Result;
                        if (!wynik1.Equals("")) throw BladSerwera(wynik1);

                        var zad2 = PobierzPodzielone(nazwa);
                        zad2.Wait();
                        var wynik2 = zad2.Result;
                        if (wynik2.Count() == 0)
                        {
                            Dispatcher.InvokeAsync(() => DodajWiadomosc("Na serwerze nie ma takiego pliku!", blad: true));
                            return;
                        }

                        var tablica_string_z_plikiem = wynik2.Select(x => Convert.FromBase64String(x.Replace("\0", ""))).ToArray();

                        var plik_do_zapisu = new byte[tablica_string_z_plikiem.Select(x => x.Length).Sum()];

                        for(int i = 0, j = 0; i < tablica_string_z_plikiem.Length; i++)
                        {

                            Array.Copy(tablica_string_z_plikiem[i], 0, plik_do_zapisu, j, tablica_string_z_plikiem[i].Length);
                            j += tablica_string_z_plikiem[i].Length;

                        }

                        File.WriteAllBytes(zapisz.FileName, plik_do_zapisu);

                        Dispatcher.Invoke(() => Okno.IsEnabled = stare_okno);

                        Dispatcher.Invoke(() => DodajWiadomosc("Plik odebrany."));

                    }),
                    Typ = Typ.OdbierzPlik,
                });

            }

        }

    }

}
