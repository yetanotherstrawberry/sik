Programy stworzone jako projekt na przedmiot "Sieci komputerowe".
SerwerSK.c uruchamia serwer na komputerze, do którego może podłączyć się klient C# z folderu SKProj1.
Klient działa asynchronicznie - wysyłanie plików nie opóźnia przesyłania wiadomości.
Projekt umożliwia granie w kółko i krzyżyk, przesyłanie wiadomości oraz plików.
Serwer zaczyna uruchamiać poczekalnię na porcie i czeka na 2 klientów.
Kiedy oby dwoje klienci się podłączą to serwer ich obsługuje i tworzy kolejną poczekalnię na porcie o 1 wyższym.
