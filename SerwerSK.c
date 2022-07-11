#include <stdio.h>
#include <stdlib.h>

#include <errno.h>
#include <string.h>

#include <netinet/ip.h>
#include <unistd.h>

#include <sys/stat.h>

#include <sys/types.h>

#define ROZMIAR_BUFORA 1024

char *odczytajBajty(FILE *plik, int ilosc_bajtow){
    
    char *bajty = malloc(sizeof(char) * ilosc_bajtow);
    
    if(plik == NULL){
        perror(strerror(errno));
        printf("odczytajBajty(): (FILE *) == NULL\n");
        if(bajty != NULL) free(bajty);
        return NULL;
    }
    
    if(bajty == NULL){
        perror(strerror(errno));
        printf("odczytajBajty(): (char *) == NULL\n");
        return NULL;
    }
    
    if(fread(bajty, sizeof(char), ilosc_bajtow, plik) != ilosc_bajtow){
        perror(strerror(errno));
        printf("odczytajBajty(): fread() != ilosc_bajtow\n");
        if(bajty != NULL) free(bajty);
        return NULL;
    }
    
    return bajty;
    
}

void zapiszBajty(char *bajty, FILE *plik, int ilosc_bajtow){

    if(plik == NULL){
        perror(strerror(errno));
        printf("zapiszBajty(): (FILE *) == NULL\n");
        return;
    }
    
    if(fwrite(bajty, sizeof(char), ilosc_bajtow, plik) != ilosc_bajtow){
        perror(strerror(errno));
        printf("zapiszBajty(): fwrite() != ilosc_bajtow\n");
        return;
    }

}

void wyslijBajty(char *bajty, int gniazdko, int ilosc){
    send(gniazdko, bajty, sizeof(char) * ilosc, 0);
}

void wyslijStringa(char *string, int gniazdko){
    int dlugosc = strlen(string), dlugosc_siec = htonl(dlugosc);
    send(gniazdko, &dlugosc_siec, sizeof(int), 0);
    if(dlugosc > 0){
        printf("[Ty -> Klient] %s\n", string);
        wyslijBajty(string, gniazdko, dlugosc);
    }
}

char *odbierzBajty(int gniazdko, int ilosc_bajtow){

    ssize_t odebrane = 0, temp;
    char *wynik = malloc(sizeof(char) * ilosc_bajtow);

    if(wynik == NULL){
        perror(strerror(errno));
        printf("odbierzBajty(): (char *) == NULL\n");
        return NULL;
    }

    while(odebrane < ilosc_bajtow){
        temp = recv(gniazdko, wynik + odebrane, ilosc_bajtow - odebrane, 0);
        if(temp <= -1){
            perror(strerror(errno));
            printf("odbierzBajty(): recv() <= -1\n");
            free(wynik);
            return NULL;
        }
        else{
            odebrane += temp;
        }
    }

    if(odebrane != ilosc_bajtow){

        printf("odbierzBajty(): odebrane != ilosc_bajtow\n");
        free(wynik);
        return NULL;

    }

    return wynik;

}

char *bajtyNaString(char *bajty, int ilosc_bajtow){

    char *ret = realloc(bajty, ilosc_bajtow + 1);

    if(ret == NULL){
        perror(strerror(errno));
        printf("bajtyNaString(): (char *) == NULL\n");
        return NULL;
    }

    ret[ilosc_bajtow] = '\0';

    return ret;

}

char *pobierzString(int gniazdko, int ilosc_znakow){

    char *bajty = odbierzBajty(gniazdko, ilosc_znakow), *ret = bajtyNaString(bajty, ilosc_znakow);

    if(ret == NULL){
        free(bajty);
    }

    return ret;

}

int pobierzRozmiar(int gniazdko){

    uint32_t bufor;
    int ilosc = recv(gniazdko, &bufor, sizeof(uint32_t), 0);
    if(ilosc != sizeof(uint32_t)){
        if(ilosc != 0){ /* Jesli ilosc == 0, to nastapilo rozlaczenie. */
            perror(strerror(errno));
            printf("pobierzRozmiar(): recv():%i != sizeof(uint32_t):%lu\n", ilosc, sizeof(uint32_t));
        }
        return 0;
    }
    return ntohl(bufor);

}

char *odbierzString(int gniazdko){

    int rozmiar = pobierzRozmiar(gniazdko);
    return rozmiar > 0 ? pobierzString(gniazdko, rozmiar) : NULL;

}

int utworzSocket(int port){

    int gniazdo = socket(PF_INET, SOCK_STREAM, 0);
    socklen_t dlugoscGniazda = sizeof(struct sockaddr_in);
    struct sockaddr_in adresSerwera;

    if(gniazdo <= -1){
        perror(strerror(errno));
        printf("start(): socket() <= -1\n");
        return -1;
    }

    adresSerwera.sin_family = AF_INET;
    adresSerwera.sin_port = htons(port);
    adresSerwera.sin_addr.s_addr = INADDR_ANY;

    if(bind(gniazdo, (struct sockaddr*)&adresSerwera, dlugoscGniazda) != 0){
        perror(strerror(errno));
        printf("start(): bind() != 0\n");
        return -1;
    }
    else{
        printf("TCP:%hi\n", ntohs(adresSerwera.sin_port));
    }

    return gniazdo;

}

int nasluchuj(int gniazdo){

    if(listen(gniazdo, 10) != 0){
        perror(strerror(errno));
        printf("nasluchuj(): listen() != 0\n");
        return -1;
    }
    else{
        printf("Serwer/poczekalnia czeka na klienta...\n");
    }

    return gniazdo;

}

void graczObieraPlik(int gniazdkoPliku){
    
    char *nazwa = odbierzString(gniazdkoPliku);
    FILE *plik = fopen(nazwa, "r");
    int wyslano = 0, rozmiar, temp;
    char *bufor;
    struct stat info;
    
    if(plik == NULL){
        perror(strerror(errno));
        if(bufor != NULL) free(bufor);
        printf("graczOtwieraPlik(): (FILE *) == NULL\n");
        send(gniazdkoPliku, &wyslano, sizeof(int), 0);
        return;
    }

    if(stat(nazwa, &info) <= -1){
        perror(strerror(errno));
        if(bufor != NULL) free(bufor);
        printf("graczOtwieraPlik(): stat() <= -1\n");
        return;
    }
    rozmiar = (int)info.st_size;
    temp = htonl(rozmiar);
    send(gniazdkoPliku, &temp, sizeof(int), 0);

    while(wyslano < rozmiar){

        temp = rozmiar - wyslano;
        if(temp > ROZMIAR_BUFORA) temp = ROZMIAR_BUFORA;
        bufor = odczytajBajty(plik, temp);
        wyslijBajty(bufor, gniazdkoPliku, temp);
        wyslano += temp;

    }
    
    fclose(plik);

    printf("Wyslano plik: %s; o wielkosci: %u\n", nazwa, rozmiar);
    
    free(bufor);
    
}

void graczWysylaPlik(int gniazdko){

    char *nazwa = odbierzString(gniazdko);
    uint32_t rozmiar = pobierzRozmiar(gniazdko);
    int pobrano = 0, temp, do_pobrania_temp;
    char *bufor = malloc(sizeof(char) * ROZMIAR_BUFORA);
    FILE *plik = fopen(nazwa, "w");

    if(bufor == NULL){
        perror(strerror(errno));
        printf("graczWysylaPlik(): (char *) == NULL\n");
        send(gniazdko, &pobrano, sizeof(int), 0);
        return;
    }

    while(pobrano < rozmiar){

        if(rozmiar > ROZMIAR_BUFORA) do_pobrania_temp = ROZMIAR_BUFORA;
        else do_pobrania_temp = rozmiar;
        temp = recv(gniazdko, bufor, sizeof(char) * do_pobrania_temp, 0);
        zapiszBajty(bufor, plik, temp);
        do_pobrania_temp -= temp;
        pobrano += temp;

    }

    fclose(plik);

    printf("Odebrano plik: %s; o wielkosci: %u\n", nazwa, rozmiar);
    
    free(bufor);

}

int obsluzPolecenie(int gniazdko, char **wiadomosciX, char **wiadomosciY, char *szachownica, char gracz){

    char *polecenie = odbierzString(gniazdko), *temp;
    uint32_t miejsce;

    if(polecenie == NULL){
        return 1;
    }
    printf("Polecenie: %s\n", polecenie);
    if(strcmp(polecenie, "KONIEC") == 0){
        return 1;
    }else if(strcmp(polecenie, "WIADOMOSC") == 0){
        temp = odbierzString(gniazdko);
        temp = realloc(temp, sizeof(char) * (strlen(temp) + 2));
        strcat(temp, "|");
        *wiadomosciX = realloc(*wiadomosciX, sizeof(char) * (strlen(*wiadomosciX) + strlen(temp) + 1));
        strcat(*wiadomosciX, temp);
        *wiadomosciY = realloc(*wiadomosciY, sizeof(char) * (strlen(*wiadomosciY) + strlen(temp) + 1));
        strcat(*wiadomosciY, temp);
    }
    else if(strcmp(polecenie, "POKAZ") == 0){
        if(gracz == 'X'){
            wyslijStringa(*wiadomosciX, gniazdko);
            *wiadomosciX = realloc(*wiadomosciX, sizeof(char) * 1);
            **wiadomosciX = '\0';
        }else{
            wyslijStringa(*wiadomosciY, gniazdko);
            *wiadomosciY = realloc(*wiadomosciY, sizeof(char) * 1);
            **wiadomosciY = '\0';
        }
    }else if(strcmp(polecenie, "RUCH") == 0){
        miejsce = pobierzRozmiar(gniazdko);
        if(miejsce < 0 || miejsce > 8){
            printf("obsluzPolecenie(): Zly numer pola!\n");
            return 0;
        }
        szachownica[miejsce] = gracz;
    }else if(strcmp(polecenie, "SZACHOWNICA") == 0){
        wyslijBajty(szachownica, gniazdko, 9);
    }else if(strcmp(polecenie, "WYSLIJ") == 0){
        return 100;
    }else if(strcmp(polecenie, "ODBIERZ") == 0){
        return 101;
    }else{
        printf("Nieznane polecenie poczekalni.\n");
    }

    return 0;

}

void poczekalnia(int gniazdo, int portDoPlikow){

    fd_set zbior_socketow;
    int maxgniazdo = 0, gniazdkoX = 0, gniazdkoY = 0, temp = 0, ret = 0, gniazdoPliku, wyczysc = 1, gniazdkoPliku;
    socklen_t dlugoscAdresuKlientaX = sizeof(struct sockaddr_in), dlugoscAdresuKlientaY = dlugoscAdresuKlientaX;
    struct sockaddr_in *graczX = calloc(dlugoscAdresuKlientaX, 1), *graczY = calloc(dlugoscAdresuKlientaY, 1);
    char *wiadomosciX = calloc(sizeof(char), 1), *wiadomosciY = calloc(sizeof(char), 1), *szachownica = malloc(sizeof(char) * 9);

    for(; temp < 9; temp++){
        szachownica[temp] = 'P';
    }

    while(1){

        FD_ZERO(&zbior_socketow);
        if(gniazdo > 0){
            FD_SET(gniazdo, &zbior_socketow);
            if(gniazdo > maxgniazdo) maxgniazdo = gniazdo;
        }
        if(gniazdkoX > 0){
            FD_SET(gniazdkoX, &zbior_socketow);
            if(gniazdkoX > maxgniazdo) maxgniazdo = gniazdkoX;
        }
        if(gniazdkoY > 0){
            FD_SET(gniazdkoY, &zbior_socketow);
            if(gniazdkoY > maxgniazdo) maxgniazdo = gniazdkoY;
        }

        if(gniazdo <= 0 && gniazdkoX <= 0 && gniazdkoY <= 0){
            printf("Wszyscy sie rolaczyli - koniec pracy poczekalni.\n");
            break;
        }

        if(select(maxgniazdo + 1, &zbior_socketow, NULL, NULL, NULL) >= 0){
            if(FD_ISSET(gniazdo, &zbior_socketow)){
                if(gniazdkoX == 0){
                    printf("Polaczyl sie X!\n");
                    gniazdkoX = accept(gniazdo, (struct sockaddr *)graczX, &dlugoscAdresuKlientaX);
                    wyslijStringa("HELLOX", gniazdkoX);
                }
                else if(gniazdkoY == 0){
                    printf("Polaczyl sie Y!\n");
                    gniazdkoY = accept(gniazdo, (struct sockaddr *)graczY, &dlugoscAdresuKlientaY);
                    wyslijStringa("HELLOO", gniazdkoY);
                    close(gniazdo);
                    gniazdo = -1;
                }
                else{
                    printf("poczekalnia(): Ktos chcial sie podlaczyc na trzeciego.\n");
                }
            }
            else{
                if(FD_ISSET(gniazdkoX, &zbior_socketow)){
                    printf("Polecenie od X.\n");
                    ret = obsluzPolecenie(gniazdkoX, &wiadomosciX, &wiadomosciY, szachownica, 'X');
                    if(ret == 1){
                        close(gniazdkoX);
                        gniazdkoX = -1;
                    }else if(ret == 100 || ret == 101){
                        gniazdoPliku = nasluchuj(utworzSocket(portDoPlikow));
                        temp = fork();
                        if(temp == 0){
                            wyczysc = 0;
                            portDoPlikow = htonl(portDoPlikow);
                            send(gniazdkoX, &portDoPlikow, sizeof(int), 0);
                            if(gniazdo > 0) close(gniazdo);
                            if(gniazdkoX > 0) close(gniazdkoX);
                            if(gniazdkoY > 0) close(gniazdkoY);
                            free(graczX);
                            free(graczY);
                            free(wiadomosciX);
                            free(wiadomosciY);
                            free(szachownica);
                            gniazdkoPliku = accept(gniazdoPliku, (struct sockaddr *)graczX, &dlugoscAdresuKlientaX);
                            if(ret == 100) graczWysylaPlik(gniazdkoPliku);
                            else if(ret == 101) graczObieraPlik(gniazdkoPliku);
                            close(gniazdoPliku);
                            break;
                        }else if(temp > 0){
                            portDoPlikow++;
                            close(gniazdoPliku);
                        }else{
                            printf("poczekalnia(): fork() < 0\n");
                        }
                    }
                }
                else if(FD_ISSET(gniazdkoY, &zbior_socketow)){
                    printf("Polecenie od Y.\n");
                    ret = obsluzPolecenie(gniazdkoY, &wiadomosciX, &wiadomosciY, szachownica, 'Y');
                    if(ret == 1){
                        close(gniazdkoY);
                        gniazdkoY = -1;
                    }else if(ret == 100 || ret == 101){
                        gniazdoPliku = nasluchuj(utworzSocket(portDoPlikow));
                        temp = fork();
                        if(temp == 0){
                            wyczysc = 0;
                            portDoPlikow = htonl(portDoPlikow);
                            send(gniazdkoY, &portDoPlikow, sizeof(int), 0);
                            if(gniazdo > 0) close(gniazdo);
                            if(gniazdkoX > 0) close(gniazdkoX);
                            if(gniazdkoY > 0) close(gniazdkoY);
                            free(graczX);
                            free(graczY);
                            free(wiadomosciX);
                            free(wiadomosciY);
                            free(szachownica);
                            gniazdkoPliku = accept(gniazdoPliku, (struct sockaddr *)graczY, &dlugoscAdresuKlientaY);
                            if(ret == 100) graczWysylaPlik(gniazdkoPliku);
                            else if(ret == 101) graczObieraPlik(gniazdkoPliku);
                            close(gniazdoPliku);
                            break;
                        }else if(temp > 0){
                            portDoPlikow++;
                            close(gniazdoPliku);
                        }else{
                            printf("poczekalnia(): fork() < 0\n");
                        }
                    }
                }
                else{
                    printf("Dane od nieznanego odbiorcy!\n");
                }
            }
        }
        else{
            perror(strerror(errno));
            printf("poczekalnia(): select() < 0\n");
            return;
        }

        maxgniazdo = 0;

    }
    
    if(wyczysc == 1){
        free(graczX);
        free(graczY);
        free(wiadomosciX);
        free(wiadomosciY);
        free(szachownica);
        if(gniazdo > 0) close(gniazdo);
        if(gniazdkoX > 0) close(gniazdkoX);
        if(gniazdkoY > 0) close(gniazdkoY);
    }
    
}

int nowaGra(int port_poczekalni){
    return nasluchuj(utworzSocket(port_poczekalni));
}

int obsluzGre(int gniazdko, int gniazdo, int port_poczekalni){

    pid_t pid = fork();
    int gniazdo_poczekalni, temp;

    if(pid < 0){
        perror(strerror(errno));
        printf("obsluzGre(): fork() < 0\n");
        return 1;
    }
    else if(pid == 0){
        gniazdo_poczekalni = nowaGra(port_poczekalni);
        temp = htonl(port_poczekalni);
        send(gniazdko, &temp, sizeof(int), 0);
        close(gniazdko);
        close(gniazdo);
        poczekalnia(gniazdo_poczekalni, port_poczekalni + 100);
        return 2;
    }
    else{
        return 0;
    }

}

int obsluzSerwer(int gniazdko, int gniazdo, int port_poczekalni){

    char *polecenie = odbierzString(gniazdko);

    if(strcmp(polecenie, "GRA") == 0){
        free(polecenie);
        return obsluzGre(gniazdko, gniazdo, port_poczekalni);
    }
    else if(strcmp(polecenie, "KONIEC") == 0){
        free(polecenie);
        return 1;
    }
    else{
        printf("Nieznane polecenie: \"%s\".\n", polecenie);
        free(polecenie);
        return 0;
    }

}

int czekajNaKlientaSerwera(int gniazdo){

    int gniazdko;
    socklen_t dlugoscAdresuKlienta = sizeof(struct sockaddr_in);
    struct sockaddr_in *adresKlienta = calloc(dlugoscAdresuKlienta, 1);

    if((gniazdko = accept(gniazdo, (struct sockaddr *)adresKlienta, &dlugoscAdresuKlienta)) <= -1){
        perror(strerror(errno));
        printf("czekajNaKlientaSerwera(): accept() <= -1\n");
        free(adresKlienta);
        return -1;
    }

    return gniazdko;

}

int main(int argc, char **argv){

    int port = 1337, status, gniazdko, gniazdo = nasluchuj(utworzSocket(port)), port_poczekalni = port+1;

    if(argc == 2) port = atoi(argv[1]);
    else if(argc > 2){
        printf("main(): argc > 2\n");
    }

    do{
        gniazdko = czekajNaKlientaSerwera(gniazdo);
        status = obsluzSerwer(gniazdko, gniazdo, port_poczekalni);
        close(gniazdko);
        port_poczekalni++;
    }while(status == 0);

    if(status != 2){
        close(gniazdo);
    }

    return 0;

}
